using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record AdvancedIpScannerKaydi(
    string Ip,
    string? Ad,
    string? Mac,
    string? Uretici,
    string? Servisler);

internal static class AdvancedIpScannerService
{
    public static async Task<IReadOnlyList<AdvancedIpScannerKaydi>> TaraAsync(string subnet, CancellationToken token, int timeoutMs = 30000)
    {
        if (!File.Exists(Paths.IpScannerConsoleExe)) return Array.Empty<AdvancedIpScannerKaydi>();

        var outFile = Path.Combine(Path.GetTempPath(), $"agtarama_ais_{Guid.NewGuid():N}.txt");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = Paths.IpScannerConsoleExe,
                    Arguments              = $"/r:{subnet}.1-{subnet}.254 /f:\"{outFile}\" /v2",
                    WorkingDirectory       = Path.GetDirectoryName(Paths.IpScannerConsoleExe) ?? Paths.AppBase,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                },
            };

            process.Start();
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }

            if (!File.Exists(outFile)) return Array.Empty<AdvancedIpScannerKaydi>();
            var text = await File.ReadAllTextAsync(outFile, token).ConfigureAwait(false);
            return Parse(text);
        }
        catch
        {
            return Array.Empty<AdvancedIpScannerKaydi>();
        }
        finally
        {
            try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
        }
    }

    internal static IReadOnlyList<AdvancedIpScannerKaydi> Parse(string text)
    {
        var kayitlar = new List<AdvancedIpScannerKaydi>();
        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || !Regex.IsMatch(line, @"\b\d{1,3}(?:\.\d{1,3}){3}\b")) continue;
            if (line.StartsWith("Status", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("IP ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Usage", StringComparison.OrdinalIgnoreCase))
                continue;

            var ip = Regex.Match(line, @"\b(?<ip>\d{1,3}(?:\.\d{1,3}){3})\b").Groups["ip"].Value;
            var macMatch = Regex.Match(line, @"\b(?<mac>[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5})\b");
            var mac = macMatch.Success ? macMatch.Groups["mac"].Value.ToUpperInvariant().Replace('-', ':') : null;

            string? ad = null;
            string? uretici = null;
            string? servisler = null;

            var parts = line.Split(new[] { '\t', ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                ad = IlkAnlamli(parts, ip, mac);
                uretici = parts.FirstOrDefault(p => p.Contains("Inc", StringComparison.OrdinalIgnoreCase) ||
                                                    p.Contains("Ltd", StringComparison.OrdinalIgnoreCase) ||
                                                    p.Contains("Corp", StringComparison.OrdinalIgnoreCase) ||
                                                    p.Contains("Co.", StringComparison.OrdinalIgnoreCase));
                servisler = string.Join(", ", parts.Where(p => Regex.IsMatch(p, @"\b(HTTP|HTTPS|FTP|SSH|RDP|SMB|RTSP|ONVIF|Telnet)\b", RegexOptions.IgnoreCase)));
            }

            kayitlar.Add(new AdvancedIpScannerKaydi(ip, Temiz(ad), mac, Temiz(uretici), Temiz(servisler)));
        }

        return kayitlar
            .GroupBy(k => k.Ip, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();
    }

    private static string? IlkAnlamli(IEnumerable<string> parts, string ip, string? mac)
    {
        foreach (var part in parts)
        {
            var temiz = Temiz(part);
            if (temiz is null) continue;
            if (temiz.Equals(ip, StringComparison.OrdinalIgnoreCase)) continue;
            if (mac != null && temiz.Equals(mac, StringComparison.OrdinalIgnoreCase)) continue;
            if (Regex.IsMatch(temiz, @"^\d{1,3}(?:\.\d{1,3}){3}$")) continue;
            if (Regex.IsMatch(temiz, @"^[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5}$")) continue;
            if (temiz.Equals("Alive", StringComparison.OrdinalIgnoreCase)) continue;
            if (temiz.Equals("Dead", StringComparison.OrdinalIgnoreCase)) continue;
            return temiz;
        }
        return null;
    }

    private static string? Temiz(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var temiz = Regex.Replace(value.Trim(), @"\s+", " ");
        return temiz.Length == 0 ? null : temiz;
    }
}
