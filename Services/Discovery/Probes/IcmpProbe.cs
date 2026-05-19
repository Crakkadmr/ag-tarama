using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class IcmpProbe : IProbe
{
    public string Name => "ICMP";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(options.ConcurrencyLimit);
        int count = Math.Max(0, hostEnd - hostStart + 1);
        var tasks = Enumerable.Range(hostStart, count).Select(i => Task.Run(async () =>
        {
            await sem.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var ip = $"{subnetPrefix}.{i}";
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, options.PingTimeoutMs).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                {
                    var bilgi = store.GetOrAdd(ip);
                    bilgi.PingYanit = true;
                    bilgi.PingMs    = (int)reply.RoundtripTime;
                    bilgi.PingTtl   = reply.Options?.Ttl ?? 0;
                    bilgi.Online    = true;
                    bilgi.KesifKaynaklari.Add("Ping");
                    store.NotifyChanged(bilgi);
                }
            }
            catch { }
            finally { sem.Release(); }
        }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
