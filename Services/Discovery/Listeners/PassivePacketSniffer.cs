using System;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

// Passive NIC sniffer via SharpPcap — BPF filter: arp or mDNS/NetBIOS/LLMNR
// Falls back silently if Npcap is not available.
internal sealed class PassivePacketSniffer : IListener
{
    public string Name => "PassiveSniffer";

    private const string BpfFilter = "arp or udp port 5353 or udp port 137 or udp port 5355";

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

            device.OnPacketArrival += (_, e) =>
            {
                try
                {
                    var raw = e.GetPacket();
                    var pkt = PacketDotNet.Packet.ParsePacket(raw.LinkLayerType, raw.Data);

                    // ARP replies → get MAC
                    var arp = pkt.Extract<PacketDotNet.ArpPacket>();
                    if (arp != null)
                    {
                        var ip = arp.SenderProtocolAddress.ToString();
                        if (ip.StartsWith(subnetPrefix + ".", StringComparison.Ordinal))
                        {
                            var normalizedMac = MacUtils.Normalize(arp.SenderHardwareAddress.ToString());
                            if (MacUtils.IsValidUnicast(normalizedMac))
                            {
                                var bilgi = store.GetOrAdd(ip);
                                bilgi.MacAdresi ??= normalizedMac;
                                bilgi.Online = true;
                                bilgi.KesifKaynaklari.Add("ARP");
                                store.NotifyChanged(bilgi);
                            }
                        }
                        return;
                    }

                    // UDP payload for mDNS / NetBIOS / LLMNR
                    var udp = pkt.Extract<PacketDotNet.UdpPacket>();
                    if (udp?.ParentPacket is PacketDotNet.IPv4Packet ipv4)
                    {
                        var ip = ipv4.SourceAddress.ToString();
                        if (!ip.StartsWith(subnetPrefix + ".", StringComparison.Ordinal)) return;

                        var bilgi = store.GetOrAdd(ip);
                        switch (udp.DestinationPort)
                        {
                            case 5353:
                                bilgi.Online = true;
                                bilgi.KesifKaynaklari.Add("mDNS");
                                store.NotifyChanged(bilgi);
                                break;
                            case 137:
                                bilgi.Online = true;
                                bilgi.KesifKaynaklari.Add("NetBIOS");
                                store.NotifyChanged(bilgi);
                                break;
                            case 5355:
                                bilgi.Online = true;
                                bilgi.KesifKaynaklari.Add("LLMNR");
                                store.NotifyChanged(bilgi);
                                break;
                        }
                    }
                }
                catch { }
            };

            device.StartCapture();

            // Run until cancellation
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
}
