using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Ai;

public static class AiPcapAnalyzer
{
    private static readonly (string ZFlag, string Label)[] _stats =
    [
        ("conv,ip",      "=== IP Konusmalari (Top Talkers) ==="),
        ("io,stat,1",    "=== IO Istatistikleri (saniye bazli) ==="),
        ("io,phs",       "=== Protokol Hiyerarsisi ==="),
        ("endpoints,ip", "=== IP Endpoint Listesi ==="),
        ("http,tree",    "=== HTTP Ozeti ==="),
        ("dns,tree",     "=== DNS Ozeti ==="),
    ];

    // Ozel IP araliglarini eslestir: 10.x.x.x, 172.16-31.x.x, 192.168.x.x
    private static readonly Regex _privateIpRx = new(
        @"\b(10\.\d{1,3}|172\.(1[6-9]|2\d|3[01])|192\.168)\.\d{1,3}\.\d{1,3}\b",
        RegexOptions.Compiled);

    public static async Task<string> AnalyzeAsync(
        string pcapPath,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pcapPath))
            throw new FileNotFoundException("pcap dosyasi bulunamadi.", pcapPath);

        if (!File.Exists(Paths.TsharkExe))
            throw new InvalidOperationException($"tshark bulunamadi: {Paths.TsharkExe}");

        var statsText = await CollectStatsAsync(pcapPath, cancellationToken);

        if (settings.AiYerelIpMaskele)
            statsText = MaskPrivateIps(statsText);

        var userPrompt =
            $"Pcap dosyasi: {Path.GetFileName(pcapPath)}\n\n{statsText}";

        return await AiClient.AskAsync(
            settings,
            AiPrompts.PcapSystemPrompt,
            userPrompt,
            cancellationToken);
    }

    private static async Task<string> CollectStatsAsync(string pcapPath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var (zFlag, label) in _stats)
        {
            ct.ThrowIfCancellationRequested();
            sb.AppendLine(label);
            sb.AppendLine(await RunTsharkStatAsync(pcapPath, zFlag, ct));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static async Task<string> RunTsharkStatAsync(
        string pcapPath,
        string zFlag,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Paths.TsharkExe,
            $"-r \"{pcapPath}\" -q -z {zFlag}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        Process? proc = null;
        try
        {
            proc = Process.Start(psi)
                ?? throw new InvalidOperationException("tshark sureci baslatilamadi.");

            // stdout ve stderr paralel okunmali; sadece stdout okunursa stderr tamponu dolup process bloklanabilir.
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);

            await proc.WaitForExitAsync(ct);

            var stdout = await stdoutTask;
            await stderrTask; // stderr drainlendi, degeri kullanilmiyor

            var lines = stdout.Split('\n');
            if (lines.Length > 50)
                return string.Join('\n', lines[..50])
                    + $"\n... (toplam {lines.Length} satir, ilk 50 gosterildi)";

            return stdout.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Hata($"AiPcapAnalyzer -z {zFlag}", ex);
            return $"(hata: {ex.Message})";
        }
        finally
        {
            if (proc is not null && !proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                await proc.WaitForExitAsync(CancellationToken.None);
            }
            proc?.Dispose();
        }
    }

    private static string MaskPrivateIps(string text)
    {
        return _privateIpRx.Replace(text, m =>
        {
            var parts = m.Value.Split('.');
            return parts.Length == 4
                ? $"{parts[0]}.{parts[1]}.x.{parts[3]}"
                : m.Value;
        });
    }
}
