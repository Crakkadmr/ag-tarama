using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgTarama.Services;

internal sealed class HistoryRecord
{
    public string Id { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string Type { get; set; } = "";
    public string Target { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Lines { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

internal static class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static HistoryRecord Kaydet(
        string type,
        string target,
        string summary,
        IEnumerable<string>? lines = null,
        Dictionary<string, string>? metadata = null)
    {
        Directory.CreateDirectory(Paths.HistoryKlasor);
        var now = DateTimeOffset.Now;
        var record = new HistoryRecord
        {
            Id = $"{now:yyyyMMdd_HHmmss_fff}_{TemizDosyaParcasi(type)}",
            CreatedAt = now,
            Type = type,
            Target = target,
            Summary = summary,
            Lines = lines?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>(),
            Metadata = metadata ?? new Dictionary<string, string>(),
        };

        var path = KayitYolu(record.Id);
        File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    public static List<HistoryRecord> SonKayitlariYukle(int limit = 100)
    {
        Directory.CreateDirectory(Paths.HistoryKlasor);
        return Directory.EnumerateFiles(Paths.HistoryKlasor, "*.json")
            .Select(Yukle)
            .OfType<HistoryRecord>()
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public static HistoryRecord? Yukle(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<HistoryRecord>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string KayitYolu(string id)
        => Path.Combine(Paths.HistoryKlasor, id.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? id : $"{id}.json");

    private static string TemizDosyaParcasi(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var temiz = new string(text.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray());
        return temiz.Length == 0 ? "kayit" : temiz;
    }
}
