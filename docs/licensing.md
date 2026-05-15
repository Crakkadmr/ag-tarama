# Lisans ve Güvenlik Referansı

## Lisans Servisleri

### LicenseService.cs (366 satır)

Supabase REST üzerinden cloud lisans doğrulama + AES önbellek + makine bağlama.

**Önemli detaylar:**
- Supabase URL ve anon key XOR-encoded (`_eu`/`_ek` byte dizileri, `0xA7`)
- Çevrimdışı tolerans: **12 saat**
- `HasFloor()==false` + network hatası → fail-closed (cache fallback yok)
- Tüm zaman kontrolü `TrustedTimeService` üzerinden
- `x-license-key` header gönderimi aktif

**Temel akış:**
```csharp
Task<LicenseResult> ValidateAsync(string key)
static LicenseResult? CheckCache()
static void ClearCache()
static string GetMachineId()
static DateTime? GetLastValidationTime()   // önbellek CachePayload.CachedAt'ı döner; yok → null
// LicenseResult: Status (Valid/Expired/Invalid), Message, Info (LicenseInfo?)
// LicenseInfo: Key, Type, ExpiresAt, MachineId
```

### TrustedTimeService.cs (301 satır)

NTP + kalıcı floor (tg.dat, AES-CBC+HMAC) + saat manipülasyonu koruması.

```csharp
static Task<TrustedTimeResult> VerifyClockAsync()
static DateTime LastNtpTime { get; }   // son başarılı NTP sorgusunun UTC zamanı (MinValue = hiç sorgulanmadı)
// NTP kaynakları: cloudflare / pool.ntp.org / time.google.com
// tg.dat: AES-CBC+HMAC ile şifrelenmiş floor timestamp
// IsClockRolledBack() — sistem saati floor'un altındaysa true
```

**Önemli:** `VerifyClockAsync()` → NTP yok + floor yok → `Ok=false` (fail-closed).

---

## Lisans UI

### Partials/MainWindow.License.cs (204 satır — #14)

Detaylı satır haritası: [docs/partials.md](partials.md)

Metotlar: `LisansPanelGuncelle`, `SetLisansUI`, `MaskeLisansAnahtari`, `LisansYenile_Click`, `LisansSifirla_Click`, `LisansBannerKapat_Click`, `LisansKopyala_Click`

**Yeni davranışlar:**
- Kalan gün < 7 → pencere üstünde sticky `LisansBanner` (oturum boyunca kapatılabilir, `_lisansBannerGizle`)
- `LisansMakineMetin`: MachineId ilk 8 karakter + `…`
- `LisansSonDogrulamaMetin`: `LicenseService.GetLastValidationTime()` UTC formatında
- `LisansNtpMetin`: `TrustedTimeService.LastNtpTime` UTC formatında

### LicenseWindow.xaml / .cs

Lisans aktivasyon ekranı. `App_Startup`'tan açılır. Karanlık tema.

### UpdateWindow.xaml / .cs + Services/UpdateService.cs

Güncelleme bildirimi + ZIP indirme + SHA-256 hash doğrulama + PowerShell self-update.

```
AgTarama-v*-win-x64.zip + .sha256 zorunlu
Opsiyonel: AGT_UPDATE_SIGNER_THUMBPRINT env var ile thumbprint pinning
```

---

## Supabase Backend

**Proje:** `network sniffer` (`hlljxkhtjzinfdiayjpf`, region: ap-northeast-1, PG 17.6)

### `public.licenses` tablosu

| Kolon | Tip | Not |
|---|---|---|
| `id` | uuid pk | — |
| `key` | text unique | `generate_license_key()` default |
| `type` | text | `'lifetime'` \| `'subscription'` |
| `is_active` | bool | — |
| `machine_id` | text nullable | UNIQUE constraint |
| `activated_at` | timestamptz | — |
| `expires_at` | timestamptz | — |
| `created_at` | timestamptz | — |
| `notes` | text | — |

**RLS Policies:**
- `license_validate` — `FOR SELECT TO anon USING (true)`
- `license_first_activation` — `FOR UPDATE TO anon USING (machine_id IS NULL) WITH CHECK (machine_id IS NOT NULL)` ✅

**GRANT'lar (anon):** sadece `SELECT, UPDATE` — `INSERT, DELETE, TRUNCATE` REVOKE edildi (2026-05-14).

**Constraints:** `licenses_key_key UNIQUE(key)`, `licenses_machine_id_unique UNIQUE(machine_id)`, `licenses_type_check`

### `public.licenses_view`

`SECURITY DEFINER` ile tanımlı → RLS bypass eder. Düzeltme: `ALTER VIEW public.licenses_view SET (security_invoker = true)`.

### Diğer objeler

`generate_license_key()`, `insert_license_30()`, `insert_license_90()`, `insert_license_lifetime()` — hepsinde `search_path` mutable (advisor WARN).

---

## Migrations

| Dosya | İçerik |
|---|---|
| `supabase/migrations/20260514_harden_licenses.sql` | REVOKE INSERT/DELETE/TRUNCATE, machine_id UNIQUE |
| `supabase/migrations/20260514_restrict_license_activation_update.sql` | UPDATE yetkisini `machine_id, activated_at` kolonlarıyla sınırla |

---

## Güvenlik Sertleştirme (2026-05-14)

**LicenseService.cs:** `x-license-key` header + aktivasyon PATCH `return=representation` + satır sayısı doğrulama + offline pencere 24h→12h + `HasFloor()==false` fail-closed.

**TrustedTimeService.cs:** `tg.dat` AES-CBC+HMAC formatına geçti; `UpdateFromTrustedUtc()` eklendi; `VerifyClockAsync()` NTP yok + floor yok → `Ok=false`.

**UpdateService.cs:** Deterministic ZIP seçimi + `.sha256` zorunluluğu + hash doğrulama başarısızsa kurulum durur + opsiyonel thumbprint pinning.

**AgTarama.csproj:** `VerifyBundledToolHashes` publish target + `ObfuscarPostBuild` `ContinueOnError=false`.

**Yeni dosyalar:** `tools/security/hashes.allowlist.sha256`, `tools/security/verify-bundled-hashes.ps1`
