using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgTarama;

internal static class LogService
{
    private static string DosyaYolu =>
        Path.Combine(Paths.LogKlasor, $"{DateTime.Now:yyyyMMdd}.log");

    private static void Yaz(string icerik)
    {
        try
        {
            Directory.CreateDirectory(Paths.LogKlasor);
            File.AppendAllText(DosyaYolu, icerik, Encoding.UTF8);
        }
        catch { }
    }

    public static void OturumBaslat() =>
        Yaz($"\n=== OTURUM: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");

    public static void Kaydet(string kategori, string hedef, IEnumerable<string> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\n[{DateTime.Now:HH:mm:ss}] [{kategori}] {hedef}");
        foreach (var s in satirlar) sb.AppendLine($"  {s}");
        Yaz(sb.ToString());
    }

    public static void Hata(string baglam, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\n[{DateTime.Now:HH:mm:ss}] [HATA] {baglam}");
        if (ex != null)
        {
            sb.AppendLine($"  {ex.GetType().Name}: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
                sb.AppendLine(ex.StackTrace);
        }
        Yaz(sb.ToString());
    }
}
