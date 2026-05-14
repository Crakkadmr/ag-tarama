# Lisans Sistemi — Güvenlik Bug & İstismar Onarım Planı

## Context

AgTarama (.NET 10 WPF) lisans sistemi Supabase REST + lokal AES cache + NTP "floor" tabanlı koruma ile çalışıyor. Mevcut `plan.md` 4 fazlı bir yol haritası tanımlıyor; bu plan **yalnızca lisans güvenliği** kapsamında (Faz 2 UpdateService ve Faz 4 DPAPI dışarıda) hem mevcut planın eksiklerini hem de yeni keşfedilen istismarları kapsar.

**Hedef:** Saat manipülasyonu, anon key sızıntısı, cache abuse, MITM ve race condition saldırılarına karşı lisans doğrulamasını **fail-closed** ve **çift taraflı (client + server)** sağlam hale getirmek.

**Kapsam dışı:** UpdateService SHA-256 (Faz 2), DPAPI veri koruma (Faz 4), Authenticode/Themida.

---

## Tespit Edilen Tüm İstismarlar (15)

| # | İstismar | Konum | Risk | Mevcut Plan |
|---|---|---|---|---|
| 1 | Saat geri sarma + tg.dat silme bypass | [TrustedTimeService.cs:71](Services/TrustedTimeService.cs#L71), [App.xaml.cs:41](App.xaml.cs#L41) | KRİTİK | Faz 1.4 (kısmi) |
| 2 | Supabase anon key XOR decode (30 sn) | [LicenseService.cs:23-56](Services/LicenseService.cs#L23) | KRİTİK | Faz 3 |
| 3 | Cache portability (24h offline penceresi) | [LicenseService.cs:239](Services/LicenseService.cs#L239) | YÜKSEK | Kısmi |
| 4 | MachineId spoofing (MachineGuid + reg edit) | [LicenseService.cs:73-85](Services/LicenseService.cs#L73) | YÜKSEK | Ele alınmamış |
| 5 | NTP bypass (UDP 123 firewall block → local fallback) | [TrustedTimeService.cs:143-171](Services/TrustedTimeService.cs#L143) | YÜKSEK | Ele alınmamış |
| 6 | HTTP Date Advance — MITM ile floor ileri itme | [TrustedTimeService.cs:58-62, 134-141](Services/TrustedTimeService.cs#L58) | ORTA | Ele alınmamış |
| 7 | Fail-open: `!IsSuccessStatusCode` → FallbackToCache valid | [LicenseService.cs:101-102](Services/LicenseService.cs#L101) | KRİTİK | Kısmi |
| 8 | Binary patching (Obfuscar yetersiz, tek bypass noktası) | obfuscar.xml, ValidateAsync | YÜKSEK | Kapsam dışı (premium) |
| 9 | Concurrent activation race (aynı lisans 2 makine) | [LicenseService.cs:128-133](Services/LicenseService.cs#L128) | ORTA | Ele alınmamış |
| 10 | Replay attack (nonce/timestamp eksik) | [LicenseService.cs:95, 211](Services/LicenseService.cs#L95) | ORTA | Faz 3 |
| 11 | Hardcoded secrets (XOR `0xA7` trivial) | [LicenseService.cs:20-50](Services/LicenseService.cs#L20) | KRİTİK | Faz 3 |
| 12 | Exception swallow zinciri (SaveCache/SaveFloor) | [LicenseService.cs:225](Services/LicenseService.cs#L225), [TrustedTimeService.cs:191-203](Services/TrustedTimeService.cs#L191) | KRİTİK | Faz 1.6 (kısmi) |
| 13 | UI race: hızlı tarama + background expired sonucu | [App.xaml.cs:68-108](App.xaml.cs#L68) | ORTA | Faz 1.7 |
| 14 | LicenseAccepted hardcoded — IL patch için tek satır | LicenseWindow.xaml.cs | YÜKSEK | Ele alınmamış |
| 15 | tg.dat MachineGuid'e bağlı — registry clone ile reset | [TrustedTimeService.cs:186-199](Services/TrustedTimeService.cs#L186) | ORTA | Ele alınmamış |

**Debugger/anti-tamper:** `SecurityService.cs` `#if DEBUG return;` mantığı ✓ doğru, başka backdoor yok.

---

## Faz A — Client-Side Sertleştirme (acil, ~2-3 gün)

### A1. `Advance()` üst limit + HTTP Date'i NTP üzerinde tutma
**Dosya:** [Services/TrustedTimeService.cs:134-141](Services/TrustedTimeService.cs#L134)

Şu an `Advance(candidate)` aday > floor ise koşulsuz kabul ediyor. MITM saldırgan `response.Headers.Date`'i 2050 yapıp floor'u ileri iterek **gerçek expiry'yi geçersiz kılabilir**. Düzeltme:

```csharp
private const int MaxAdvanceSec = 86400; // tek seferde 24h üzeri sıçramayı reddet

private static void Advance(DateTime candidate, AdvanceSource source)
{
    // Kaynak güvenilirlik sırası: NTP > Local > HTTP-Date
    if (source == AdvanceSource.HttpDate && _floor != DateTime.MinValue
        && (candidate - _floor).TotalSeconds > MaxAdvanceSec)
        return; // HTTP Date'in tek başına büyük sıçraması reddedilir
    if (candidate > _floor)
    {
        _floor = candidate;
        SaveFloor(_floor);
    }
}
```
`UpdateFromHttpDate` → `Advance(offset.UtcDateTime, AdvanceSource.HttpDate)`, NTP yolu `AdvanceSource.Ntp`.

### A2. NTP zorunluluğu — sessiz fallback'i kapat
**Dosya:** [Services/TrustedTimeService.cs:33-52, 102-130](Services/TrustedTimeService.cs#L33)

Şu an `GetUtcNowAsync` NTP başarısızsa local UTC döner; corporate firewall UDP 123'ü kapatırsa attacker'a alan açar. Düzeltme:

- `VerifyClockAsync` zaten NTP olmazsa `Source.None` döndürüyor → bu durum `ClockVerifyResult.Ok = true` döner; **floor varsa** `Ok = false` yapılmalı (NTP'siz fakat floor'lu durumda local saatin floor'dan ≤120s fark içinde olması koşulu).
- `GetUtcNowAsync` içinde NTP yoksa: `_floor` varsa `max(local, floor)` döner ✓ (bu zaten doğru); fakat 7 gün içinde **en az bir başarılı NTP** olmamışsa `LastNtpSuccess`'i kaydet, 7 günü aşarsa `ValidateAsync` zorla online doğrulama bekler veya reddeder.

```csharp
private static DateTime _lastNtpSuccess = DateTime.MinValue;
public static bool NtpStale => _lastNtpSuccess != DateTime.MinValue
    && (DateTime.UtcNow - _lastNtpSuccess).TotalDays > 7;
```

### A3. FallbackToCache fail-open daraltma
**Dosya:** [Services/LicenseService.cs:101-102, 190-196](Services/LicenseService.cs#L101)

`!response.IsSuccessStatusCode` → FallbackToCache valid sayılıyor. Düzeltme: HTTP 4xx (özellikle 401/403/404) → **kesinlikle fail-closed**, sadece TaskCanceledException / HttpRequestException (gerçek network down) cache'e düşsün.

```csharp
if (!response.IsSuccessStatusCode)
{
    var code = (int)response.StatusCode;
    if (code >= 400 && code < 500) // sunucu cevap verdi, lisans reddi anlamlı
        return new ValidationResult(LicenseStatus.Invalid,
            $"Sunucu reddetti (HTTP {code}).");
    return FallbackToCache($"Sunucu hatası: HTTP {code}");
}
```

### A4. Exception swallow → fail-closed loglama
**Dosyalar:** [LicenseService.cs:182, 225, 242](Services/LicenseService.cs), [TrustedTimeService.cs:169, 191, 203](Services/TrustedTimeService.cs)

`SaveCache`/`SaveFloor` boş yutmada zincirleme bypass var (floor yazılmadı → IsClockRolledBack false → cache geçerli). Düzeltme:

- Her catch → `LogService.Hata("Service.Method", ex)` (zaten kısmi var, eksikleri tamamla).
- `SaveFloor` 3 kez başarısız olursa app'a bayrak — `TrustedTimeService.IsHealthy = false`, bu durumda online doğrulama mecburi.

### A5. Cache & floor için MachineId çoklu kaynak + HMAC
**Dosyalar:** [LicenseService.cs:73-85, 215-243](Services/LicenseService.cs#L73)

MachineGuid tek başına registry clone'a açık. Birden fazla kaynağı SHA-256'ya karıştır:

```csharp
public static string GetMachineId()
{
    var parts = new List<string>();
    try { using var r = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
          if (r?.GetValue("MachineGuid") is string g) parts.Add(g); } catch { }
    try { parts.Add(GetWmiUuid()); } catch { } // Win32_ComputerSystemProduct.UUID
    try { parts.Add(GetCpuId()); } catch { }   // Win32_Processor.ProcessorId
    if (parts.Count == 0) parts.Add(Environment.MachineName);
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts))));
}
```
Plus: cache payload'ına **HMAC** ekle, AES-CBC'nin authentication eksikliğini kapat (saldırgan IV+ciphertext bit flip yapamasın):

```csharp
// EncryptAes sonrası:
var hmac = HMACSHA256.HashData(key, result);
return result.Concat(hmac).ToArray();
// DecryptAes öncesi: son 32 byte'ı ayır, recompute, equal değilse return null.
```

### A6. `IsClockRolledBack()` ilk-çalıştırma boşluğunu kapat
**Dosya:** [Services/TrustedTimeService.cs:68-73](Services/TrustedTimeService.cs#L68)

İlk açılışta `_floor == MinValue` → `false` döner. Saldırgan saati geri alıp ilk açılışı yapsa, NTP olmadığı durumda floor lokal düşük zamana set edilir. Çözüm: `VerifyClockAsync` startup'ta **NTP başarısızsa ve floor yoksa** uygulamayı online doğrulamaya kilitle (LicenseWindow'a it). Bu kısmı [App.xaml.cs:14-25](App.xaml.cs#L14) zaten yapıyor (`Ok=false` → Shutdown), ancak `Source.None` Ok=true dönüyor — A2'de düzeltilecek.

### A7. ValidateInBackgroundAsync race fix
**Dosya:** [App.xaml.cs:68-108](App.xaml.cs#L68)

`mainWindow.LisansIptalEt()` çağrılıyor ama bu metot MainWindow'da yok (sadece plan.md'de öneriliyor, henüz uygulanmadı). Eklenmesi gereken:

```csharp
public bool LisansIptal { get; private set; }
public CancellationTokenSource MasterCts { get; } = new();
public void LisansIptalEt()
{
    LisansIptal = true;
    try { MasterCts.Cancel(); } catch { }
}
```
Tüm scan/capture/whois call site'larına `MasterCts.Token` parametresi yayılmalı. Tarama başlamadan önce `if (LisansIptal) return;` kontrolü.

### A8. UI tarafında doğrudan DateTime kullanımları ayıkla
**Dosyalar:** [Partials/MainWindow.License.cs:61](Partials/MainWindow.License.cs#L61) ✓ (zaten `GetTrustedNowSync` kullanıyor — doğrulandı), diğer partial'ler içinde `DateTime.Now/UtcNow` arama yapılacak (özellikle scan/log/whois). Lisans kararı veren hiçbir yerde ham DateTime olmamalı.

---

## Faz B — Sunucu Tarafı Sertleştirme (Supabase, ~1 gün — Faz A ile paralel)

**Faz 3 Edge Function bu sprintte yapılacak mı belirsiz** olduğundan iki seçenek:

### B1. **Minimum yol** (Edge Function olmadan)
- **RLS politikaları** (anon key sızıntısına karşı set savunma):
  ```sql
  ALTER TABLE licenses ENABLE ROW LEVEL SECURITY;
  -- anon: sadece SELECT, yalnızca kendi key'i ile
  CREATE POLICY "anon_select_own" ON licenses FOR SELECT TO anon
    USING (key = current_setting('request.headers')::json->>'x-license-key');
  -- anon: UPDATE yalnızca machine_id NULL ise (ilk aktivasyon)
  CREATE POLICY "anon_activate_once" ON licenses FOR UPDATE TO anon
    USING (machine_id IS NULL)
    WITH CHECK (machine_id IS NOT NULL);
  REVOKE DELETE, INSERT ON licenses FROM anon;
  ```
- **UNIQUE constraint** concurrent activation race'ini sertleştir:
  ```sql
  ALTER TABLE licenses ADD CONSTRAINT licenses_machine_unique
    UNIQUE (machine_id) DEFERRABLE INITIALLY IMMEDIATE;
  ```
- Client tarafında conditional update (`machine_id=is.null` filter ekle):
  ```csharp
  var url = $"{SupabaseUrl}/rest/v1/licenses?key=eq.{key}&machine_id=is.null";
  // PostgREST yalnızca machine_id NULL ise update edecek
  ```

### B2. **Tam yol** (Edge Function + HMAC, plan.md Faz 3 ile aynı)
Mevcut [plan.md §3.1-3.6](plan.md) tasarımı tüm gereksinimleri karşılıyor. Faz 3'e gidilirse ek olarak:
- Edge Function response'una **server_time** alanı ekle, client `TrustedTimeService.Advance(server_time, Source.Server)` kullansın.
- HMAC nonce: timestamp + 16-byte random nonce, server son 5 dk'lık nonce'ları KV'de tut (replay tam kapanır).

**Öneri:** B1 hemen uygulansın (1 saat iş), B2 ayrı sprintte değerlendirilsin.

---

## Faz C — Test ve Doğrulama

| Test | Senaryo | Beklenen |
|---|---|---|
| C1 | Saat 1 yıl ileri | `CheckCache` `Expired` (elapsed > 24h) |
| C2 | Saat 1 yıl geri | `IsClockRolledBack=true` → Invalid |
| C3 | `tg.dat` sil + saat 6 ay geri + offline | Network fail + floor yok → LicenseWindow (online zorunlu) |
| C4 | Cache başka makineye kopyala | MachineId mismatch → AES + HMAC fail → null |
| C5 | MITM ile HTTP Date 2050 | `Advance` 24h üst sınırı reddeder, floor değişmez |
| C6 | UDP 123 firewall block + cache var | 7 gün içinde son NTP varsa devam, 7 günü aşarsa online zorunlu |
| C7 | Aynı lisans 2 makineden eşzamanlı aktivasyon | UNIQUE constraint ile biri başarısız → MachineConflict |
| C8 | HTTP 404 (lisans silinmiş) | Fail-closed: Invalid (cache'e düşmez) |
| C9 | HTTP 500 (gerçek sunucu hatası) | FallbackToCache geçerli |
| C10 | Cache HMAC tamper (1 bit flip) | LoadCache null döner, log'a düşer |
| C11 | dnSpy ile ValidateAsync return Valid patch | HMAC + server-side validation sayesinde cache yeniden üretilemez, sonraki online tur reddeder |
| C12 | Background validate sırasında tarama başlat → expired sonuç | `MasterCts` cancel → tarama temiz iptal, data loss yok |

```powershell
dotnet build -c Release
dotnet test
# Manuel: saat değiştirme + tg.dat sil senaryoları
```

---

## Critical Files

**Düzenlenecek (client):**
- [Services/LicenseService.cs](Services/LicenseService.cs) — A3, A4, A5, B1 PATCH filter
- [Services/TrustedTimeService.cs](Services/TrustedTimeService.cs) — A1, A2, A4, A6
- [App.xaml.cs](App.xaml.cs) — A2 (NtpStale guard), A7
- [MainWindow.xaml.cs](MainWindow.xaml.cs) — A7 (`LisansIptalEt`, `MasterCts`)

**Sunucu (Supabase Studio SQL Editor):**
- `licenses` tablosu — RLS policies, UNIQUE constraint (B1)
- Opsiyonel: `supabase/functions/validate-license/` (B2)

**Reuse:**
- `LogService.Hata(scope, ex)` — tüm catch'ler için zaten mevcut
- `TrustedTimeService.GetTrustedNowSync()` — yeni call site'larda direkt kullanılır
- `LicenseService.GetMachineId()` — A5 ile genişletilir, çağrı arayüzü değişmez

**Yeni eklenecek dosya yok.**

---

## Sıralama

1. **A3, A4, A6, A8** — hemen (1 gün, regresyon riski düşük)
2. **A1, A5 (HMAC kısmı), A7** — 2. gün (cache format değişir → migration: eski cache silinir)
3. **A2, A5 (multi-source MachineId)** — 3. gün (MachineId değişirse mevcut Supabase machine_id mismatch → release notları "lisansı yeniden aktive edin")
4. **B1** — Supabase Studio'dan, paralel (1 saat)
5. **B2 (Edge Function)** — ayrı sprint, plan.md §3 kullanılır

**Geriye uyumluluk uyarısı:** A5'teki MachineId formülü değişikliği mevcut kullanıcıların Supabase'deki `machine_id` kayıtlarını invalid yapar. Destek için one-time reset script: `UPDATE licenses SET machine_id=NULL WHERE …`

---

## Çözülmeyen Riskler (kapsam dışı)

- **Binary patching:** Obfuscar yetersiz; gerçek koruma için Themida/VMProtect gerekir. Server-side validation (B2) bypass riskini azaltır.
- **Authenticode imzası yok:** GitHub release ZIP'i imzasız. Kapsam dışı.
- **Kernel-mode debugger / VM detection:** SecurityService.cs sadece user-mode kontrol yapar.
