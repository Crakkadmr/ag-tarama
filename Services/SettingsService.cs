using System;
using System.IO;
using System.Text.Json;

namespace AgTarama.Services;

public static class SettingsService
{
    private static readonly string DosyaYolu = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama", "settings.json");

    public static AppSettings Yukle()
    {
        try
        {
            if (File.Exists(DosyaYolu))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(DosyaYolu)) ?? new();
        }
        catch { }
        return new AppSettings();
    }

    public static void Kaydet(AppSettings ayarlar)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu)!);
        File.WriteAllText(DosyaYolu,
            JsonSerializer.Serialize(ayarlar, new JsonSerializerOptions { WriteIndented = true }));
    }
}
