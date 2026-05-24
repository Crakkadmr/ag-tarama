using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class TelnetBannerProbe : IProbe
{
    public string Name => "Telnet";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);

        var tasks = System.Linq.Enumerable.Range(hostStart, Math.Max(0, hostEnd - hostStart + 1))
            .Select(i => System.Threading.Tasks.Task.Run(async () =>
            {
                var ip = $"{subnetPrefix}.{i}";
                if (!store.TryGet(ip, out var bilgi)) return;
                bool port23Acik;
                lock (bilgi!.AcikPortlar) port23Acik = bilgi.AcikPortlar.Contains(23);
                if (!port23Acik) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var banner = await BannerOkuAsync(ip, token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(banner)) return;

                    bilgi.TelnetBanner = banner;
                    bilgi.KesifKaynaklari.Add("Telnet");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<string?> BannerOkuAsync(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 23, cts.Token).ConfigureAwait(false);
            tcp.ReceiveTimeout = 1500;
            using var ns = tcp.GetStream();
            var buf = new byte[512];
            var read = await ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (read <= 0) return null;
            // IAC seçeneklerini (0xFF ile başlayan kontrol dizilerini) filtrele
            var sb = new StringBuilder();
            for (int i = 0; i < read; i++)
            {
                if (buf[i] == 0xFF) { i += 2; continue; }  // IAC + cmd + option
                if (buf[i] >= 0x20 || buf[i] == '\n' || buf[i] == '\r')
                    sb.Append((char)buf[i]);
            }
            return sb.ToString().Trim();
        }
        catch { return null; }
    }

    // Telnet banner'ından marka ve cihaz türü çıkar.
    internal static (string? Marka, string Tur) AyrıştırBanner(string banner)
    {
        var b = banner.ToLowerInvariant();
        if (b.Contains("routeros") || b.Contains("mikrotik") || b.Contains("routerboard"))
            return ("MikroTik", "Router/AP");
        if (b.Contains("cisco ios") || b.Contains("catalyst") || b.Contains("cisco small business"))
            return ("Cisco", "Switch");
        if (b.Contains("procurve") || b.Contains("hp network") || b.Contains("hp procurve"))
            return ("HP", "Switch");
        if (b.Contains("junos") || b.Contains("juniper"))
            return ("Juniper", "Switch");
        if (b.Contains("arubaos") || b.Contains("aruba networks"))
            return ("Aruba", "Switch/AP");
        if (b.Contains("fortigate") || b.Contains("fortinet"))
            return ("Fortinet", "Güvenlik Duvarı");
        if (b.Contains("pfsense"))
            return ("pfSense", "Güvenlik Duvarı");
        if (b.Contains("opnsense"))
            return ("OPNsense", "Güvenlik Duvarı");
        if (b.Contains("openwrt") || b.Contains("luci"))
            return ("OpenWrt", "Router/AP");
        if (b.Contains("dd-wrt"))
            return ("DD-WRT", "Router/AP");
        if (b.Contains("zynos") || b.Contains("zyxel"))
            return ("ZyXEL", "Switch/AP");
        if (b.Contains("busybox"))
            return (null, "Linux IoT");
        return (null, "Router/Switch");
    }
}
