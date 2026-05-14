using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace AgTarama.Services;

public enum LicenseStatus { Valid, Invalid, Expired, MachineConflict, NetworkError }

public record LicenseInfo(string Key, string Type, DateTime? ExpiresAt);

public record ValidationResult(LicenseStatus Status, string Message, LicenseInfo? Info = null);

public static class LicenseService
{
    // Supabase projenizin URL ve anon key'ini buraya girin
    private const string SupabaseUrl = "https://hlljxkhtjzinfdiayjpf.supabase.co";
    private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImhsbGp4a2h0anppbmZkaWF5anBmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg3NTYzNDAsImV4cCI6MjA5NDMzMjM0MH0.3Kiah0H0xauyNMSa21bMd36VdIYZNOVias8a0Dz_2Pk";

    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama", "license.cache");

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseAnonKey);
        return client;
    }

    // Windows MachineGuid → SHA-256 hex (registry erişimi yoksa MachineName fallback)
    public static string GetMachineId()
    {
        try
        {
            using var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = reg?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(guid)));
        }
        catch
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName)));
        }
    }

    public static async Task<ValidationResult> ValidateAsync(string licenseKey)
    {
        licenseKey = licenseKey.Trim();
        if (string.IsNullOrEmpty(licenseKey))
            return new ValidationResult(LicenseStatus.Invalid, "Lisans anahtarı boş olamaz.");

        try
        {
            var url = $"{SupabaseUrl}/rest/v1/licenses?key=eq.{Uri.EscapeDataString(licenseKey)}&select=*";
            var response = await Http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return FallbackToCache("Sunucuya bağlanılamadı.");

            var json = await response.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<LicenseRow[]>(json, JsonOpts);

            if (rows is null || rows.Length == 0)
                return new ValidationResult(LicenseStatus.Invalid, "Lisans anahtarı bulunamadı.");

            var row = rows[0];

            if (!row.is_active)
                return new ValidationResult(LicenseStatus.Invalid, "Bu lisans devre dışı bırakılmış.");

            if (row.type == "subscription" && row.expires_at.HasValue && row.expires_at.Value < DateTime.UtcNow)
                return new ValidationResult(LicenseStatus.Expired,
                    $"Lisans süresi doldu: {row.expires_at.Value:dd.MM.yyyy}");

            var machineId = GetMachineId();

            if (row.machine_id is null)
            {
                // İlk aktivasyon — bu makineye bağla
                var ok = await ActivateMachineAsync(licenseKey, machineId);
                if (!ok)
                    return new ValidationResult(LicenseStatus.NetworkError, "Aktivasyon kaydedilemedi. Tekrar deneyin.");
            }
            else if (!string.Equals(row.machine_id, machineId, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(LicenseStatus.MachineConflict,
                    "Bu lisans başka bir cihaza bağlı.\nFarklı cihazda kullanmak için destek ile iletişime geçin.");
            }

            var info = new LicenseInfo(licenseKey, row.type, row.expires_at);
            SaveCache(info);
            return new ValidationResult(LicenseStatus.Valid, "Lisans geçerli.", info);
        }
        catch (TaskCanceledException)
        {
            return FallbackToCache("Sunucu yanıt vermedi (zaman aşımı).");
        }
        catch (HttpRequestException)
        {
            return FallbackToCache("İnternet bağlantısı kurulamadı.");
        }
        catch (Exception ex)
        {
            return FallbackToCache($"Hata: {ex.Message}");
        }
    }

    // Önbellekteki lisansı kontrol et (uygulama açılışında hızlı kontrol)
    public static ValidationResult? CheckCache()
    {
        var cached = LoadCache();
        if (cached is null) return null;

        if (cached.Type == "subscription" && cached.ExpiresAt.HasValue && cached.ExpiresAt.Value < DateTime.UtcNow)
        {
            ClearCache();
            return new ValidationResult(LicenseStatus.Expired,
                $"Abonelik süresi doldu: {cached.ExpiresAt.Value:dd.MM.yyyy}");
        }

        return new ValidationResult(LicenseStatus.Valid, "Önbellekten doğrulandı.", cached);
    }

    public static void ClearCache()
    {
        try { if (File.Exists(CacheFile)) File.Delete(CacheFile); } catch { }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static ValidationResult FallbackToCache(string networkError)
    {
        var cached = LoadCache();
        if (cached is not null)
            return new ValidationResult(LicenseStatus.Valid, $"Çevrimdışı mod ({networkError})", cached);
        return new ValidationResult(LicenseStatus.NetworkError, networkError);
    }

    private static async Task<bool> ActivateMachineAsync(string key, string machineId)
    {
        var url = $"{SupabaseUrl}/rest/v1/licenses?key=eq.{Uri.EscapeDataString(key)}";
        var body = JsonSerializer.Serialize(new
        {
            machine_id = machineId,
            activated_at = DateTime.UtcNow.ToString("o")
        });
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Prefer", "return=minimal");
        var resp = await Http.SendAsync(request);
        return resp.IsSuccessStatusCode;
    }

    private static void SaveCache(LicenseInfo info)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            var data = new CachePayload(info.Key, info.Type, info.ExpiresAt, DateTime.UtcNow);
            var json = JsonSerializer.Serialize(data);
            var machineKey = SHA256.HashData(Encoding.UTF8.GetBytes(GetMachineId()));
            File.WriteAllBytes(CacheFile, EncryptAes(Encoding.UTF8.GetBytes(json), machineKey));
        }
        catch { }
    }

    private static LicenseInfo? LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            var machineKey = SHA256.HashData(Encoding.UTF8.GetBytes(GetMachineId()));
            var json = Encoding.UTF8.GetString(DecryptAes(File.ReadAllBytes(CacheFile), machineKey));
            var payload = JsonSerializer.Deserialize<CachePayload>(json, JsonOpts);
            if (payload is null) return null;
            // 7 günlük çevrimdışı toleransı
            if ((DateTime.UtcNow - payload.CachedAt).TotalDays > 7) return null;
            return new LicenseInfo(payload.Key, payload.Type, payload.ExpiresAt);
        }
        catch { return null; }
    }

    private static byte[] EncryptAes(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(data, 0, data.Length);
        var result = new byte[16 + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, 16);
        Buffer.BlockCopy(cipher, 0, result, 16, cipher.Length);
        return result;
    }

    private static byte[] DecryptAes(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = data[..16];
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 16, data.Length - 16);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private record LicenseRow(
        string key, string type, bool is_active,
        string? machine_id, DateTime? expires_at, DateTime? activated_at);

    private record CachePayload(string Key, string Type, DateTime? ExpiresAt, DateTime CachedAt);
}
