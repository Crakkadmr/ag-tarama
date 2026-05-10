using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal static class PortScanService
{
    public static int[] Parse(string giris)
    {
        var portlar = new HashSet<int>();
        foreach (var parca in giris.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = parca.Trim();
            if (t.Contains('-'))
            {
                var b = t.Split('-');
                if (b.Length == 2 &&
                    int.TryParse(b[0], out int bas) &&
                    int.TryParse(b[1], out int son))
                {
                    for (int p = Math.Clamp(bas, 1, 65535);
                             p <= Math.Clamp(son, 1, 65535); p++)
                        portlar.Add(p);
                }
            }
            else if (int.TryParse(t, out int p) && p is >= 1 and <= 65535)
                portlar.Add(p);
        }
        return portlar.OrderBy(x => x).ToArray();
    }

    public static async Task<int> TaraAsync(
        string hedef,
        int[] portlar,
        Func<int, Task> portAcikCallback,
        CancellationToken token,
        int eszamanli = 50,
        int timeoutMs = 1000)
    {
        int acik = 0;
        var semaphore = new SemaphoreSlim(eszamanli);

        var gorevler = portlar.Select(async port =>
        {
            if (token.IsCancellationRequested) return;
            bool acquired = false;
            try
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                acquired = true;
                using var client = new TcpClient();
                var baglanti = client.ConnectAsync(hedef, port);
                var bitti    = await Task.WhenAny(baglanti, Task.Delay(timeoutMs, token))
                                          .ConfigureAwait(false);
                if (bitti == baglanti && baglanti.IsCompletedSuccessfully)
                {
                    Interlocked.Increment(ref acik);
                    await portAcikCallback(port).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally { if (acquired) semaphore.Release(); }
        });

        await Task.WhenAll(gorevler);
        return acik;
    }
}
