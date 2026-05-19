using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class TcpPortProbe : IProbe
{
    public string Name => "TCP-Port";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(options.ConcurrencyLimit);
        int count = Math.Max(0, hostEnd - hostStart + 1);

        var tasks = Enumerable.Range(hostStart, count).Select(i => Task.Run(async () =>
        {
            var ip = $"{subnetPrefix}.{i}";
            var acik = new List<int>();

            foreach (var port in options.Ports)
            {
                if (token.IsCancellationRequested) break;
                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts2.CancelAfter(options.PortTimeoutMs);
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(ip, port, cts2.Token).ConfigureAwait(false);
                    acik.Add(port);
                }
                catch { }
                finally { sem.Release(); }
            }

            if (acik.Count > 0)
            {
                var bilgi = store.GetOrAdd(ip);
                lock (bilgi.AcikPortlar)
                    bilgi.AcikPortlar.AddRange(acik.Except(bilgi.AcikPortlar));
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("Port");
                store.NotifyChanged(bilgi);
            }
        }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
