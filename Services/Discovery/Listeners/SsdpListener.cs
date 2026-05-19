using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

internal sealed class SsdpListener : IListener
{
    public string Name => "SSDP";

    private static readonly byte[] MSearchBytes = Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 3\r\nST: ssdp:all\r\n\r\n");

    public async Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token)
    {
        try
        {
            using var udp = new UdpClient();
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            var ssdpEp = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
            await udp.SendAsync(MSearchBytes, MSearchBytes.Length, ssdpEp).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await udp.ReceiveAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                var srcIp = res.RemoteEndPoint.Address.ToString();
                if (!srcIp.StartsWith(subnetPrefix + ".", StringComparison.Ordinal)) continue;

                var resp = Encoding.UTF8.GetString(res.Buffer);
                var bilgi = store.GetOrAdd(srcIp);
                bilgi.SsdpBulundu = true;
                bilgi.Online      = true;

                var headers = ParseHeaders(resp);
                if (headers.TryGetValue("SERVER", out var srv)) bilgi.SsdpSunucu = srv;

                if (headers.TryGetValue("LOCATION", out var loc))
                {
                    bilgi.SsdpLocation = loc.Trim();
                    _ = Task.Run(async () =>
                    {
                        var det = await FetchSsdpDetails(bilgi.SsdpLocation, token).ConfigureAwait(false);
                        bilgi.SsdpFriendlyName = det.FriendlyName ?? bilgi.SsdpFriendlyName;
                        bilgi.SsdpManufacturer = det.Manufacturer ?? bilgi.SsdpManufacturer;
                        bilgi.SsdpModelName    = det.ModelName ?? bilgi.SsdpModelName;
                        bilgi.SsdpModelNumber  = det.ModelNumber ?? bilgi.SsdpModelNumber;
                        store.NotifyChanged(bilgi);
                    }, token);
                }

                bilgi.KesifKaynaklari.Add("SSDP");
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }

    private static async Task<(string? FriendlyName, string? Manufacturer, string? ModelName, string? ModelNumber)>
        FetchSsdpDetails(string? location, CancellationToken token)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out _)) return default;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2000);
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(2000) };
            var xml = await client.GetStringAsync(location, cts.Token).ConfigureAwait(false);
            return (
                XmlTag(xml, "friendlyName"),
                XmlTag(xml, "manufacturer"),
                XmlTag(xml, "modelName"),
                XmlTag(xml, "modelNumber"));
        }
        catch { return default; }
    }

    private static string? XmlTag(string xml, string tag)
    {
        var m = Regex.Match(xml, $@"<{Regex.Escape(tag)}[^>]*>(?<v>.*?)</{Regex.Escape(tag)}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups["v"].Value.Trim() : null;
    }

    private static System.Collections.Generic.Dictionary<string, string> ParseHeaders(string resp)
    {
        var d = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in resp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var idx = line.IndexOf(':');
            if (idx > 0) d[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return d;
    }
}
