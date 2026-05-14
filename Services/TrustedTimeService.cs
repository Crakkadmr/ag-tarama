using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgTarama;

namespace AgTarama.Services;

/// <summary>
/// Sistem saati manipülasyonuna karşı güvenilir UTC zamanı sağlar.
/// NTP sorgusu + "geri gidemez" kalıcı taban (floor) ile çalışır.
/// </summary>
public static class TrustedTimeService
{
    private static readonly string FloorFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama", "tg.dat");

    private static readonly string[] NtpServers =
        ["time.cloudflare.com", "pool.ntp.org", "time.google.com"];

    // Bellekte tutulan minimum zaman — uygulama yeniden başlayınca diskten yüklenir
    private static DateTime _floor = DateTime.MinValue;
    private static bool _floorLoaded = false;

    // NTP sağlık takibi
    private static DateTime _lastNtpSuccess = DateTime.MinValue;
    private static int _saveFloorFailures = 0;

    // Floor yazma sürekli başarısız olursa online doğrulama mecburi
    public static bool IsHealthy => _saveFloorFailures < 3;

    // 7 gün içinde başarılı NTP olmazsa stale sayılır
    public static bool NtpStale =>
        _lastNtpSuccess != DateTime.MinValue &&
        (DateTime.UtcNow - _lastNtpSuccess).TotalDays > 7;

    // Kaynak güvenilirlik sırası: NTP > Local > HttpDate
    private enum AdvanceSource { Ntp, Local, HttpDate }

    // ─── Dışa açık API ───────────────────────────────────────────────────────

    /// <summary>
    /// NTP → local → floor üçlüsünden en güvenilir UTC zamanını döner.
    /// </summary>
    public static async Task<DateTime> GetUtcNowAsync()
    {
        EnsureFloorLoaded();

        var ntp = await TryNtpAsync();
        if (ntp.HasValue)
        {
            Advance(ntp.Value, AdvanceSource.Ntp);
            return ntp.Value;
        }

        // NTP ulaşılamadı — yerel saate bak; geri gidiyorsa floor'u kullan
        var local = DateTime.UtcNow;
        if (local < _floor)
            return _floor;   // saat geri sarılmış → floor kullan

        Advance(local, AdvanceSource.Local);
        return local;
    }

    /// <summary>
    /// HTTP yanıtındaki Date başlığından güvenilir zaman bilgisi al.
    /// Başarılı Supabase çağrılarından sonra çağrılır.
    /// HTTP Date tek başına büyük sıçrama yapamaz (MITM koruması).
    /// </summary>
    public static void UpdateFromHttpDate(HttpResponseMessage response)
    {
        if (response.Headers.Date is { } offset)
            Advance(offset.UtcDateTime, AdvanceSource.HttpDate);
    }

    /// <summary>
    /// Sistem saati bilinen minimumun gerisindeyse true döner.
    /// İlk çalıştırmada (floor yok) her zaman false döner.
    /// </summary>
    public static bool IsClockRolledBack()
    {
        EnsureFloorLoaded();
        if (_floor == DateTime.MinValue) return false; // henüz floor kaydı yok
        return DateTime.UtcNow < _floor.AddSeconds(-30); // 30s tolerans
    }

    /// <summary>
    /// Async gerektirmeyen bağlamlarda (CheckCache gibi) en güvenilir UTC zamanını döner.
    /// NTP floor'u varsa onu taban alır, yoksa yerel UTC'yi döner.
    /// </summary>
    public static DateTime GetTrustedNowSync()
    {
        EnsureFloorLoaded();
        var local = DateTime.UtcNow;
        if (_floor == DateTime.MinValue) return local;
        return local > _floor ? local : _floor;
    }

    /// <summary>
    /// Disk floor kaydı yüklenmiş ve geçerliyse true döner.
    /// false → ilk çalıştırma veya tg.dat silinmiş; online doğrulama zorunlu.
    /// </summary>
    public static bool HasFloor()
    {
        EnsureFloorLoaded();
        return _floor != DateTime.MinValue;
    }

    /// <summary>
    /// Startup saat doğrulaması — NTP öncelikli, fallback floor.
    /// NTP erişilebilirse: sistem saati ile fark 2 dakikayı aşarsa false.
    /// NTP erişilemezse ve floor varsa: local'in floor'dan geride olup olmadığını kontrol eder.
    /// NTP erişilemez + floor yok (ilk kurulum): true döner, online doğrulama App.xaml.cs'de zorunlu kılınır.
    /// </summary>
    public static async Task<ClockVerifyResult> VerifyClockAsync()
    {
        EnsureFloorLoaded();
        var ntp = await TryNtpAsync();

        if (ntp.HasValue)
        {
            Advance(ntp.Value, AdvanceSource.Ntp);
            var driftSeconds = (ntp.Value - DateTime.UtcNow).TotalSeconds;
            // Sistem saati NTP'den 2 dakikadan fazla gerideyse veya 24 saatten fazla ilerideyse reddet
            if (driftSeconds > 120)
                return new ClockVerifyResult(false, ClockVerifySource.Ntp,
                    $"Sistem saati NTP'den {(int)(driftSeconds / 60)} dakika geride.");
            if (driftSeconds < -86400)
                return new ClockVerifyResult(false, ClockVerifySource.Ntp,
                    $"Sistem saati NTP'den {(int)(-driftSeconds / 3600)} saat ileride.");
            return new ClockVerifyResult(true, ClockVerifySource.Ntp, null);
        }

        // NTP ulaşılamadı → floor ile offline koruma
        if (_floor == DateTime.MinValue)
        {
            // İlk kurulum: floor yok, NTP yok.
            // Ok=false değil (kullanıcıyı engelleme), ama App.xaml.cs online doğrulama yapacak.
            return new ClockVerifyResult(true, ClockVerifySource.None, null);
        }

        // Floor var, NTP yok — local'in floor'dan en az 30s geride olup olmadığını kontrol et
        if (DateTime.UtcNow < _floor.AddSeconds(-30))
            return new ClockVerifyResult(false, ClockVerifySource.Floor,
                "Sistem saati son kaydedilen saatten geride.");

        // Floor var, NTP yok, local makul — ama NtpStale ise uyar (App.xaml.cs değerlendirir)
        return new ClockVerifyResult(true, ClockVerifySource.Floor, null);
    }

    // ─── Yardımcı metotlar ───────────────────────────────────────────────────

    // Tek seferde HTTP Date'in yapabileceği max ilerleme (24h) — MITM koruması
    private const double MaxHttpAdvanceSec = 86400;

    private static void Advance(DateTime candidate, AdvanceSource source)
    {
        // HTTP Date tek başına 24h'den fazla sıçrayamaz (MITM floor manipülasyonunu engeller)
        if (source == AdvanceSource.HttpDate
            && _floor != DateTime.MinValue
            && (candidate - _floor).TotalSeconds > MaxHttpAdvanceSec)
            return;

        if (candidate > _floor)
        {
            _floor = candidate;
            SaveFloor(_floor);
        }
    }

    private static async Task<DateTime?> TryNtpAsync()
    {
        foreach (var server in NtpServers)
        {
            try
            {
                var data = new byte[48];
                data[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)

                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = 2000;
                await udp.SendAsync(data, data.Length, server, 123);

                var result = await udp.ReceiveAsync();
                var buf = result.Buffer;
                if (buf.Length < 48) continue;

                // Transmit Timestamp: byte 40-43 = seconds (big-endian) since 1900-01-01
                uint seconds = ((uint)buf[40] << 24) | ((uint)buf[41] << 16)
                             | ((uint)buf[42] << 8)  |  buf[43];

                if (seconds == 0) continue;

                var epoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var ntpTime = epoch.AddSeconds(seconds);
                _lastNtpSuccess = DateTime.UtcNow; // başarılı NTP kaydı
                return ntpTime;
            }
            catch (Exception ex) { LogService.Hata($"TrustedTimeService.TryNtp({server})", ex); }
        }
        return null;
    }

    private static void EnsureFloorLoaded()
    {
        if (_floorLoaded) return;
        _floorLoaded = true;
        _floor = LoadFloor() ?? DateTime.MinValue;
    }

    private static DateTime? LoadFloor()
    {
        try
        {
            if (!File.Exists(FloorFile)) return null;
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(LicenseService.GetMachineId()));
            var raw = DecryptAes(File.ReadAllBytes(FloorFile), key);
            var dto = JsonSerializer.Deserialize<FloorPayload>(Encoding.UTF8.GetString(raw));
            return dto?.Floor;
        }
        catch (Exception ex)
        {
            LogService.Hata("TrustedTimeService.LoadFloor", ex);
            return null;
        }
    }

    private static void SaveFloor(DateTime floor)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FloorFile)!);
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(LicenseService.GetMachineId()));
            var json = JsonSerializer.Serialize(new FloorPayload(floor));
            File.WriteAllBytes(FloorFile, EncryptAes(Encoding.UTF8.GetBytes(json), key));
            _saveFloorFailures = 0; // başarı → sıfırla
        }
        catch (Exception ex)
        {
            _saveFloorFailures++;
            LogService.Hata("TrustedTimeService.SaveFloor", ex);
        }
    }

    // ─── AES yardımcıları (LicenseService ile aynı şema) ────────────────────

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

    private record FloorPayload(DateTime Floor);
}

public enum ClockVerifySource { Ntp, Floor, None }
public record ClockVerifyResult(bool Ok, ClockVerifySource Source, string? Detail);
