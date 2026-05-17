using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record ArayuzBilgi(string No, string Ad);

internal static class InterfaceDiscoveryService
{
    public static async Task<List<ArayuzBilgi>> TumunuGetirAsync()
    {
        var liste = new List<ArayuzBilgi>();
        var psi = new ProcessStartInfo(Paths.TsharkExe, "-D")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"tshark başlatılamadı: {Paths.TsharkExe}");
        var cikti = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        foreach (var satir in cikti.Split('\n'))
        {
            var t = satir.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            var noktaIdx = t.IndexOf('.');
            if (noktaIdx < 0) continue;
            var no = t[..noktaIdx].Trim();
            var parcaIdx = t.IndexOf('(');
            var ad = parcaIdx >= 0
                ? t[(parcaIdx + 1)..].TrimEnd(')', ' ')
                : t[(noktaIdx + 2)..].Trim();
            if (ad.Length > 32) ad = ad[..29] + "…";
            liste.Add(new ArayuzBilgi(no, ad));
        }
        return liste;
    }

    public static async Task<int> PaketSayisiAsync(string no, int sureSn = 2)
    {
        try
        {
            var psi = new ProcessStartInfo(Paths.TsharkExe, $"-i {no} -a duration:{sureSn} -q")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return 0;
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            foreach (var satir in stderr.Split('\n'))
            {
                var s = satir.Trim();
                if (s.EndsWith("packets captured", StringComparison.OrdinalIgnoreCase))
                {
                    var parca = s.Split(' ')[0];
                    if (int.TryParse(parca, out int n)) return n;
                }
            }
        }
        catch { }
        return 0;
    }
}
