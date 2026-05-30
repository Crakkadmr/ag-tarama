using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Ai;

public sealed record CihazDto(
    string Ip,
    string Ad,
    string Tur,
    string Marka,
    string Model,
    string Ping,
    string Portlar,
    string Kesif,
    string Mac,
    string Uretici,
    string Servis,
    int    Guven);

public static class AiDeviceAnalyzer
{
    public sealed record Preset(string Etiket, string Ikon, string Talep);

    public static readonly IReadOnlyList<Preset> Presetler =
    [
        new("Güvenlik riski tespiti",    "🛡️",
            "Güvenlik riski taşıyan cihazları işaretle (tehlikeli açık portlar, şifresiz protokol belirtileri, " +
            "güvenlik kamerası/NVR varsayılan port varlığı, telnet/ftp gibi zayıf servisler). " +
            "Her riskli cihaz için IP adresini ve risk nedenini belirt."),

        new("Kamera/NVR/DVR listesi",    "📷",
            "Kamera, NVR ve DVR cihazlarını tespit et. Her biri için IP, marka, model ve tespit kaynağını listele."),

        new("AP/Router/Switch grupla",   "📡",
            "Wi-Fi erişim noktası, router ve switch türündeki cihazları ayrı başlıklar altında gruplandır. " +
            "Her grup için cihazların IP ve markasını yaz."),

        new("Bilinmeyen cihaz sorguları","❓",
            "Tur 'Cihaz' veya 'Bilinmiyor' olan cihazlar için hangi ek tarama/sorgu yapılmalı öner " +
            "(port taraması, SNMP, banner grabbing, web arayüzü vb.). IP bazlı önerileri listele."),

        new("Sonraki tarama önerisi",    "🔍",
            "Her cihaz için en uygun sonraki tarama adımını öner. " +
            "Port taraması, SNMP, ONVIF, web arayüzü, banner grabbing gibi seçenekleri IP bazlı listele."),
    ];

    public static async Task<string> AnalyzeAsync(
        IReadOnlyList<CihazDto> cihazlar,
        string talep,
        AppSettings settings,
        CancellationToken ct = default)
    {
        const int maxCihaz = 50;
        var liste  = cihazlar.Take(maxCihaz).ToList();
        var fazla  = cihazlar.Count - liste.Count;

        var json = JsonSerializer.Serialize(
            liste.Select(c => new
            {
                ip      = c.Ip,
                ad      = c.Ad,
                tur     = c.Tur,
                marka   = c.Marka,
                model   = c.Model,
                ping    = c.Ping,
                portlar = c.Portlar,
                kesif   = c.Kesif,
                mac     = c.Mac,
                uretici = c.Uretici,
                servis  = c.Servis,
                guven   = c.Guven,
            }),
            new JsonSerializerOptions { WriteIndented = true });

        var fazlaNote = fazla > 0 ? $"\n\n(Not: listede {fazla} cihaz daha var, ilk {maxCihaz} gönderildi.)" : "";
        var userPrompt = $"{talep}\n\nCihaz listesi ({liste.Count} cihaz):\n{json}{fazlaNote}";

        return await AiClient.AskAsync(
            settings,
            AiPrompts.CihazSystemPrompt,
            userPrompt,
            ct);
    }
}
