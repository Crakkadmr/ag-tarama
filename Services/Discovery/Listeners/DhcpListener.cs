using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Listeners;

// DHCP pasif sniff — UDP 67/68 broadcast yakalama.
// BOOTP/DHCP RFC 2131: 240 byte BOOTP header + magic cookie + TLV options.
// Yararlı option'lar:
//   12 (Host Name)         → hostname → DeviceInfo.DhcpHostname
//   55 (Parameter Request) → OS fingerprint signature → DhcpFingerprint
//   60 (Vendor Class ID)   → "MSFT 5.0" / "android-dhcp-13" / "udhcp" → marka+tür
internal sealed class DhcpListener : IListener
{
    public string Name => "DHCP";

    private const string BpfFilter = "udp and (port 67 or port 68)";

    public async Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token)
    {
        if (!PcapHelper.IsNpcapAvailable) return;

        var localIp = PcapHelper.GetLocalIpForSubnet(subnetPrefix);
        if (localIp == null) return;

        var device = PcapHelper.GetDeviceForIp(localIp);
        if (device == null) return;

        try
        {
            device.Open(new SharpPcap.DeviceConfiguration { Mode = SharpPcap.DeviceModes.Promiscuous, ReadTimeout = 100 });
            device.Filter = BpfFilter;

            device.OnPacketArrival += (_, e) => HandlePacket(e, subnetPrefix, store);

            device.StartCapture();

            try { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }

            device.StopCapture();
        }
        catch { }
        finally
        {
            try { device.Close(); } catch { }
        }
    }

    private static void HandlePacket(SharpPcap.PacketCapture e, string subnetPrefix, DeviceStore store)
    {
        try
        {
            var raw = e.GetPacket();
            var pkt = PacketDotNet.Packet.ParsePacket(raw.LinkLayerType, raw.Data);
            var udp = pkt.Extract<PacketDotNet.UdpPacket>();
            if (udp == null) return;

            var payload = udp.PayloadData;
            if (payload == null || payload.Length < 240) return;

            // BOOTP magic cookie kontrolü (240-243: 99,130,83,99 = 0x63825363)
            if (payload[236] != 0x63 || payload[237] != 0x82 ||
                payload[238] != 0x53 || payload[239] != 0x63) return;

            // BOOTP: chaddr = bytes 28..34 (6 byte MAC)
            var macBytes = payload[28..34];
            var mac = MacUtils.Normalize(string.Join(":", macBytes.Select(b => b.ToString("X2"))));
            if (!MacUtils.IsValidUnicast(mac)) return;

            // ciaddr (client IP, bytes 12-15) → tercihen kullan; yoksa yiaddr (your IP, bytes 16-19)
            string? ip = null;
            if (payload[12] != 0 || payload[13] != 0 || payload[14] != 0 || payload[15] != 0)
                ip = $"{payload[12]}.{payload[13]}.{payload[14]}.{payload[15]}";
            else if (payload[16] != 0 || payload[17] != 0 || payload[18] != 0 || payload[19] != 0)
                ip = $"{payload[16]}.{payload[17]}.{payload[18]}.{payload[19]}";

            if (ip == null || !ip.StartsWith(subnetPrefix + ".", StringComparison.Ordinal)) return;

            // Options TLV parse: payload[240..]
            string? hostname = null, vendorClass = null, paramList = null;
            int p = 240;
            while (p < payload.Length)
            {
                byte opt = payload[p++];
                if (opt == 0xFF) break;       // END
                if (opt == 0x00) continue;    // PAD
                if (p >= payload.Length) break;
                byte len = payload[p++];
                if (p + len > payload.Length) break;

                switch (opt)
                {
                    case 12: hostname    = Encoding.UTF8.GetString(payload, p, len); break;
                    case 60: vendorClass = Encoding.UTF8.GetString(payload, p, len); break;
                    case 55: paramList   = string.Join(",", payload[p..(p + len)].Select(b => b.ToString())); break;
                }
                p += len;
            }

            if (string.IsNullOrWhiteSpace(hostname) &&
                string.IsNullOrWhiteSpace(vendorClass) &&
                string.IsNullOrWhiteSpace(paramList)) return;

            var bilgi = store.GetOrAdd(ip);
            bilgi.MacAdresi ??= mac;
            if (!string.IsNullOrWhiteSpace(hostname))    bilgi.DhcpHostname    = hostname;
            if (!string.IsNullOrWhiteSpace(vendorClass)) bilgi.DhcpVendorClass = vendorClass;
            if (!string.IsNullOrWhiteSpace(paramList))   bilgi.DhcpFingerprint = paramList;
            bilgi.Online = true;
            bilgi.KesifKaynaklari.Add("DHCP");
            store.NotifyChanged(bilgi);
        }
        catch { }
    }
}
