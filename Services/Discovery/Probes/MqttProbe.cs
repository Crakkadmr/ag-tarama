using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class MqttProbe : IProbe
{
    public string Name => "MQTT";

    // Minimal MQTT 3.1.1 CONNECT paketi: boş ClientID, CleanSession=true.
    private static readonly byte[] MqttConnectPaketi =
    {
        0x10,             // Fixed header: CONNECT (packet type 1)
        0x0C,             // Remaining length = 12
        0x00, 0x04,       // Protocol Name length = 4
        0x4D, 0x51, 0x54, 0x54, // "MQTT"
        0x04,             // Protocol level = 4 (v3.1.1)
        0x02,             // Connect flags: CleanSession = 1
        0x00, 0x3C,       // KeepAlive = 60 saniye
        0x00, 0x00,       // ClientID length = 0 (boş)
    };

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
                bool port1883Acik;
                lock (bilgi!.AcikPortlar) port1883Acik = bilgi.AcikPortlar.Contains(1883);
                if (!port1883Acik) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var yanit = await MqttConnectGonder(ip, token).ConfigureAwait(false);
                    if (yanit == null || !ConnackMi(yanit)) return;

                    bilgi.MqttBulundu = true;
                    bilgi.KesifKaynaklari.Add("MQTT");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<byte[]?> MqttConnectGonder(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 1883, cts.Token).ConfigureAwait(false);
            using var ns = tcp.GetStream();
            await ns.WriteAsync(MqttConnectPaketi, cts.Token).ConfigureAwait(false);
            var buf = new byte[16];
            var read = await ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (read < 4) return null;
            var yanit = new byte[read];
            Array.Copy(buf, yanit, read);
            return yanit;
        }
        catch { return null; }
    }

    // MQTT CONNACK fixed header = 0x20, remaining length = 0x02
    internal static bool ConnackMi(byte[] veri)
        => veri.Length >= 4 && veri[0] == 0x20 && veri[1] == 0x02;
}
