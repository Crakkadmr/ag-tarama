# Güvenlik ve Lisans İyileştirme Planı

## Context

AgTarama (.NET 10 WPF ağ tarama uygulaması) mevcut lisans + Supabase mimarisinde **9 doğrulanmış bug** ve **3 mimari risk** taşıyor:

- **Saat manipülasyonu:** `CheckCache()` ([LicenseService.cs:169](Services/LicenseService.cs#L169)) ve `MainWindow.License.cs:59` hâlâ doğrudan `DateTime.UtcNow / DateTime.Now` kullanıyor. TrustedTimeService eklendi ama tüm çağrı noktalarına entegre edilmedi.
- **`tg.dat` silinince koruma kapanıyor:** `IsClockRolledBack()` ilk açılışta her zaman `false` döner ([TrustedTimeService.cs:71](Services/TrustedTimeService.cs#L71)) — saldırgan `tg.dat` siler + saati 1 yıl geri alır.
- **24h offline + cache portability:** Saldırgan cache + tg.dat'ı kopyalarsa MachineGuid farklı olduğundan AES decrypt başarısız (korunuyor), ama 24 saat offline tolerans saat ileri alındığında hâlâ pencere açıyor.
- **Anon key client'ta:** XOR `0xA7` obfuscation dakikalar içinde çözülür. Supabase REST doğrudan PATCH ile başka makineye lisans bağlamaya açık (RLS yoksa).
- **UpdateService hash kontrolü yok** ([UpdateService.cs:67-91](Services/UpdateService.cs#L67-L91)): MITM / poisoned GitHub release tüm kurulumu trojan'layabilir.
- **Veri gizliliği:** `%APPDATA%\AgTarama\` altındaki logs/history/settings plain-text. KVKK kapsamında MAC/IP/topoloji bilgisi.

**Amaç:** 4 fazlı sırayla — önce kritik lisans bug'larını kapat, sonra UpdateService'i güvende, ardından Supabase'i client'tan tamamen gizle (Edge Function), son olarak yerel verileri DPAPI ile şifrele.

**Kapsam dışı:** Authenticode/EV code signing (bütçe yok), Strong naming (.snk), Themida/VMProtect gibi premium binary koruması.

---

## Faz 1 — Lisans Bug Fix'leri (öncelik: yüksek, ~2 gün)

### 1.1 `TrustedTimeService.GetTrustedNowSync()` ekle
**Dosya:** [Services/TrustedTimeService.cs](Services/TrustedTimeService.cs)

`GetUtcNowAsync()` startup'ta senkron `CheckCache()` içinden çağrılamaz. Yeni senkron API:
```csharp
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
```

### 1.2 `CheckCache()` ve `LoadCache()` trusted time kullansın
**Dosya:** [Services/LicenseService.cs:169, 237](Services/LicenseService.cs)

```csharp
// CheckCache() — DateTime.UtcNow yerine:
var now = TrustedTimeService.GetTrustedNowSync();

// LoadCache() — tolerans pencerelerini sıkılaştır:
var trustedNow = TrustedTimeService.GetTrustedNowSync();
var elapsed = trustedNow - payload.CachedAt;
if (elapsed.TotalHours > 24 || elapsed.TotalSeconds < 0) return null;
```
Negatif elapsed (`CachedAt > trustedNow`) → cache reddedilir, "saat geri" sinyali.

### 1.3 `MainWindow.License.cs:59` local time düzelt
**Dosya:** [Partials/MainWindow.License.cs:59](Partials/MainWindow.License.cs#L59)

```csharp
var bitisUtc = info.ExpiresAt!.Value.Kind == DateTimeKind.Utc
    ? info.ExpiresAt.Value
    : info.ExpiresAt.Value.ToUniversalTime();
var trustedNow = TrustedTimeService.GetTrustedNowSync();
var kalan = bitisUtc - trustedNow;
var bitisLocal = bitisUtc.ToLocalTime(); // sadece görüntüleme

LisansBitisTarihi.Text = bitisLocal.ToString("dd.MM.yyyy");
```

### 1.4 İlk açılışta NTP zorunluluğu
**Dosya:** [App.xaml.cs:9-27](App.xaml.cs)

`HasFloor() == false` (uygulamanın ilk çalıştırması veya tg.dat silinmiş) ise cache'i kabul etme; splash + online doğrulama bekle:
```csharp
var cached = LicenseService.CheckCache();
if (cached?.Status == LicenseStatus.Valid && TrustedTimeService.HasFloor())
{
    var main = new MainWindow();
    main.Show();
    _ = ValidateInBackgroundAsync(main, cached.Info!.Key);
    _ = CheckForUpdateInBackgroundAsync(main);
    return;
}

if (cached?.Status == LicenseStatus.Valid && !TrustedTimeService.HasFloor())
{
    // Floor yok — saat manipülasyonu yapılmış veya temiz kurulum.
    // Online doğrulama zorunlu, başarısızsa LicenseWindow.
    var result = await LicenseService.ValidateAsync(cached.Info!.Key);
    if (result.Status == LicenseStatus.Valid)
    {
        new MainWindow().Show();
        return;
    }
}

new LicenseWindow().Show();
```

### 1.5 Lifetime tipi enumerasyonu
**Dosya:** [Services/LicenseService.cs:111](Services/LicenseService.cs#L111)

```csharp
var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "subscription", "lifetime" };
if (!validTypes.Contains(row.type))
    return new ValidationResult(LicenseStatus.Invalid, "Bilinmeyen lisans tipi.");
```

### 1.6 Boş `catch { }` blokları loglansın
**Dosyalar:** [Services/LicenseService.cs:182, 222, 241](Services/LicenseService.cs), [Services/TrustedTimeService.cs:111, 133, 145](Services/TrustedTimeService.cs)

```csharp
catch (Exception ex) { LogService.Hata("LicenseService.LoadCache", ex); }
```
LicenseService → LogService bağımlılığı zaten var, ekstra namespace yok.

### 1.7 `ValidateInBackgroundAsync` race condition
**Dosya:** [App.xaml.cs:42-80](App.xaml.cs)

`MainWindow`'a `_shuttingDown` flag + lisans expired durumda master `CancellationTokenSource` cancel:
```csharp
public bool LisansIptal { get; private set; }
public CancellationTokenSource MasterCts { get; } = new();

public void LisansIptalEt()
{
    LisansIptal = true;
    MasterCts.Cancel();
}
```
`ValidateInBackgroundAsync` `Expired` / `Invalid` sonucunda `mainWindow.LisansIptalEt()` → tüm scan operations cancel → `await Task.Delay(500)` → `mainWindow.Close()`.

### Faz 1 Test
- **T1:** Saat 1 yıl ileri → `CheckCache` expired sinyali (cache CachedAt > floor olduğundan elapsed negatif).
- **T2:** Saat 1 yıl geri → `IsClockRolledBack` true (floor > local) → cache silinir.
- **T3:** `tg.dat` sil + saat 6 ay geri → ilk açılış akışı online zorunlu kılar, network yoksa lisans reddedilir.
- **T4:** Cache başka makineye kopyala → MachineGuid mismatch → AES decrypt fail → null.
- **T5:** Lifetime lisans (`type="lifetime"`, `expires_at=null`) → expiry check atlanır, valid.

---

## Faz 2 — UpdateService SHA-256 (öncelik: orta, ~1 gün)

### 2.1 Release manifest formatı
GitHub release body'sine fenced JSON blok:
```
```agt-manifest
{"version":"0.2.0","sha256":"a1b2c3...","size":12345678}
```
```

### 2.2 Manifest parse + hash field
**Dosya:** [Services/UpdateService.cs:12, 42-56](Services/UpdateService.cs)

```csharp
public record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes, long SizeBytes, string Sha256);

// CheckForUpdateAsync içinde:
var manifestMatch = Regex.Match(notes, @"```agt-manifest\s*([\s\S]+?)```");
if (!manifestMatch.Success) return null; // imzalı manifest yoksa güncelleme reddi
var manifest = JsonSerializer.Deserialize<JsonElement>(manifestMatch.Groups[1].Value);
var sha = manifest.GetProperty("sha256").GetString() ?? "";
return new UpdateInfo(remoteVer, dlUrl, notes.Trim(), size, sha);
```

### 2.3 İndirme sonrası doğrulama
**Dosya:** [Services/UpdateService.cs:91](Services/UpdateService.cs) sonrasına yeni public metot:

```csharp
public static bool VerifyHash(string filePath, string expectedHex)
{
    using var fs = File.OpenRead(filePath);
    var hash = SHA256.HashData(fs);
    return Convert.ToHexString(hash).Equals(expectedHex, StringComparison.OrdinalIgnoreCase);
}
```

`UpdateWindow.xaml.cs` indirme tamamlandıktan sonra `ExtractAndRestart` çağırmadan önce:
```csharp
if (!UpdateService.VerifyHash(zipPath, _info.Sha256))
{
    MessageBox.Show("Güncelleme dosyasının bütünlüğü doğrulanamadı. Yükleme iptal edildi.",
        "Güvenlik Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
    File.Delete(zipPath);
    return;
}
UpdateService.ExtractAndRestart(zipPath);
```

### 2.4 Release süreç dokümantasyonu
`EKLENECEKLER.md` veya `docs/RELEASE.md`'ye ekle: ZIP build sonrası SHA-256 hesapla, release body'ye `agt-manifest` blok yapıştır.

### Faz 2 Test
- Manuel test: yerel bir release oluştur, manifest hash'i bilerek yanlış ver → güncelleme reddedilmeli + log.
- Doğru hash → ExtractAndRestart çalışır.

---

## Faz 3 — Supabase Edge Function Proxy (öncelik: yüksek-mimari, ~3-5 gün)

### 3.1 Yeni mimari
```
[Client] --HMAC-signed POST--> [Edge Function] --service_role--> [licenses table]
```
Client artık Supabase REST API'sini bilmiyor; sadece `https://<ref>.functions.supabase.co/validate-license` endpoint'ini görüyor. Anon key kaldırılıyor; yerine HMAC shared secret (tek başına yetkisiz, sadece imza için).

### 3.2 Edge Function: `supabase/functions/validate-license/index.ts`
```typescript
import { serve } from "https://deno.land/std/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const HMAC_SECRET = Deno.env.get("CLIENT_HMAC_SECRET")!;
const TS_WINDOW_SEC = 300; // 5 dakika replay penceresi

async function verifySignature(req: Request, body: string): Promise<boolean> {
  const sig = req.headers.get("x-agt-signature");
  const ts  = req.headers.get("x-agt-timestamp");
  if (!sig || !ts) return false;
  if (Math.abs(Date.now()/1000 - parseInt(ts)) > TS_WINDOW_SEC) return false;

  const key = await crypto.subtle.importKey(
    "raw", new TextEncoder().encode(HMAC_SECRET),
    { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const mac = await crypto.subtle.sign("HMAC", key,
    new TextEncoder().encode(`${ts}.${body}`));
  const expected = Array.from(new Uint8Array(mac))
    .map(b => b.toString(16).padStart(2,"0")).join("");
  return expected === sig;
}

serve(async (req) => {
  const body = await req.text();
  if (!await verifySignature(req, body))
    return new Response("unauthorized", { status: 401 });

  const { action, key, machine_id } = JSON.parse(body);
  const sb = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!
  );

  if (action !== "validate") return jsonResp({ status: "invalid" }, 400);

  const { data, error } = await sb.from("licenses")
    .select("*").eq("key", key).maybeSingle();
  if (error || !data) return jsonResp({ status: "invalid", reason: "notfound" });
  if (!data.is_active) return jsonResp({ status: "invalid", reason: "disabled" });

  const now = new Date();
  if (data.type === "subscription" && data.expires_at && new Date(data.expires_at) < now)
    return jsonResp({ status: "expired", expires_at: data.expires_at });

  if (data.machine_id === null) {
    await sb.from("licenses")
      .update({ machine_id, activated_at: now.toISOString() }).eq("key", key);
  } else if (data.machine_id !== machine_id) {
    return jsonResp({ status: "machine_conflict" });
  }

  return jsonResp({
    status: "valid",
    type: data.type,
    expires_at: data.expires_at,
    server_time: now.toISOString()
  });
});

const jsonResp = (o:any, status=200) =>
  new Response(JSON.stringify(o), { status, headers:{"content-type":"application/json"}});
```

### 3.3 RLS Policy (Supabase SQL Editor)
```sql
ALTER TABLE licenses ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON licenses FROM anon, authenticated;
GRANT ALL ON licenses TO service_role;
```
Bu, anon key client'tan sızsa bile doğrudan `/rest/v1/licenses` çağrısının 401/empty dönmesini sağlar.

### 3.4 Client tarafı geçiş — `LicenseService.cs`
**Dosya:** [Services/LicenseService.cs](Services/LicenseService.cs)

- `_eu` (Supabase URL) ve `_ek` (anon key) byte dizilerini **kaldır**.
- `_fu` (Function URL) ve `_hk` (HMAC secret) ekle — yine XOR-encoded, ama bu sefer tek başına yetkisiz.
- `Http` apikey/Authorization header'larını kaldır.
- `ValidateAsync` yeniden yaz:

```csharp
private static async Task<HttpResponseMessage> SignedPostAsync(string body)
{
    var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var secret = Decode(_hk);
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var sig = Convert.ToHexString(
        hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{body}"))
    ).ToLowerInvariant();

    var req = new HttpRequestMessage(HttpMethod.Post, Decode(_fu))
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    req.Headers.Add("x-agt-timestamp", ts);
    req.Headers.Add("x-agt-signature", sig);
    return await Http.SendAsync(req);
}

public static async Task<ValidationResult> ValidateAsync(string licenseKey)
{
    var body = JsonSerializer.Serialize(new {
        action = "validate",
        key = licenseKey.Trim(),
        machine_id = GetMachineId()
    });

    var resp = await SignedPostAsync(body);
    TrustedTimeService.UpdateFromHttpDate(resp);

    if (!resp.IsSuccessStatusCode) return FallbackToCache("Sunucu hatası.");

    var json = await resp.Content.ReadAsStringAsync();
    var result = JsonSerializer.Deserialize<EdgeResponse>(json, JsonOpts);
    // result.status → "valid" / "expired" / "invalid" / "machine_conflict"
    // server_time → TrustedTimeService.Advance için kullan
}

private record EdgeResponse(string status, string? type, DateTime? expires_at, string? reason, DateTime? server_time);
```

### 3.5 Deploy adımları
```bash
supabase login
supabase link --project-ref <project-ref>
supabase secrets set CLIENT_HMAC_SECRET=<64-hex-random>  # openssl rand -hex 32
supabase functions deploy validate-license --no-verify-jwt
```
`--no-verify-jwt` çünkü kendi HMAC verify mekanizmamız var, Supabase'in JWT'sine ihtiyaç yok.

### 3.6 Dual-mode geçiş stratejisi
Eski client (anon key bilen) ile yeni client (HMAC) 2 hafta paralel çalışsın:
1. Edge Function deploy → yeni release `v0.2.0` çıkar (HMAC kullanan).
2. 2 hafta bekle (kullanıcılar güncellesin diye).
3. Supabase dashboard'unda anon key'i rotate et. Eski client artık çalışmaz, ama Edge Function service_role kullandığı için yeni client çalışmaya devam eder.

### Faz 3 Test
- HMAC eksik → 401.
- Timestamp 5dk eski → 401.
- Geçerli istek + machine_id eşleşmiyor → `machine_conflict`.
- Client'tan doğrudan `https://<ref>.supabase.co/rest/v1/licenses?select=*` → RLS sayesinde boş array veya 401.
- Edge logs: `supabase functions logs validate-license --tail`.

---

## Faz 4 — Veri Gizliliği: DataProtectionService (öncelik: orta, ~2 gün)

### 4.1 Yeni dosya: `Services/DataProtectionService.cs`
DPAPI (`ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser`) tabanlı. Avantajı: anahtar yönetimi yok, Windows user account'a bağlı, başka kullanıcı/makine açamaz. AES-GCM gerektirmiyor; DPAPI zaten authenticated encryption sağlar.

```csharp
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AgTarama.Services;

public static class DataProtectionService
{
    private static readonly byte[] Magic = "AGT1"u8.ToArray();
    private static readonly byte[] Entropy = "AgTarama.v1"u8.ToArray();

    public static byte[] Protect(byte[] plaintext)
    {
        var enc = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        var result = new byte[Magic.Length + enc.Length];
        Buffer.BlockCopy(Magic, 0, result, 0, Magic.Length);
        Buffer.BlockCopy(enc, 0, result, Magic.Length, enc.Length);
        return result;
    }

    public static byte[] Unprotect(byte[] data)
    {
        if (!IsProtected(data))
            return data; // legacy plaintext — migration desteği
        var inner = new byte[data.Length - Magic.Length];
        Buffer.BlockCopy(data, Magic.Length, inner, 0, inner.Length);
        return ProtectedData.Unprotect(inner, Entropy, DataProtectionScope.CurrentUser);
    }

    public static string ProtectString(string s) =>
        Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(s)));

    public static string UnprotectString(string s)
    {
        try { return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(s))); }
        catch { return s; } // muhtemelen eski plaintext
    }

    public static void ProtectFile(string path)
    {
        var raw = File.ReadAllBytes(path);
        if (IsProtected(raw)) return; // zaten şifreli
        File.WriteAllBytes(path, Protect(raw));
    }

    public static byte[] UnprotectFile(string path) =>
        Unprotect(File.ReadAllBytes(path));

    public static bool IsProtected(byte[] data) =>
        data.Length >= 4 &&
        data[0] == Magic[0] && data[1] == Magic[1] &&
        data[2] == Magic[2] && data[3] == Magic[3];
}
```

### 4.2 LogService — exit-time encryption
**Dosya:** [Services/LogService.cs](Services/LogService.cs)

Canlı log yazımı sırasında şifreleme practical değil (append paterni bozulur). Onun yerine:
1. Mevcut günün `.log` dosyası plaintext yazılmaya devam.
2. App exit'te (`App.xaml.cs` `OnExit`) günlük log dosyasını DPAPI ile sar.
3. Açılışta `LoadLogs()` (LogViewer için) `IsProtected` ise decrypt edip göster.

```csharp
// App.xaml.cs:
protected override void OnExit(ExitEventArgs e)
{
    try
    {
        foreach (var f in Directory.EnumerateFiles(Paths.LogKlasor, "*.log"))
        {
            if (Path.GetFileName(f) == $"{DateTime.Now:yyyyMMdd}.log") continue; // bugünki kalsın
            DataProtectionService.ProtectFile(f);
        }
    } catch { }
    base.OnExit(e);
}
```

### 4.3 HistoryService — yazılım anında şifrele
**Dosya:** [Services/HistoryService.cs](Services/HistoryService.cs)

`File.WriteAllText(path, json)` → `File.WriteAllBytes(path, DataProtectionService.Protect(Encoding.UTF8.GetBytes(json)))`.
Okuma tarafında `File.ReadAllBytes` → `DataProtectionService.Unprotect` → JSON parse. Eski plaintext kayıtlar `IsProtected` false → otomatik geri-uyum.

### 4.4 SettingsService — küçük dosya, tam şifrele
**Dosya:** [Services/SettingsService.cs](Services/SettingsService.cs)

```csharp
public static void Kaydet(AppSettings ayarlar)
{
    var json = JsonSerializer.Serialize(ayarlar, JsonOptions);
    var bytes = DataProtectionService.Protect(Encoding.UTF8.GetBytes(json));
    File.WriteAllBytes(DosyaYolu, bytes);
}

public static AppSettings Yukle()
{
    if (!File.Exists(DosyaYolu)) return new AppSettings();
    var raw = File.ReadAllBytes(DosyaYolu);
    var json = Encoding.UTF8.GetString(DataProtectionService.Unprotect(raw));
    return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
}
```

### 4.5 PCAP — yakalama tamamlanınca şifrele
**Dosya:** [Partials/MainWindow.Capture.cs](Partials/MainWindow.Capture.cs)

`CaptureService.YakalaAsync` callback'i tamamlandığında:
```csharp
DataProtectionService.ProtectFile(_sonPcap);
// .pcap.enc adına rename veya magic header'lı tut
```
"Wireshark'ta Aç" butonu:
1. `DataProtectionService.UnprotectFile` ile geçici `.pcap` üret (`%LOCALAPPDATA%\Temp\AgTarama\<guid>.pcap`).
2. Wireshark Process.Start.
3. App exit'te temp klasörü zorla sil (`OnExit`).

### Faz 4 Test
- `%APPDATA%\AgTarama\settings.json` hex dump: ilk 4 byte `41 47 54 31` (`AGT1`).
- Başka Windows kullanıcı hesabıyla aç → `Unprotect` `CryptographicException` fırlatır → boş ayar dönülür.
- Eski plaintext `history\*.json` mevcut kurulumda otomatik okunmalı (migration smoke test).

---

## Sıralama ve Geriye Uyumluluk

**Önerilen sıra:**
1. **Faz 1 (lisans bug fix)** — hemen, kullanıcı için en görünür değer.
2. **Faz 2 (UpdateService hash)** — bağımsız, paralel çalışılabilir.
3. **Faz 3 (Edge Function)** — yeni release `v0.2.0` ile birlikte. Dual-mode 2 hafta bekle, sonra anon key rotate.
4. **Faz 4 (DataProtection)** — en az aciliyet, kullanıcı verisi koruma.

**Cache versiyonlama:** `CachePayload`'a `int Version = 2` ekle, v1 okurken eski semantikle, v2'de yeni semantikle çalış.

---

## Critical Files

**Düzenlenecek:**
- [Services/LicenseService.cs](Services/LicenseService.cs) — Faz 1, Faz 3
- [Services/TrustedTimeService.cs](Services/TrustedTimeService.cs) — Faz 1
- [Services/UpdateService.cs](Services/UpdateService.cs) — Faz 2
- [App.xaml.cs](App.xaml.cs) — Faz 1, Faz 4 (OnExit)
- [Partials/MainWindow.License.cs](Partials/MainWindow.License.cs) — Faz 1.3
- [Services/LogService.cs](Services/LogService.cs), [Services/HistoryService.cs](Services/HistoryService.cs), [Services/SettingsService.cs](Services/SettingsService.cs) — Faz 4
- [Partials/MainWindow.Capture.cs](Partials/MainWindow.Capture.cs) — Faz 4.5

**Yeni:**
- `Services/DataProtectionService.cs` — Faz 4.1
- `supabase/functions/validate-license/index.ts` — Faz 3.2
- `docs/RELEASE.md` veya EKLENECEKLER.md güncellemesi — Faz 2.4

**AGENTS.md** her faz sonrası güncellenecek (kural §1).

---

## Verification — Genel Akış

| Faz | Test |
|---|---|
| 1 | Saat ileri/geri/floor sil senaryoları (T1-T5) — `dotnet run` Debug build'de manuel |
| 2 | Yerel test release oluştur, sahte hash → güncelleme iptal |
| 3 | `supabase functions logs --tail`, doğrudan REST → 401, HMAC eksik → 401, geçerli istek → 200 |
| 4 | Hex dump `AGT1` magic, başka user → CryptographicException, eski plaintext migration |

**Smoke test sonu:**
```powershell
dotnet build -c Release
dotnet publish -c Release
# Obfuscar post-build çalışır, çıktıdaki LicenseService'i dnSpy'da aç
# ValidateAsync'i bul, anon key veya HMAC secret string olarak görünüyor mu?
```
