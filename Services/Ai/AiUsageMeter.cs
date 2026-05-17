using System.IO;
using System.Text.Json;

namespace AgTarama.Services.Ai;

public sealed class AiUsageSnapshot
{
    public DateTime DayUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime MonthUtc { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    public int DailyInputTokens { get; set; }
    public int DailyOutputTokens { get; set; }
    public int MonthlyInputTokens { get; set; }
    public int MonthlyOutputTokens { get; set; }
}

public static class AiUsageMeter
{
    private static readonly string UsageFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama",
        "ai.usage.json");

    public static AiUsageSnapshot Load()
    {
        try
        {
            if (File.Exists(UsageFile))
            {
                var data = JsonSerializer.Deserialize<AiUsageSnapshot>(File.ReadAllText(UsageFile));
                if (data is not null)
                    return NormalizePeriods(data);
            }
        }
        catch (Exception ex)
        {
            LogService.Hata("AiUsageMeter.Load", ex);
        }

        return new AiUsageSnapshot();
    }

    public static void AddUsage(int inputTokens, int outputTokens)
    {
        try
        {
            var snapshot = Load();
            snapshot.DailyInputTokens += Math.Max(0, inputTokens);
            snapshot.DailyOutputTokens += Math.Max(0, outputTokens);
            snapshot.MonthlyInputTokens += Math.Max(0, inputTokens);
            snapshot.MonthlyOutputTokens += Math.Max(0, outputTokens);
            Save(snapshot);
        }
        catch (Exception ex)
        {
            LogService.Hata("AiUsageMeter.AddUsage", ex);
        }
    }

    private static void Save(AiUsageSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UsageFile)!);
        File.WriteAllText(
            UsageFile,
            JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static AiUsageSnapshot NormalizePeriods(AiUsageSnapshot snapshot)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (snapshot.DayUtc.Date != today)
        {
            snapshot.DayUtc = today;
            snapshot.DailyInputTokens = 0;
            snapshot.DailyOutputTokens = 0;
        }

        if (snapshot.MonthUtc.Year != monthStart.Year || snapshot.MonthUtc.Month != monthStart.Month)
        {
            snapshot.MonthUtc = monthStart;
            snapshot.MonthlyInputTokens = 0;
            snapshot.MonthlyOutputTokens = 0;
        }

        return snapshot;
    }
}
