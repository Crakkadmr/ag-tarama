using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace AgTarama.Services;

public record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes, long SizeBytes);

public static class UpdateService
{
    private const string Owner = "Crakkadmr";
    private const string Repo  = "ag-tarama";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.Clear();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("AgTarama-Updater/1.0");

            var url  = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var json = await Http.GetStringAsync(url);

            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            var tag       = root.GetProperty("tag_name").GetString() ?? "";
            var remoteVer = tag.TrimStart('v');

            if (!IsNewer(remoteVer, CurrentVersion)) return null;

            var notes = root.TryGetProperty("body", out var bodyProp)
                ? (bodyProp.GetString() ?? "")
                : "";

            if (!root.TryGetProperty("assets", out var assets)) return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                var dlUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                var size  = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;

                return new UpdateInfo(remoteVer, dlUrl, notes.Trim(), size);
            }

            return null;
        }
        catch
        {
            return null; // Ağ hatası — sessizce geç
        }
    }

    public static async Task DownloadAsync(
        string url, string destPath,
        IProgress<int> progress, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destPath);

        var buffer     = new byte[81920];
        long downloaded = 0;
        int  read;

        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0) progress.Report((int)(downloaded * 100 / total));
        }
        progress.Report(100);
    }

    // ZIP'i çıkart, PowerShell güncelleme betiği oluştur ve çalıştır, ardından uygulamayı kapat.
    public static void ExtractAndRestart(string zipPath)
    {
        var appDir    = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        var appExe    = Process.GetCurrentProcess().MainModule!.FileName;
        var updateDir = Path.Combine(Path.GetTempPath(), "AgTaramaUpdate");
        var extractTo = Path.Combine(updateDir, "extracted");

        if (Directory.Exists(extractTo)) Directory.Delete(extractTo, true);
        ZipFile.ExtractToDirectory(zipPath, extractTo);

        // Zip içinde tek kök klasör varsa onu kaynak yap
        var entries = Directory.GetFileSystemEntries(extractTo);
        var srcDir  = entries.Length == 1 && Directory.Exists(entries[0])
            ? entries[0]
            : extractTo;

        string Q(string s) => s.Replace("'", "''"); // PowerShell string escape

        var sb = new StringBuilder();
        sb.AppendLine("Start-Sleep -Seconds 2");
        sb.AppendLine("$src = '" + Q(srcDir) + "\\'");
        sb.AppendLine("$dst = '" + Q(appDir) + "\\'");
        sb.AppendLine("Get-ChildItem -Path $src -Recurse -File | ForEach-Object {");
        sb.AppendLine("    $rel    = $_.FullName.Substring($src.Length)");
        sb.AppendLine("    $target = Join-Path $dst $rel");
        sb.AppendLine("    $dir    = Split-Path $target");
        sb.AppendLine("    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }");
        sb.AppendLine("    Copy-Item -Path $_.FullName -Destination $target -Force");
        sb.AppendLine("}");
        sb.AppendLine("Start-Process -FilePath '" + Q(appExe) + "'");
        sb.AppendLine("Start-Sleep -Seconds 1");
        sb.AppendLine("Remove-Item -Path '" + Q(updateDir) + "' -Recurse -Force -ErrorAction SilentlyContinue");
        var script = sb.ToString();

        var scriptPath = Path.Combine(updateDir, "update.ps1");
        Directory.CreateDirectory(updateDir);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo
        {
            FileName  = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false
        });

        Application.Current.Shutdown();
    }

    private static bool IsNewer(string remote, string current)
    {
        if (Version.TryParse(remote, out var r) && Version.TryParse(current, out var c))
            return r > c;
        return false;
    }
}
