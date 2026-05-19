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

                var (marka, tur) = ParseMdnsPacket(res.Buffer);
                if (string.IsNullOrWhiteSpace(marka) && string.IsNullOrWhiteSpace(tur)) continue;

                var bilgi = store.GetOrAdd(srcIp);
                if (!string.IsNullOrWhiteSpace(marka)) bilgi.MdnsMarka = marka;
                if (!string.IsNullOrWhiteSpace(tur))   bilgi.MdnsTur   = tur;
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

    private static (string Marka, string Tur) ParseMdnsPacket(byte[] buf)
    {
        var text = Encoding.UTF8.GetString(buf).ToLowerInvariant();
        foreach (var (servis, marka, tur) in MdnsServisler)
        {
            var key = servis.Split('.')[0].ToLowerInvariant().TrimStart('_');
            if (text.Contains(key, StringComparison.Ordinal))
                return (marka, tur);
        }
        return ("", "");
    }
}
