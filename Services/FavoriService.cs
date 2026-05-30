using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace AgTarama.Services;

public static class FavoriService
{
    private static readonly string DosyaYolu = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama", "favorites.json");

    public static List<string> YukleHepsi()
    {
        try
        {
            if (File.Exists(DosyaYolu))
                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(DosyaYolu)) ?? [];
        }
        catch { }
        return [];
    }

    public static void Kaydet(List<string> favoriler)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu)!);
        File.WriteAllText(DosyaYolu,
            JsonSerializer.Serialize(favoriler, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Returns false if already exists (case-insensitive + IP normalize)
    public static bool Ekle(string ip)
    {
        var normalize = Normalize(ip);
        var liste = YukleHepsi();
        if (liste.Any(x => string.Equals(Normalize(x), normalize, StringComparison.OrdinalIgnoreCase)))
            return false;
        liste.Add(normalize);
        Kaydet(liste);
        return true;
    }

    public static void Sil(string ip)
    {
        var normalize = Normalize(ip);
        var liste = YukleHepsi();
        var kalan = liste.Where(x => !string.Equals(Normalize(x), normalize, StringComparison.OrdinalIgnoreCase)).ToList();
        if (kalan.Count != liste.Count) Kaydet(kalan);
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var t = s.Trim();
        // "192.168.001.1" → "192.168.1.1"
        if (IPAddress.TryParse(t, out var ip)) return ip.ToString();
        return t;
    }
}
