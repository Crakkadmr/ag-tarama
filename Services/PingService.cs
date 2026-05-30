using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record PingSonuc(int Sira, int Toplam, IPStatus Durum, long RtMs, int Ttl, string? Hata);

internal static class PingService
{
    public static async IAsyncEnumerable<PingSonuc> PingleAsync(
        string hedef,
        int sayi = 4,
        int timeoutMs = 2000,
        int araSn = 700,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        using var ping = new Ping();
        for (int i = 1; i <= sayi && !token.IsCancellationRequested; i++)
        {
            PingSonuc s;
            try
            {
                var y = await ping.SendPingAsync(hedef, timeoutMs);
                s = new PingSonuc(i, sayi, y.Status, y.RoundtripTime, y.Options?.Ttl ?? 0, null);
            }
            catch (PingException px)
            {
                s = new PingSonuc(i, sayi, IPStatus.Unknown, 0, 0,
                    px.InnerException?.Message ?? px.Message);
            }
            catch (Exception ex) when (ex.GetBaseException() is not OperationCanceledException)
            {
                s = new PingSonuc(i, sayi, IPStatus.Unknown, 0, 0, ex.Message);
            }
            yield return s;

            if (i < sayi && !token.IsCancellationRequested)
            {
                try { await Task.Delay(araSn, token); }
                catch (OperationCanceledException) { }
            }
        }
    }
}
