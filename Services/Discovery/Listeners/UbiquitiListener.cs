using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

// Ubiquiti Discovery Protocol — delegates to existing UbiquitiDiscoveryService
internal sealed class UbiquitiListener : IListener
{
    public string Name => "Ubiquiti";

    public async Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token)
    {
        try
        {
            var kayitlar = await Services.UbiquitiDiscoveryService.TaraAsync(subnetPrefix, token)
                .ConfigureAwait(false);

            foreach (var k in kayitlar)
            {
                if (token.IsCancellationRequested) break;
                var bilgi = store.GetOrAdd(k.Ip);
                if (k.Mac != null) bilgi.MacAdresi ??= MacUtils.Normalize(k.Mac);
                bilgi.UbntPlatform = k.Platform ?? k.ModelKodu;
                bilgi.UbntFirmware = k.Firmware;
                bilgi.UbntHostname = k.Hostname;
                bilgi.Uretici ??= "Ubiquiti";
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("Ubiquiti");
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }
}
