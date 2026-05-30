using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class HttpFingerprintProbe : IProbe
{
    public string Name => "HTTP-FP";

    private static readonly int[] HttpPorts = { 80, 8080, 443, 8443 };

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        // HTTP-FP runs on hosts where HTTP port is already open (detected by TcpPortProbe).
        // Re-probe on all hosts in range that have an open HTTP port.
        foreach (var dev in store.All)
        {
            if (token.IsCancellationRequested) break;
            if (!dev.Ip.StartsWith(subnetPrefix + ".")) continue;

            List<int> portlar;
            lock (dev.AcikPortlar) portlar = [..dev.AcikPortlar];
            var httpPort = HttpPorts.FirstOrDefault(p => portlar.Contains(p));
            if (httpPort == 0) continue;

            var fp = await Services.HttpFingerprintService.ProbeAsync(dev.Ip, httpPort, token).ConfigureAwait(false);
            if (fp is null) continue;

            dev.HttpFpMarka = fp.Marka;
            dev.HttpFpTur   = fp.Tur;
            dev.HttpFpModel = fp.Model;
            dev.Online      = true;
            dev.KesifKaynaklari.Add("HTTP-FP");
            store.NotifyChanged(dev);
        }
    }
}
