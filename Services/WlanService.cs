using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

public sealed class WlanSonuc
{
    public string Ssid        { get; set; } = "";
    public string Bssid       { get; set; } = "";
    public string Auth        { get; set; } = "";   // WPA2-Personal, WPA3-Personal, WEP, Open …
    public string Encryption  { get; set; } = "";   // CCMP, WEP, None …
    public int    Signal      { get; set; }          // 0-100 %
    public int    Channel     { get; set; }
    public string RadioType   { get; set; } = "";   // 802.11ax, 802.11ac …
    public bool   EvilTwin    { get; set; }          // aynı SSID, farklı BSSID ile birden fazla görünen
}

public static class WlanService
{
    // netsh wlan show networks mode=bssid çıktısını parse eder
    public static async Task<List<WlanSonuc>> ScanAsync(CancellationToken ct = default)
    {
        var sonuclar = new List<WlanSonuc>();

        var psi = new ProcessStartInfo("netsh", "wlan show networks mode=bssid")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        sonuclar = Parse(output);
        Işaretle(sonuclar);
        return sonuclar;
    }

    // -------------------------------------------------------------------
    private static List<WlanSonuc> Parse(string output)
    {
        var list    = new List<WlanSonuc>();
        WlanSonuc? cur = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                // "SSID 1                 : MyNetwork"  — yeni ağ bloğu başlıyor
                if (cur != null) list.Add(cur);
                cur = new WlanSonuc();
                cur.Ssid = AfterColon(line);
                continue;
            }

            if (cur == null) continue;

            if (line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                // Birden fazla BSSID satırı gelirse yeni WlanSonuc nesnesi oluştur (aynı SSID)
                if (!string.IsNullOrEmpty(cur.Bssid))
                {
                    list.Add(cur);
                    cur = new WlanSonuc
                    {
                        Ssid       = cur.Ssid,
                        Auth       = cur.Auth,
                        Encryption = cur.Encryption,
                        RadioType  = cur.RadioType,
                    };
                }
                cur.Bssid = AfterColon(line);
            }
            else if (StartsWithCI(line, "Authentication"))
            {
                cur.Auth = AfterColon(line);
            }
            else if (StartsWithCI(line, "Encryption"))
            {
                cur.Encryption = AfterColon(line);
            }
            else if (StartsWithCI(line, "Signal"))
            {
                var s = AfterColon(line).Replace("%", "").Trim();
                if (int.TryParse(s, out var pct)) cur.Signal = pct;
            }
            else if (StartsWithCI(line, "Channel"))
            {
                var s = AfterColon(line).Trim();
                if (int.TryParse(s, out var ch)) cur.Channel = ch;
            }
            else if (StartsWithCI(line, "Radio type"))
            {
                cur.RadioType = AfterColon(line);
            }
        }

        if (cur != null && !string.IsNullOrEmpty(cur.Ssid))
            list.Add(cur);

        return list;
    }

    // Evil-Twin: aynı SSID, birden fazla farklı BSSID
    private static void Işaretle(List<WlanSonuc> list)
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
            // farklı BSSID'ler varsa Evil-Twin
            var bssids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in g) bssids.Add(s.Bssid);
            if (bssids.Count > 1)
                foreach (var s in g) s.EvilTwin = true;
        }
    }

    // ─── Yardımcılar ────────────────────────────────────────────────────
    private static string AfterColon(string line)
    {
        var idx = line.IndexOf(':');
        return idx >= 0 ? line[(idx + 1)..].Trim() : "";
    }

    private static bool StartsWithCI(string line, string prefix)
        => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    // Wi-Fi adaptörü var mı?
    public static bool WifiAdaptorVarMi()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            // "There is 1 interface" veya "Name" satırı varsa adaptor mevcut
            return output.Contains("Name", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("There are 0 interfaces", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
