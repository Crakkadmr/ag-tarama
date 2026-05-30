using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

internal sealed class MdnsListener : IListener
{
    public string Name => "mDNS";

    private static readonly IPAddress MdnsMulticast = IPAddress.Parse("224.0.0.251");
    private const int MdnsPort = 5353;

    private static readonly (string Servis, string Marka, string Tur)[] MdnsServisler =
    {
        ("_onvif._tcp.local",           "",           "Kamera"),
        ("_rtsp._tcp.local",            "",           "Kamera"),
        ("_dahua-ipc._tcp.local",       "Dahua",      "Kamera"),
        ("_hikvision._tcp.local",       "Hikvision",  "Kamera"),
        ("_axis-video._tcp.local",      "Axis",       "Kamera"),
        ("_googlecast._tcp.local",      "Google",     "Akıllı TV"),
        ("_airplay._tcp.local",         "Apple",      "Akıllı TV"),
        ("_ipp._tcp.local",             "",           "Yazıcı"),
        ("_ipps._tcp.local",            "",           "Yazıcı"),
        ("_printer._tcp.local",         "",           "Yazıcı"),
        ("_smb._tcp.local",             "",           "Bilgisayar"),
        ("_workstation._tcp.local",     "",           "Bilgisayar"),
        ("_ssh._tcp.local",             "",           "Bilgisayar"),
        ("_spotify-connect._tcp.local", "",           "Müzik Cihazı"),
        ("_amzn-wplay._tcp.local",      "Amazon",     "Akıllı TV"),
        ("_http._tcp.local",            "",           "Cihaz"),
    };

    public async Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
            udp.JoinMulticastGroup(MdnsMulticast);

            // Send queries for all services
            foreach (var (servis, _, _) in MdnsServisler)
            {
                if (token.IsCancellationRequested) break;
                var q = BuildMdnsQuery(servis);
                await udp.SendAsync(q, q.Length, new IPEndPoint(MdnsMulticast, MdnsPort))
                    .ConfigureAwait(false);
            }

            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await udp.ReceiveAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                var srcIp = res.RemoteEndPoint.Address.ToString();
                if (!srcIp.StartsWith(subnetPrefix + ".", StringComparison.Ordinal)) continue;

                var parsed = ParseMdnsPacket(res.Buffer);
                if (string.IsNullOrWhiteSpace(parsed.Marka) &&
                    string.IsNullOrWhiteSpace(parsed.Tur) &&
                    string.IsNullOrWhiteSpace(parsed.Hostname)) continue;

                var bilgi = store.GetOrAdd(srcIp);
                if (!string.IsNullOrWhiteSpace(parsed.Marka)) bilgi.MdnsMarka = parsed.Marka;
                if (!string.IsNullOrWhiteSpace(parsed.Tur))   bilgi.MdnsTur   = parsed.Tur;
                if (!string.IsNullOrWhiteSpace(parsed.Hostname) &&
                    string.IsNullOrWhiteSpace(bilgi.DnsAdi))
                {
                    bilgi.DnsAdi = parsed.Hostname;
                }
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("mDNS");
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }

    private static byte[] BuildMdnsQuery(string name)
    {
        var parts = name.TrimEnd('.').Split('.');
        var buf = new List<byte>();
        // Header: ID=0, Flags=0 (standard query), QDCOUNT=1
        buf.AddRange(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 });
        foreach (var p in parts)
        {
            buf.Add((byte)p.Length);
            buf.AddRange(Encoding.ASCII.GetBytes(p));
        }
        buf.Add(0);
        buf.AddRange(new byte[] { 0x00, 0x0C, 0x00, 0x01 }); // PTR, IN
        return buf.ToArray();
    }

    // Parse mDNS response: extract (Marka, Tur) from service match + Hostname from SRV/A records.
    internal static (string Marka, string Tur, string? Hostname) ParseMdnsPacket(byte[] buf)
    {
        // Servis adı → Marka/Tur eşlemesi (mevcut basit text-contains davranışı korunur)
        var lowerText = Encoding.UTF8.GetString(buf).ToLowerInvariant();
        string marka = "", tur = "";
        foreach (var (servis, m, t) in MdnsServisler)
        {
            var key = servis.Split('.')[0].ToLowerInvariant().TrimStart('_');
            if (lowerText.Contains(key, StringComparison.Ordinal)) { marka = m; tur = t; break; }
        }

        // Hostname: DNS message parse — SRV target veya A record name'inden `<host>.local` çıkar.
        var hostname = ExtractHostnameFromDnsMessage(buf);
        return (marka, tur, hostname);
    }

    // mDNS DNS message parser — sadece hostname için.
    // Header (12 byte) → QDCOUNT × Question → ANCOUNT × Answer.
    // Answer'da TYPE = SRV(33) → RDATA içinde target name; TYPE = A(1) veya AAAA(28) → name field zaten host.
    private static string? ExtractHostnameFromDnsMessage(byte[] buf)
    {
        if (buf.Length < 12) return null;
        try
        {
            int qd = (buf[4] << 8) | buf[5];
            int an = (buf[6] << 8) | buf[7];
            int pos = 12;

            // Skip questions
            for (int i = 0; i < qd && pos < buf.Length; i++)
            {
                pos = SkipDnsName(buf, pos);
                pos += 4; // QTYPE + QCLASS
            }

            string? bestHost = null;

            for (int i = 0; i < an && pos < buf.Length; i++)
            {
                int nameStart = pos;
                string? name = ReadDnsName(buf, ref pos, out _);
                if (pos + 10 > buf.Length) break;
                int type   = (buf[pos] << 8) | buf[pos + 1];
                // int klass = (buf[pos + 2] << 8) | buf[pos + 3];
                // uint ttl = (uint)((buf[pos+4] << 24) | (buf[pos+5] << 16) | (buf[pos+6] << 8) | buf[pos+7]);
                int rdlen  = (buf[pos + 8] << 8) | buf[pos + 9];
                pos += 10;
                int rdStart = pos;
                if (rdStart + rdlen > buf.Length) break;

                string? candidate = null;

                switch (type)
                {
                    case 33: // SRV — RDATA: priority(2) weight(2) port(2) target(name)
                        if (rdlen >= 7)
                        {
                            int srvNamePos = rdStart + 6;
                            candidate = ReadDnsName(buf, ref srvNamePos, out _);
                        }
                        break;

                    case 1:  // A
                    case 28: // AAAA
                        candidate = name; // record name = `<host>.local`
                        break;

                    case 12: // PTR — RDATA contains target name; sometimes `<instance>._svc._tcp.local`
                        var ptrPos = rdStart;
                        candidate = ReadDnsName(buf, ref ptrPos, out _);
                        // PTR genelde servis instance — sadece A/SRV yoksa fallback
                        if (candidate != null && (candidate.Contains("._tcp.", StringComparison.Ordinal) ||
                                                  candidate.Contains("._udp.", StringComparison.Ordinal)))
                        {
                            // Servis adı; instance kısmını host adı olarak alabiliriz
                            var dot = candidate.IndexOf('.');
                            if (dot > 0) candidate = candidate[..dot];
                            else candidate = null;
                        }
                        break;
                }

                pos = rdStart + rdlen;

                if (string.IsNullOrWhiteSpace(candidate)) continue;

                var normalized = NormalizeHost(candidate);
                if (normalized == null) continue;

                // En kısa, anlamlı hostname tercih edilir (A/SRV > PTR fallback)
                if (bestHost == null || (type is 1 or 28 or 33))
                    bestHost = normalized;
            }

            return bestHost;
        }
        catch { return null; }
    }

    private static int SkipDnsName(byte[] buf, int pos)
    {
        while (pos < buf.Length)
        {
            byte len = buf[pos];
            if (len == 0) { pos++; break; }
            if ((len & 0xC0) == 0xC0) { pos += 2; break; }
            pos += len + 1;
        }
        return pos;
    }

    // DNS name reader — compressed pointer (0xC0) destekli.
    // pos referans güncellenir; 'jumped' true ise pos sadece pointer'ı tüketmiş kabul edilir.
    private static string? ReadDnsName(byte[] buf, ref int pos, out bool jumped)
    {
        jumped = false;
        var sb = new StringBuilder();
        int safety = 128;
        int? returnTo = null;

        while (pos < buf.Length && safety-- > 0)
        {
            byte len = buf[pos];
            if (len == 0)
            {
                pos++;
                break;
            }
            if ((len & 0xC0) == 0xC0)
            {
                if (pos + 1 >= buf.Length) return null;
                int target = ((len & 0x3F) << 8) | buf[pos + 1];
                if (!jumped) returnTo = pos + 2;
                pos = target;
                jumped = true;
                continue;
            }
            pos++;
            if (pos + len > buf.Length) return null;
            if (sb.Length > 0) sb.Append('.');
            sb.Append(Encoding.UTF8.GetString(buf, pos, len));
            pos += len;
        }

        if (returnTo.HasValue) pos = returnTo.Value;

        var result = sb.ToString();
        return result.Length > 0 ? result : null;
    }

    private static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var temiz = host.Trim().TrimEnd('.');
        if (temiz.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            temiz = temiz[..^6];
        // Servis adı kalıntılarını reddet (örn. "._tcp" / "._sub")
        if (temiz.Length == 0 || temiz.StartsWith("_", StringComparison.Ordinal)) return null;
        if (temiz.Contains("._tcp", StringComparison.OrdinalIgnoreCase) ||
            temiz.Contains("._udp", StringComparison.OrdinalIgnoreCase) ||
            temiz.Contains("._sub", StringComparison.OrdinalIgnoreCase)) return null;
        if (temiz.Contains("in-addr.arpa", StringComparison.OrdinalIgnoreCase)) return null;
        return temiz;
    }
}
