using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record NetbiosBilgi(string? CihazAdi, string? GrupAdi);

internal static class NetbiosService
{
    public static async Task<NetbiosBilgi?> SorgulaAsync(string ip, CancellationToken token, int timeoutMs = 2500)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "nbtstat",
                    Arguments              = $"-A {ip}",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                },
                EnableRaisingEvents = true,
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
                return null;
            }

            var output = await outputTask;
            var bilgi  = Parse(output);
            return bilgi.CihazAdi is null && bilgi.GrupAdi is null ? null : bilgi;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    internal static NetbiosBilgi Parse(string output)
    {
        string? cihaz = null;
        string? grup  = null;

        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(rawLine, @"^\s*(?<name>.{1,15})\s+<(?<code>[0-9A-Fa-f]{2})>\s+(?<type>\S+)", RegexOptions.CultureInvariant);
            if (!match.Success) continue;

            var name = match.Groups["name"].Value.Trim();
            var code = match.Groups["code"].Value.ToUpperInvariant();
            var type = match.Groups["type"].Value.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(name) || name == "__MSBROWSE__") continue;

            bool group = type.Contains("GROUP") || type.Contains("GRUP");
            if (code == "20" && !group)
                cihaz ??= name;
            else if (code == "00" && group)
                grup ??= name;
            else if (code == "00" && !group)
                cihaz ??= name;
        }

        return new NetbiosBilgi(cihaz, grup);
    }
}
