using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class SnmpProbe : IProbe
{
    public string Name => "SNMP";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);
        int count = Math.Max(0, hostEnd - hostStart + 1);

        var tasks = Enumerable.Range(hostStart, count).Select(i => Task.Run(async () =>
        {
            await sem.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var ip = $"{subnetPrefix}.{i}";
                var sysDescr = await Services.SnmpFingerprintService.SysDescrAsync(ip, token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(sysDescr)) return;

                var bilgi = store.GetOrAdd(ip);
                bilgi.SnmpSysDescr = sysDescr;
                var sysName = await Services.SnmpFingerprintService.SysNameAsync(ip, token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(sysName)) bilgi.SnmpSysName = sysName;
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("SNMP");
                store.NotifyChanged(bilgi);
            }
            catch { }
            finally { sem.Release(); }
        }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
