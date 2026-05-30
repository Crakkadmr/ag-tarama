using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

// MikroTik Neighbor Discovery Protocol — delegates to existing MndpDiscoveryService
internal sealed class MndpListener : IListener
{
    public string Name => "MNDP";

    public async Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token)
    {
        try
        {
            var kayitlar = await Services.MndpDiscoveryService.TaraAsync(subnetPrefix, token)
                .ConfigureAwait(false);

            foreach (var k in kayitlar)
            {
                if (token.IsCancellationRequested) break;
                var bilgi = store.GetOrAdd(k.Ip);
                if (k.Mac != null) bilgi.MacAdresi ??= MacUtils.Normalize(k.Mac);
                bilgi.MikroTikBoard    = k.Board;
                bilgi.MikroTikVersion  = k.Version;
                bilgi.MikroTikIdentity = k.Identity;
                bilgi.Uretici ??= "MikroTik";
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("MNDP");
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }
}
