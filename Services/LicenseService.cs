using System.IO;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgTarama;
using Microsoft.Win32;

namespace AgTarama.Services;

public enum LicenseStatus { Valid, Invalid, Expired, MachineConflict, NetworkError }

public record LicenseInfo(string Key, string Type, DateTime? ExpiresAt);

public record ValidationResult(LicenseStatus Status, string Message, LicenseInfo? Info = null);

public static class LicenseService
{
    private const byte _xk = 0xA7;

    // XOR-encoded Supabase endpoint — düz metin olarak derlenmez
    private static readonly byte[] _eu = {
        0xCF, 0xD3, 0xD3, 0xD7, 0xD4, 0x9D, 0x88, 0x88, 0xCF, 0xCB, 0xCB, 0xCD,
        0xDF, 0xCC, 0xCF, 0xD3, 0xCD, 0xDD, 0xCE, 0xC9, 0xC1, 0xC3, 0xCE, 0xC6,
        0xDE, 0xCD, 0xD7, 0xC1, 0x89, 0xD4, 0xD2, 0xD7, 0xC6, 0xC5, 0xC6, 0xD4,
        0xC2, 0x89, 0xC4, 0xC8
    };

    // XOR-encoded Supabase anon key
    private static readonly byte[] _ek = {
        0xC2, 0xDE, 0xED, 0xCF, 0xC5, 0xE0, 0xC4, 0xCE, 0xE8, 0xCE, 0xED, 0xEE,
        0xF2, 0xDD, 0xEE, 0x96, 0xE9, 0xCE, 0xEE, 0xD4, 0xEE, 0xC9, 0xF5, 0x92,
        0xC4, 0xE4, 0xEE, 0x91, 0xEE, 0xCC, 0xD7, 0xFF, 0xF1, 0xE4, 0xED, 0x9E,
        0x89, 0xC2, 0xDE, 0xED, 0xD7, 0xC4, 0x94, 0xEA, 0xCE, 0xE8, 0xCE, 0xED,
        0xDD, 0xC3, 0xFF, 0xE5, 0xCF, 0xFE, 0xCA, 0xE1, 0xDD, 0xFD, 0xF4, 0xEE,
        0xD4, 0xEE, 0xC9, 0xED, 0xCB, 0xFD, 0xCE, 0xEE, 0x91, 0xEE, 0xCA, 0xCF,
        0xD4, 0xC5, 0xE0, 0xD7, 0x93, 0xC6, 0x95, 0xCF, 0x97, 0xC6, 0xC9, 0xD7,
        0xD7, 0xC5, 0xCA, 0xFD, 0xCC, 0xC6, 0xF0, 0xE1, 0x92, 0xC6, 0xC9, 0xE5,
        0xCA, 0xEE, 0xCE, 0xD0, 0xCE, 0xC4, 0xCA, 0x9E, 0xD4, 0xFD, 0xF4, 0xEE,
        0x91, 0xEE, 0xCA, 0xE1, 0xD2, 0xC5, 0x95, 0x93, 0xCE, 0xEB, 0xE4, 0xED,
        0xD7, 0xFE, 0xFF, 0xF6, 0xCE, 0xE8, 0xCD, 0xE2, 0x94, 0xE9, 0xDD, 0xC0,
        0x94, 0xE9, 0xF3, 0xFE, 0xDD, 0xE9, 0xE3, 0xE6, 0xD4, 0xEE, 0xCA, 0xF1,
        0x93, 0xC4, 0xE4, 0xEE, 0x91, 0xEA, 0xCD, 0xE6, 0x92, 0xE9, 0xE3, 0xEA,
        0xDD, 0xEA, 0xCD, 0xEA, 0x97, 0xEA, 0xEF, 0x97, 0x89, 0x94, 0xEC, 0xCE,
        0xC6, 0xCF, 0x97, 0xEF, 0x97, 0xDF, 0xC6, 0xD2, 0xDE, 0xE9, 0xEA, 0xF4,
        0xC6, 0x95, 0x96, 0xC5, 0xEA, 0xC3, 0x94, 0x91, 0xF1, 0xC3, 0xEE, 0xFE,
        0xFD, 0xE9, 0xE8, 0xF1, 0xCE, 0xC6, 0xD4, 0x9F, 0xC6, 0x97, 0xE3, 0xDD,
        0xF8, 0x95, 0xF7, 0xCC
    };

    private static string Decode(byte[] data) =>
        Encoding.UTF8.GetString(data.Select(b => (byte)(b ^ _xk)).ToArray());

    private static string SupabaseUrl => Decode(_eu);
    private static string SupabaseAnonKey => Decode(_ek);

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

    // Çoklu donanım kaynağı → registry clone ile spoofing'i zorlaştırır
    public static string GetMachineId()
    {
        var parts = new List<string>();

        try
        {
            using var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            if (reg?.GetValue("MachineGuid") is string guid)
                parts.Add(guid);
        }
        catch (Exception ex) { LogService.Hata("LicenseService.GetMachineId.MachineGuid", ex); }

        try { parts.Add(GetWmiValue("Win32_ComputerSystemProduct", "UUID")); }
        catch (Exception ex) { LogService.Hata("LicenseService.GetMachineId.WmiUuid", ex); }

        try { parts.Add(GetWmiValue("Win32_Processor", "ProcessorId")); }
        catch (Exception ex) { LogService.Hata("LicenseService.GetMachineId.CpuId", ex); }

        if (parts.Count == 0)
            parts.Add(Environment.MachineName);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts))));
    }

    private static string GetWmiValue(string wmiClass, string property)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
        foreach (ManagementObject obj in searcher.Get())
        {
            var val = obj[property]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(val) && val != "None" && val != "00000000-0000-0000-0000-000000000000")
                return val;
        }
        return "";
    }

    public static async Task<ValidationResult> ValidateAsync(string licenseKey)
    {
        licenseKey = licenseKey.Trim();
        if (string.IsNullOrEmpty(licenseKey))
            return new ValidationResult(LicenseStatus.Invalid, "Lisans anahtarı boş olamaz.");

        // IsHealthy false → floor yazılamıyor → online doğrulama her seferinde zorunlu
        // (NtpStale kontrolü burada değil, App.xaml.cs startup'ta)

        try
        {
            var url = $"{SupabaseUrl}/rest/v1/licenses?key=eq.{Uri.EscapeDataString(licenseKey)}&select=*";
            var response = await Http.GetAsync(url);

            // Sunucunun Date başlığından güvenilir zamanı al → floor'u güncelle
            TrustedTimeService.UpdateFromHttpDate(response);

            // A3: HTTP 4xx → fail-closed (sunucu cevap verdi, reddetmiş)
            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                if (code >= 400 && code < 500)
                    return new ValidationResult(LicenseStatus.Invalid,
                        $"Sunucu reddetti (HTTP {code}).");
                return FallbackToCache($"Sunucu hatası: HTTP {code}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<LicenseRow[]>(json, JsonOpts);

            if (rows is null || rows.Length == 0)
                return new ValidationResult(LicenseStatus.Invalid, "Lisans anahtarı bulunamadı.");

            var row = rows[0];

            if (!row.is_active)
                return new ValidationResult(LicenseStatus.Invalid, "Bu lisans devre dışı bırakılmış.");

            var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "subscription", "lifetime" };
            if (!validTypes.Contains(row.type))
                return new ValidationResult(LicenseStatus.Invalid, "Bilinmeyen lisans tipi.");

            // Güvenilir NTP/sunucu zamanıyla süre kontrolü
            var trustedNow = await TrustedTimeService.GetUtcNowAsync();

            if (row.type == "subscription" && row.expires_at.HasValue && row.expires_at.Value < trustedNow)
                return new ValidationResult(LicenseStatus.Expired,
                    $"Lisans süresi doldu: {row.expires_at.Value:dd.MM.yyyy}");

            var machineId = GetMachineId();

            if (row.machine_id is null)
            {
                // B1: conditional update — machine_id=is.null filtresi race condition'ı azaltır
                var ok = await ActivateMachineAsync(licenseKey, machineId, trustedNow);
                if (!ok)
                    return new ValidationResult(LicenseStatus.NetworkError, "Aktivasyon kaydedilemedi. Tekrar deneyin.");
            }
            else if (!string.Equals(row.machine_id, machineId, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(LicenseStatus.MachineConflict,
                    "Bu lisans başka bir cihaza bağlı.\nFarklı cihazda kullanmak için destek ile iletişime geçin.");
            }

            var info = new LicenseInfo(licenseKey, row.type, row.expires_at);
            SaveCache(info, trustedNow);
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
            LogService.Hata("LicenseService.ValidateAsync", ex);
            return FallbackToCache($"Hata: {ex.Message}");
        }
    }

    // Önbellekteki lisansı kontrol et (uygulama açılışında hızlı kontrol)
    public static ValidationResult? CheckCache()
    {
        // Saat geriye sarılmışsa erişimi reddet — ama cache'i silme.
        // Cache'deki CachedAt güvenilir NTP zamanı içerdiğinden saat düzelince
        // loadCache elapsed kontrolü zaten geçerli sonucu verir.
        if (TrustedTimeService.IsClockRolledBack())
            return new ValidationResult(LicenseStatus.Invalid,
                "Sistem saati manipüle edilmiş. Lisans doğrulanamadı.");

        var cached = LoadCache();
        if (cached is null) return null;

        var now = TrustedTimeService.GetTrustedNowSync();
        if (cached.Type == "subscription" && cached.ExpiresAt.HasValue && cached.ExpiresAt.Value < now)
        {
            ClearCache();
            return new ValidationResult(LicenseStatus.Expired,
                $"Abonelik süresi doldu: {cached.ExpiresAt.Value:dd.MM.yyyy}");
        }

        return new ValidationResult(LicenseStatus.Valid, "Önbellekten doğrulandı.", cached);
    }

    public static void ClearCache()
    {
        try { if (File.Exists(CacheFile)) File.Delete(CacheFile); }
        catch (Exception ex) { LogService.Hata("LicenseService.ClearCache", ex); }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static ValidationResult FallbackToCache(string networkError)
    {
        var cached = LoadCache();
        if (cached is not null)
            return new ValidationResult(LicenseStatus.Valid, $"Çevrimdışı mod ({networkError})", cached);
        return new ValidationResult(LicenseStatus.NetworkError, networkError);
    }

    private static async Task<bool> ActivateMachineAsync(string key, string machineId, DateTime trustedNow)
    {
        // machine_id=is.null filtresi: sunucu sadece henüz aktive edilmemiş lisansı günceller
        // → concurrent activation race condition'ını azaltır (Supabase UNIQUE constraint ile birlikte)
        var url = $"{SupabaseUrl}/rest/v1/licenses?key=eq.{Uri.EscapeDataString(key)}&machine_id=is.null";
        var body = JsonSerializer.Serialize(new
        {
            machine_id = machineId,
            activated_at = trustedNow.ToString("o")
        });
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Prefer", "return=minimal");
        var resp = await Http.SendAsync(request);
        return resp.IsSuccessStatusCode;
    }

    private static void SaveCache(LicenseInfo info, DateTime trustedNow)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            var data = new CachePayload(info.Key, info.Type, info.ExpiresAt, trustedNow);
            var json = JsonSerializer.Serialize(data);
            var machineKey = SHA256.HashData(Encoding.UTF8.GetBytes(GetMachineId()));
            File.WriteAllBytes(CacheFile, EncryptAesHmac(Encoding.UTF8.GetBytes(json), machineKey));
        }
        catch (Exception ex) { LogService.Hata("LicenseService.SaveCache", ex); }
    }

    private static LicenseInfo? LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            var machineKey = SHA256.HashData(Encoding.UTF8.GetBytes(GetMachineId()));
            var plain = DecryptAesHmac(File.ReadAllBytes(CacheFile), machineKey);
            if (plain is null) return null; // HMAC tamper veya farklı makine
            var json = Encoding.UTF8.GetString(plain);
            var payload = JsonSerializer.Deserialize<CachePayload>(json, JsonOpts);
            if (payload is null) return null;
            var trustedNow = TrustedTimeService.GetTrustedNowSync();
            var elapsed = trustedNow - payload.CachedAt;
            if (elapsed.TotalHours > 24 || elapsed.TotalSeconds < 0) return null; // > 24h eskimiş veya saat geri sarılmış
            return new LicenseInfo(payload.Key, payload.Type, payload.ExpiresAt);
        }
        catch (Exception ex) { LogService.Hata("LicenseService.LoadCache", ex); return null; }
    }

    // ─── AES-CBC + HMAC-SHA256 (Encrypt-then-MAC) ────────────────────────────

    // Format: [16 IV | AES ciphertext | 32 HMAC]
    private static byte[] EncryptAesHmac(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(data, 0, data.Length);

        var payload = new byte[16 + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, 16);
        Buffer.BlockCopy(cipher, 0, payload, 16, cipher.Length);

        var hmac = HMACSHA256.HashData(key, payload);
        return payload.Concat(hmac).ToArray();
    }

    // null döner → HMAC geçersiz (tamper veya yanlış makine anahtarı)
    private static byte[]? DecryptAesHmac(byte[] data, byte[] key)
    {
        if (data.Length < 16 + 32) return null; // minimum boyut: IV + en az 1 block + HMAC

        var payloadLen = data.Length - 32;
        var storedHmac = data[payloadLen..];
        var payload = data[..payloadLen];

        var expectedHmac = HMACSHA256.HashData(key, payload);
        if (!CryptographicOperations.FixedTimeEquals(storedHmac, expectedHmac))
            return null; // timing-safe karşılaştırma

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = payload[..16];
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(payload, 16, payload.Length - 16);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private record LicenseRow(
        string key, string type, bool is_active,
        string? machine_id, DateTime? expires_at, DateTime? activated_at);

    private record CachePayload(string Key, string Type, DateTime? ExpiresAt, DateTime CachedAt);
}
