using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgTarama;

namespace AgTarama.Services;

/// <summary>
/// Provides a trusted UTC clock using NTP + monotonic persisted floor.
/// </summary>
public static class TrustedTimeService
{
    private static readonly string FloorFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgTarama",
        "tg.dat");

    private static readonly string[] NtpServers =
        ["time.cloudflare.com", "pool.ntp.org", "time.google.com"];

    private static DateTime _floor = DateTime.MinValue;
    private static bool _floorLoaded;
    private static DateTime _lastNtpSuccess = DateTime.MinValue;
    private static int _saveFloorFailures;

    public static bool IsHealthy => _saveFloorFailures < 3;
    public static bool NtpStale =>
        _lastNtpSuccess != DateTime.MinValue &&
        (DateTime.UtcNow - _lastNtpSuccess).TotalDays > 7;
    public static DateTime LastNtpTime => _lastNtpSuccess;

    private enum AdvanceSource { Ntp, Local, HttpDate, TrustedServer }
    private const double MaxHttpAdvanceSec = 86400; // 24h limit for HTTP Date jumps

    /// <summary>
    /// Best-effort trusted UTC now: NTP -> local clamped by floor.
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

        var local = DateTime.UtcNow;
        if (local < _floor)
            return _floor;

        Advance(local, AdvanceSource.Local);
        return local;
    }

    /// <summary>
    /// Uses the Date header as a weak trusted source.
    /// </summary>
    public static void UpdateFromHttpDate(HttpResponseMessage response)
    {
        if (response.Headers.Date is { } offset)
            Advance(offset.UtcDateTime, AdvanceSource.HttpDate);
    }

    /// <summary>
    /// Uses an explicit trusted server timestamp (proxy/edge function).
    /// </summary>
    public static void UpdateFromTrustedUtc(DateTime utc)
    {
        var v = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        Advance(v, AdvanceSource.TrustedServer);
    }

    public static bool IsClockRolledBack()
    {
        EnsureFloorLoaded();
        if (_floor == DateTime.MinValue) return false;
        return DateTime.UtcNow < _floor.AddSeconds(-30);
    }

    public static DateTime GetTrustedNowSync()
    {
        EnsureFloorLoaded();
        var local = DateTime.UtcNow;
        if (_floor == DateTime.MinValue) return local;
        return local > _floor ? local : _floor;
    }

    public static bool HasFloor()
    {
        EnsureFloorLoaded();
        return _floor != DateTime.MinValue;
    }

    public static async Task<ClockVerifyResult> VerifyClockAsync()
    {
        EnsureFloorLoaded();
        var ntp = await TryNtpAsync();

        if (ntp.HasValue)
        {
            Advance(ntp.Value, AdvanceSource.Ntp);
            var driftSeconds = (ntp.Value - DateTime.UtcNow).TotalSeconds;
            if (driftSeconds > 120)
            {
                return new ClockVerifyResult(
                    false,
                    ClockVerifySource.Ntp,
                    $"System clock is {(int)(driftSeconds / 60)} minutes behind NTP.");
            }
            if (driftSeconds < -86400)
            {
                return new ClockVerifyResult(
                    false,
                    ClockVerifySource.Ntp,
                    $"System clock is {(int)(-driftSeconds / 3600)} hours ahead of NTP.");
            }
            return new ClockVerifyResult(true, ClockVerifySource.Ntp, null);
        }

        if (_floor == DateTime.MinValue)
        {
            return new ClockVerifyResult(
                false,
                ClockVerifySource.None,
                "Trusted time source unavailable (NTP unreachable and no floor record).");
        }

        if (DateTime.UtcNow < _floor.AddSeconds(-30))
        {
            return new ClockVerifyResult(
                false,
                ClockVerifySource.Floor,
                "System clock is behind the last trusted timestamp.");
        }

        return new ClockVerifyResult(true, ClockVerifySource.Floor, null);
    }

    private static void Advance(DateTime candidate, AdvanceSource source)
    {
        if (candidate.Kind != DateTimeKind.Utc)
            candidate = candidate.ToUniversalTime();

        if (source == AdvanceSource.HttpDate &&
            _floor != DateTime.MinValue &&
            (candidate - _floor).TotalSeconds > MaxHttpAdvanceSec)
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
                data[0] = 0x1B;

                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = 2000;
                await udp.SendAsync(data, data.Length, server, 123);

                var result = await udp.ReceiveAsync();
                var buf = result.Buffer;
                if (buf.Length < 48) continue;

                uint seconds = ((uint)buf[40] << 24) | ((uint)buf[41] << 16)
                             | ((uint)buf[42] << 8) | buf[43];
                if (seconds == 0) continue;

                var epoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var ntpTime = epoch.AddSeconds(seconds);
                _lastNtpSuccess = DateTime.UtcNow;
                return ntpTime;
            }
            catch (Exception ex)
            {
                LogService.Hata($"TrustedTimeService.TryNtp({server})", ex);
            }
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
            var raw = File.ReadAllBytes(FloorFile);

            // New format: AES-CBC + HMAC
            var plain = DecryptAesHmac(raw, key);
            if (plain is null)
            {
                // Backward compatibility: old format without HMAC
                plain = TryDecryptLegacy(raw, key);
            }
            if (plain is null) return null;

            var dto = JsonSerializer.Deserialize<FloorPayload>(Encoding.UTF8.GetString(plain), JsonOpts);
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
            var json = JsonSerializer.Serialize(new FloorPayload(floor), JsonOpts);
            File.WriteAllBytes(FloorFile, EncryptAesHmac(Encoding.UTF8.GetBytes(json), key));
            _saveFloorFailures = 0;
        }
        catch (Exception ex)
        {
            _saveFloorFailures++;
            LogService.Hata("TrustedTimeService.SaveFloor", ex);
        }
    }

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

    private static byte[]? DecryptAesHmac(byte[] data, byte[] key)
    {
        if (data.Length < 16 + 32) return null;

        var payloadLen = data.Length - 32;
        var storedHmac = data[payloadLen..];
        var payload = data[..payloadLen];
        var expectedHmac = HMACSHA256.HashData(key, payload);
        if (!CryptographicOperations.FixedTimeEquals(storedHmac, expectedHmac))
            return null;

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = payload[..16];
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(payload, 16, payload.Length - 16);
    }

    private static byte[]? TryDecryptLegacy(byte[] data, byte[] key)
    {
        try
        {
            if (data.Length < 16) return null;
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = data[..16];
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(data, 16, data.Length - 16);
        }
        catch
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private sealed record FloorPayload(DateTime Floor);
}

public enum ClockVerifySource { Ntp, Floor, None }
public record ClockVerifyResult(bool Ok, ClockVerifySource Source, string? Detail);
