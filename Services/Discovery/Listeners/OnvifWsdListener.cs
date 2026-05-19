using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

internal sealed class OnvifWsdListener : IListener
{
    public string Name => "ONVIF/WSD";

    public async Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token)
    {
        try
        {
            string onvifProbe = BuildProbe("dn:NetworkVideoTransmitter");
            string wsdProbe   = BuildProbe("wsdp:Device");

            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            var hedef = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702);

            var ob = Encoding.UTF8.GetBytes(onvifProbe);
            var wb = Encoding.UTF8.GetBytes(wsdProbe);
            await udp.SendAsync(ob, ob.Length, hedef).ConfigureAwait(false);
            await udp.SendAsync(wb, wb.Length, hedef).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await udp.ReceiveAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                var srcIp = res.RemoteEndPoint.Address.ToString();
                if (!srcIp.StartsWith(subnetPrefix + ".", StringComparison.Ordinal)) continue;
                var xml = Encoding.UTF8.GetString(res.Buffer);
                if (!xml.Contains("ProbeMatch")) continue;

                var bilgi = store.GetOrAdd(srcIp);

                bool isOnvif = xml.Contains("NetworkVideoTransmitter", StringComparison.OrdinalIgnoreCase) ||
                               xml.Contains("onvif://", StringComparison.OrdinalIgnoreCase);
                bool isWsd   = xml.Contains("wsdp:", StringComparison.OrdinalIgnoreCase) ||
                               xml.Contains("PrintDeviceType", StringComparison.OrdinalIgnoreCase);

                if (isOnvif)
                {
                    bilgi.OnvifBulundu = true;
                    var xAddr = Regex.Match(xml, @"<[^>]*XAddrs[^>]*>([^<]+)<");
                    if (xAddr.Success) bilgi.OnvifServisUrl = xAddr.Groups[1].Value.Trim().Split(' ')[0];
                    foreach (Match m in Regex.Matches(xml, @"onvif://www\.onvif\.org/(\w+)/([^<\s""]+)"))
                    {
                        var key = m.Groups[1].Value;
                        var val = Uri.UnescapeDataString(m.Groups[2].Value);
                        if (key == "hardware") bilgi.OnvifHardware = val;
                        if (key == "name")     bilgi.OnvifAdi      = val;
                        if (key == "location") bilgi.OnvifKonum    = val;
                    }
                    bilgi.KesifKaynaklari.Add("ONVIF");
                }

                if (isWsd)
                {
                    bilgi.WsdTipi = xml.Contains("PrintDeviceType", StringComparison.OrdinalIgnoreCase)
                        ? "Yazıcı"
                        : xml.Contains("ScanDeviceType", StringComparison.OrdinalIgnoreCase)
                            ? "Tarayıcı"
                            : "WSD";
                    bilgi.KesifKaynaklari.Add("WSD");
                }

                bilgi.Online = true;
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }

    private static string BuildProbe(string types)
    {
        var id = Guid.NewGuid();
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope""
               xmlns:wsa=""http://schemas.xmlsoap.org/ws/2004/08/addressing""
               xmlns:wsd=""http://schemas.xmlsoap.org/ws/2005/04/discovery""
               xmlns:wsdp=""http://schemas.xmlsoap.org/ws/2006/02/devprof""
               xmlns:dn=""http://www.onvif.org/ver10/network/wsdl"">
  <soap:Header>
    <wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To>
    <wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action>
    <wsa:MessageID>uuid:{id}</wsa:MessageID>
  </soap:Header>
  <soap:Body><wsd:Probe><wsd:Types>{types}</wsd:Types></wsd:Probe></soap:Body>
</soap:Envelope>";
    }
}
