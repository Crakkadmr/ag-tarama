using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AgTarama.Services;

public sealed class WlanSonuc
{
    public string Ssid { get; set; } = "";
    public string Bssid { get; set; } = "";
    public string Auth { get; set; } = "";
    public string Encryption { get; set; } = "";
    public int Signal { get; set; }
    public int Channel { get; set; }
    public string RadioType { get; set; } = "";
    public string Band { get; set; } = "";
    public string Oui { get; set; } = "";
    public bool CokluAp { get; set; }
    public bool SupheliEvilTwin { get; set; }
    public List<string> SupheNedenleri { get; } = new();
}

public static class WlanService
{
    public static async Task<List<WlanSonuc>> ScanAsync(CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("netsh", "wlan show networks mode=bssid")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        var sonuclar = Parse(output);
        Isaretle(sonuclar);
        return sonuclar;
    }

    private static List<WlanSonuc> Parse(string output)
    {
        var list = new List<WlanSonuc>();
        WlanSonuc? cur = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                if (cur != null) list.Add(cur);
                cur = new WlanSonuc { Ssid = AfterColon(line) };
                continue;
            }

            if (cur == null) continue;

            if (line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(cur.Bssid))
                {
                    list.Add(cur);
                    cur = new WlanSonuc
                    {
                        Ssid = cur.Ssid,
                        Auth = cur.Auth,
                        Encryption = cur.Encryption,
                        RadioType = cur.RadioType,
                        Band = cur.Band,
                    };
                }

                cur.Bssid = AfterColon(line);
                cur.Oui = OuiPrefix(cur.Bssid);
            }
            else if (StartsWithAny(line, "Authentication", "Kimlik dogrulamasi", "Kimlik doğrulaması"))
            {
                cur.Auth = AfterColon(line);
            }
            else if (StartsWithAny(line, "Encryption", "Sifreleme", "Şifreleme"))
            {
                cur.Encryption = AfterColon(line);
            }
            else if (StartsWithAny(line, "Signal", "Sinyal"))
            {
                var s = AfterColon(line).Replace("%", "").Trim();
                if (int.TryParse(s, out var pct)) cur.Signal = pct;
            }
            else if (StartsWithAny(line, "Channel", "Kanal"))
            {
                var s = AfterColon(line).Trim();
                if (int.TryParse(s, out var ch)) cur.Channel = ch;
            }
            else if (StartsWithAny(line, "Radio type", "Radyo turu", "Radyo türü"))
            {
                cur.RadioType = AfterColon(line);
            }
            else if (StartsWithAny(line, "Band", "Bant"))
            {
                cur.Band = AfterColon(line);
            }
        }

        if (cur != null && !string.IsNullOrEmpty(cur.Ssid))
            list.Add(cur);

        return list;
    }

    private static void Isaretle(List<WlanSonuc> list)
    {
        var gruplar = new Dictionary<string, List<WlanSonuc>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in list)
        {
            if (!gruplar.TryGetValue(s.Ssid, out var g))
                gruplar[s.Ssid] = g = new List<WlanSonuc>();
            g.Add(s);
        }

        foreach (var g in gruplar.Values)
        {
            if (g.Count < 2) continue;

            foreach (var s in g)
                s.CokluAp = true;

            var oiuSet = g.Select(x => x.Oui).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (oiuSet.Count > 1)
                SupheEkle(g, "Ayni SSID altinda farkli OUI");

            var sifrelemeSet = g.Select(x => Normalize(x.Encryption)).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (sifrelemeSet.Count > 1)
                SupheEkle(g, "Ayni SSID altinda farkli sifreleme");
        }
    }

    private static void SupheEkle(IEnumerable<WlanSonuc> grup, string neden)
    {
        foreach (var s in grup)
        {
            s.SupheliEvilTwin = true;
            if (!s.SupheNedenleri.Contains(neden, StringComparer.OrdinalIgnoreCase))
                s.SupheNedenleri.Add(neden);
        }
    }

    private static string AfterColon(string line)
    {
        var idx = line.IndexOf(':');
        return idx >= 0 ? line[(idx + 1)..].Trim() : "";
    }

    private static bool StartsWithAny(string line, params string[] prefixes)
        => prefixes.Any(p => line.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static string OuiPrefix(string? bssid)
    {
        if (string.IsNullOrWhiteSpace(bssid)) return "";
        var hex = Regex.Replace(bssid, "[^0-9A-Fa-f]", "").ToUpperInvariant();
        return hex.Length >= 6 ? hex[..6] : "";
    }

    private static string Normalize(string value)
        => Regex.Replace(value ?? "", "\\s+", " ").Trim();

    public static async Task<bool> WifiAdaptorVarMiAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi)!;
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var output = await outputTask;
            return output.Contains("Name", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("There are 0 interfaces", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
