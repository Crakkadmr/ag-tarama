using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

// SSH Banner — reads first line of SSH connection (e.g. "SSH-2.0-OpenSSH_8.4 Ubuntu")
internal sealed class SshBannerProbe : IProbe
{
    public string Name => "SSH-Banner";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);
        int count = Math.Max(0, hostEnd - hostStart + 1);

        var tasks = Enumerable.Range(hostStart, count).Select(i => Task.Run(async () =>
        {
            var ip = $"{subnetPrefix}.{i}";
            if (!store.TryGet(ip, out var bilgi) || bilgi == null) return;
            bool has22;
            lock (bilgi.AcikPortlar) has22 = bilgi.AcikPortlar.Contains(22);
            if (!has22) return;

            await sem.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var banner = await ReadBannerAsync(ip, token).ConfigureAwait(false);
                if (banner == null) return;
                bilgi.SshBanner = banner;

                // OS detection from SSH banner
                var os = DetectOsFromBanner(banner);
                if (!string.IsNullOrWhiteSpace(os) && string.IsNullOrWhiteSpace(bilgi.Os))
                    bilgi.Os = os;

                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("SSH");
                store.NotifyChanged(bilgi);
            }
            catch { }
            finally { sem.Release(); }
        }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<string?> ReadBannerAsync(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 22, cts.Token).ConfigureAwait(false);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = 2000;
            var buf = new byte[256];
            int n = await stream.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            var line = Encoding.ASCII.GetString(buf, 0, n)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            return line?.Trim();
        }
        catch { return null; }
    }

    private static string? DetectOsFromBanner(string banner)
    {
        var b = banner.ToLowerInvariant();
        if (b.Contains("ubuntu"))    return "Linux (Ubuntu)";
        if (b.Contains("debian"))    return "Linux (Debian)";
        if (b.Contains("raspbian")) return "Linux (Raspbian)";
        if (b.Contains("centos"))    return "Linux (CentOS)";
        if (b.Contains("fedora"))    return "Linux (Fedora)";
        if (b.Contains("openbsd"))   return "OpenBSD";
        if (b.Contains("freebsd"))   return "FreeBSD";
        if (b.Contains("routeros") || b.Contains("mikrotik")) return "RouterOS";
        if (b.Contains("cisco"))     return "Cisco IOS";
        if (b.Contains("dropbear"))  return "Linux (Embedded)";
        if (b.Contains("openssh") || b.Contains("ssh-2.0")) return "Linux";
        return null;
    }
}
