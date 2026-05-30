using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace AgTarama.Services;

public record UpdateInfo(
    string Version,
    string DownloadUrl,
    string ReleaseNotes,
    long SizeBytes,
    string AssetName,
    string Sha256);

public static class UpdateService
{
    private const string Owner = "Crakkadmr";
    private const string Repo = "ag-tarama";
    private static readonly Regex ZipNamePattern =
        new(@"^AgTarama-v?\d+\.\d+\.\d+(?:-win-x64)?\.zip$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.Clear();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("AgTarama-Updater/1.0");

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var json = await Http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var remoteVer = tag.TrimStart('v');
            if (!IsNewer(remoteVer, CurrentVersion)) return null;

            var notes = root.TryGetProperty("body", out var bodyProp)
                ? (bodyProp.GetString() ?? "")
                : "";

            if (!root.TryGetProperty("assets", out var assets)) return null;

            var zipAsset = assets.EnumerateArray()
                .Select(a => new
                {
                    Name = a.GetProperty("name").GetString() ?? "",
                    Url = a.GetProperty("browser_download_url").GetString() ?? "",
                    Size = a.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0
                })
                .Where(a => ZipNamePattern.IsMatch(a.Name))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (zipAsset is null) return null;

            var shaCandidates = assets.EnumerateArray()
                .Select(a => new
                {
                    Name = a.GetProperty("name").GetString() ?? "",
                    Url = a.GetProperty("browser_download_url").GetString() ?? ""
                })
                .Where(a =>
                    a.Name.Equals($"{zipAsset.Name}.sha256", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals($"{zipAsset.Name}.sha256.txt", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (shaCandidates.Count == 0) return null;

            var shaText = await Http.GetStringAsync(shaCandidates[0].Url);
            var sha256 = ParseSha256Text(shaText);
            if (sha256 is null) return null;

            return new UpdateInfo(
                remoteVer,
                zipAsset.Url,
                notes.Trim(),
                zipAsset.Size,
                zipAsset.Name,
                sha256);
        }
        catch (Exception ex)
        {
            LogService.Hata("UpdateService.CheckForUpdateAsync", ex);
            return null;
        }
    }

    public static async Task DownloadAsync(
        string url,
        string destPath,
        IProgress<int> progress,
        CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0) progress.Report((int)(downloaded * 100 / total));
        }

        progress.Report(100);
    }

    public static bool VerifyHash(string filePath, string expectedSha256)
    {
        if (!File.Exists(filePath)) return false;
        if (string.IsNullOrWhiteSpace(expectedSha256)) return false;

        var expected = NormalizeHex(expectedSha256);
        if (expected.Length != 64) return false;

        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual),
            Encoding.ASCII.GetBytes(expected));
    }

    // Extracts ZIP, validates signed executable (optional thumbprint pin), then launches updater script.
    public static void ExtractAndRestart(string zipPath)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        var appExe = Process.GetCurrentProcess().MainModule!.FileName;
        var appExeName = Path.GetFileName(appExe);
        var updateDir = Path.Combine(Path.GetTempPath(), "AgTaramaUpdate");
        var extractTo = Path.Combine(updateDir, "extracted");

        if (Directory.Exists(extractTo)) Directory.Delete(extractTo, true);
        SafeExtractZip(zipPath, extractTo);

        var entries = Directory.GetFileSystemEntries(extractTo);
        var srcDir = entries.Length == 1 && Directory.Exists(entries[0]) ? entries[0] : extractTo;

        var candidateExe = Path.Combine(srcDir, appExeName);
        if (!File.Exists(candidateExe))
            throw new InvalidDataException($"Update package does not contain {appExeName}.");

        if (!VerifySignerThumbprint(candidateExe, out var signatureError))
            throw new InvalidDataException(signatureError);

        string Q(string s) => s.Replace("'", "''");

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
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false
        });

        Application.Current.Shutdown();
    }

    // Hardened ZIP extraction: validates each entry path stays inside extractTo
    // (defends against ZipSlip / path traversal). Limits entry count and total size.
    private const int MaxZipEntries = 5000;
    private const long MaxZipUncompressedSize = 500L * 1024 * 1024; // 500 MB
    private const long MaxSingleEntrySize = 200L * 1024 * 1024;     // 200 MB

    private static void SafeExtractZip(string zipPath, string extractTo)
    {
        Directory.CreateDirectory(extractTo);
        var baseFull = Path.GetFullPath(extractTo);
        if (!baseFull.EndsWith(Path.DirectorySeparatorChar))
            baseFull += Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxZipEntries)
            throw new InvalidDataException($"Update ZIP contains too many entries ({archive.Entries.Count} > {MaxZipEntries}).");

        long totalSize = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxSingleEntrySize)
                throw new InvalidDataException($"Update entry '{entry.FullName}' exceeds size limit.");
            totalSize += entry.Length;
            if (totalSize > MaxZipUncompressedSize)
                throw new InvalidDataException("Update ZIP uncompressed size exceeds limit.");

            // Reject absolute paths, drive letters, and parent traversal
            var entryName = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(entryName) || entryName.Contains(".."))
                throw new InvalidDataException($"Unsafe entry path: {entry.FullName}");

            var targetPath = Path.GetFullPath(Path.Combine(extractTo, entryName));
            if (!targetPath.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Zip Slip detected for entry: {entry.FullName}");

            // Directory entry
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static bool VerifySignerThumbprint(string filePath, out string error)
    {
        error = "";
        var expectedThumbprint = Environment.GetEnvironmentVariable("AGT_UPDATE_SIGNER_THUMBPRINT");
        if (string.IsNullOrWhiteSpace(expectedThumbprint))
        {
            // Thumbprint pinlenmemiş — güncelleme hash doğrulamasına güveniliyor.
            // Daha güçlü koruma için AGT_UPDATE_SIGNER_THUMBPRINT ortam değişkenini ayarlayın.
            LogService.Kaydet("UpdateService", "VerifySignerThumbprint", ["AGT_UPDATE_SIGNER_THUMBPRINT ayarli degil — imza pinleme atlandi."]);
            return true;
        }

        try
        {
#pragma warning disable SYSLIB0057
            var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
#pragma warning restore SYSLIB0057
            var actual = NormalizeHex(cert.Thumbprint ?? "");
            var expected = NormalizeHex(expectedThumbprint);

            if (actual == expected) return true;
            error = "Signer thumbprint does not match configured update signer.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Signed executable verification failed: {ex.Message}";
            return false;
        }
    }

    private static bool IsNewer(string remote, string current)
    {
        if (Version.TryParse(remote, out var r) && Version.TryParse(current, out var c))
            return r > c;
        return false;
    }

    private static string? ParseSha256Text(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = line.Split([' ', '\t', '*'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(token)) continue;
            var normalized = NormalizeHex(token);
            if (normalized.Length == 64) return normalized;
        }

        return null;
    }

    private static string NormalizeHex(string value) =>
        new string(value.Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
}
