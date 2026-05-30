using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class NetbiosProbe : IProbe
{
    public string Name => "NetBIOS";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(16);
        var denenenler = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        int count = Math.Max(0, hostEnd - hostStart + 1);

        var tasks = Enumerable.Range(hostStart, count).Select(i => Task.Run(async () =>
        {
            var ip = $"{subnetPrefix}.{i}";
            if (!denenenler.TryAdd(ip, 0)) return;
            await sem.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var nb = await Services.NetbiosService.SorgulaAsync(ip, token).ConfigureAwait(false);
                if (nb is null) return;
                var bilgi = store.GetOrAdd(ip);
                bilgi.NetbiosCihazAdi = nb.NetbiosAdi;
                bilgi.NetbiosGrupAdi  = nb.GrupAdi;
                bilgi.DnsAdi          = bilgi.DnsAdi ?? nb.DnsAdi;
                bilgi.PingAdi         = bilgi.PingAdi ?? nb.PingAdi;
                bilgi.Online          = true;
                bilgi.KesifKaynaklari.Add("NetBIOS");
                store.NotifyChanged(bilgi);
            }
            catch { }
            finally { sem.Release(); }
        }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
