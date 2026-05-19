using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

// Link-Local Multicast Name Resolution — UDP 5355, Windows hostname discovery
internal sealed class LlmnrProbe : IProbe
{
    public string Name => "LLMNR";

    private static readonly IPAddress LlmnrMulticast = IPAddress.Parse("224.0.0.252");
    private const int LlmnrPort = 5355;

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        try
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            // Send LLMNR query for each host's reverse PTR name
            int count = Math.Max(0, hostEnd - hostStart + 1);
            foreach (var i in Enumerable.Range(hostStart, count))
            {
                if (token.IsCancellationRequested) break;
                var ip = $"{subnetPrefix}.{i}";
                var queryBytes = BuildLlmnrQuery(ip);
                await udp.SendAsync(queryBytes, queryBytes.Length,
                    new IPEndPoint(LlmnrMulticast, LlmnrPort)).ConfigureAwait(false);
            }

            using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts2.CancelAfter(4000);
            while (!cts2.Token.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await udp.ReceiveAsync(cts2.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                var srcIp = res.RemoteEndPoint.Address.ToString();
                if (!srcIp.StartsWith(subnetPrefix + ".", StringComparison.Ordinal)) continue;

                var name = ParseLlmnrResponseName(res.Buffer);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var bilgi = store.GetOrAdd(srcIp);
                bilgi.LlmnrHostname = name;
                bilgi.Online        = true;
                bilgi.KesifKaynaklari.Add("LLMNR");
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }

    private static byte[] BuildLlmnrQuery(string ip)
    {
        // LLMNR query for reverse lookup: 1.0.168.192.in-addr.arpa PTR
        var parts = ip.Split('.');
        var qname = $"{parts[3]}.{parts[2]}.{parts[1]}.{parts[0]}.in-addr.arpa";
        var sb = new System.Collections.Generic.List<byte>();
        // Header: ID=0x0001, Flags=0x0000 (standard query), QDCOUNT=1
        sb.AddRange(new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        foreach (var label in qname.Split('.'))
        {
            sb.Add((byte)label.Length);
            sb.AddRange(Encoding.ASCII.GetBytes(label));
        }
        sb.Add(0x00); // end
        sb.AddRange(new byte[] { 0x00, 0x0C, 0x00, 0x01 }); // PTR, IN
        return sb.ToArray();
    }

    private static string? ParseLlmnrResponseName(byte[] buf)
    {
        if (buf.Length < 14) return null;
        try
        {
            // Must be a response (QR bit set) with at least 1 answer
            if ((buf[2] & 0x80) == 0) return null;
            int anCount = (buf[6] << 8) | buf[7];
            if (anCount < 1) return null;

            int pos = 12;
            // Skip question QNAME — handles both full labels and compressed pointers
            while (pos < buf.Length && buf[pos] != 0)
            {
                if ((buf[pos] & 0xC0) == 0xC0) { pos += 2; goto skipQType; }
                pos += buf[pos] + 1;
            }
            if (pos < buf.Length) pos++; // null terminator
            skipQType:
            pos += 4; // QTYPE (2) + QCLASS (2)

            if (pos >= buf.Length) return null;

            // Skip Answer NAME (compressed = 2 bytes; full labels = walk)
            if ((buf[pos] & 0xC0) == 0xC0)
                pos += 2;
            else
            {
                while (pos < buf.Length && buf[pos] != 0)
                    pos += buf[pos] + 1;
                if (pos < buf.Length) pos++;
            }

            // TYPE(2) + CLASS(2) + TTL(4) + RDLENGTH(2) = 10 bytes
            if (pos + 10 > buf.Length) return null;
            pos += 10;

            // Read RDATA as DNS name (PTR target), with compressed pointer support
            var sb = new StringBuilder();
            bool jumped = false;
            int safetyLimit = 128;
            while (pos < buf.Length && buf[pos] != 0 && safetyLimit-- > 0)
            {
                if ((buf[pos] & 0xC0) == 0xC0)
                {
                    if (pos + 1 >= buf.Length) break;
                    pos = ((buf[pos] & 0x3F) << 8) | buf[pos + 1];
                    jumped = true;
                    continue;
                }
                int len = buf[pos++];
                if (len == 0 || pos + len > buf.Length) break;
                if (sb.Length > 0) sb.Append('.');
                sb.Append(Encoding.ASCII.GetString(buf, pos, len));
                pos += len;
            }

            var result = sb.ToString().TrimEnd('.');
            // Reject reverse-lookup arpa names (e.g. 127.1.168.192.in-addr.arpa)
            if (result.EndsWith(".arpa", StringComparison.OrdinalIgnoreCase) ||
                result.Equals("arpa", StringComparison.OrdinalIgnoreCase)) return null;
            return result.Length > 0 ? result : null;
        }
        catch { return null; }
    }
}
