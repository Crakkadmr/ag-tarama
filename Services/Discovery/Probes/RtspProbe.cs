using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class RtspProbe : IProbe
{
    public string Name => "RTSP";

    // RTSP OPTIONS isteği — kimlik doğrulama gerektirmez, Server header döner.
    private const string RtspOptionsIstek =
        "OPTIONS * RTSP/1.0\r\n" +
        "CSeq: 1\r\n" +
        "User-Agent: NetSniffer/1.0\r\n\r\n";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);

        var tasks = Enumerable.Range(hostStart, Math.Max(0, hostEnd - hostStart + 1))
            .Select(i => Task.Run(async () =>
            {
                var ip = $"{subnetPrefix}.{i}";
                if (!store.TryGet(ip, out var bilgi)) return;
                bool port554Acik;
                lock (bilgi!.AcikPortlar) port554Acik = bilgi.AcikPortlar.Contains(554);
                if (!port554Acik) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var yanit = await RtspOptionsGonder(ip, token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(yanit) || !RtspYanitiMi(yanit)) return;

                    var sunucu = AyrıştırSunucu(yanit);
                    if (sunucu != null) bilgi.RtspServerHeader = sunucu;
                    bilgi.RtspDurum = "OK";
                    bilgi.KesifKaynaklari.Add("RTSP");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<string?> RtspOptionsGonder(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 554, cts.Token).ConfigureAwait(false);
            using var ns = tcp.GetStream();
            var istek = Encoding.ASCII.GetBytes(RtspOptionsIstek);
            await ns.WriteAsync(istek, cts.Token).ConfigureAwait(false);
            var buf = new byte[1024];
            var read = await ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            return read > 0 ? Encoding.ASCII.GetString(buf, 0, read) : null;
        }
        catch { return null; }
    }

    // "Server: <value>" satırını bul ve değeri döndür.
    internal static string? AyrıştırSunucu(string yanit)
    {
        foreach (var satir in yanit.Split('\n'))
        {
            if (satir.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))
                return satir.Substring(7).Trim('\r', '\n', ' ');
        }
        return null;
    }

    // Yanıt gerçekten RTSP cihazından mı geliyor?
    internal static bool RtspYanitiMi(string yanit)
        => yanit.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase);
}
