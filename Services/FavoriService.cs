using System;
using System.Collections.Generic;
using System.IO;
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

    // Returns false if already exists
    public static bool Ekle(string ip)
    {
        var liste = YukleHepsi();
        if (liste.Contains(ip)) return false;
        liste.Add(ip);
        Kaydet(liste);
        return true;
    }

    public static void Sil(string ip)
    {
        var liste = YukleHepsi();
        if (liste.Remove(ip)) Kaydet(liste);
    }
}
