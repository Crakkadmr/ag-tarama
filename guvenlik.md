# Guvenlik ve Lisans Denetim Raporu

Tarih: 2026-05-14  
Kapsam: Lisans dogrulama, Supabase REST/RLS yuzeyi, yerel lisans cache'i, zaman dogrulama, update zinciri, anti-debug/obfuscation ve temel build/bug testi.

## Calistirilan Testler

- `dotnet build` -> basarili, 0 uyari, 0 hata.
- `dotnet build -c Release` -> basarili, 1 uyari:
  - `Services/CaptureService.cs(44,25)` CA2024: async metotta `_proc.StandardOutput.EndOfStream` kullanimi.
- `Services/LicenseService.cs` icindeki XOR `0xA7` bloklari cozuldu:
  - Supabase URL cozuldu.
  - Anon JWT uzunlugu: 208 karakter.
  - Anahtar rapora tam yazilmadi; sonuc, obfuscation'in gizlilik saglamadigini dogrulamak icin yeterli.
- Canli Supabase Data API izin testi yapildi, gercek lisans degeri rapora yazilmadi:
  - `GET /rest/v1/licenses?select=id&limit=1` -> HTTP 206, `row-visible`.
  - `GET /rest/v1/licenses_view?select=*&limit=1` -> HTTP 206, `row-visible`.
  - Var olmayan anahtara non-destructive test: `PATCH /rest/v1/licenses?key=eq.__CODEX_SECURITY_NONEXISTENT__&machine_id=is.null` -> HTTP 204.
- Harici exe'ler icin SHA-256 baseline alindi; bilinen-guvenilir hash listesi olmadigi icin sadece envanter niteliginde:
  - `Req/npcap-1.88.exe`: `A2F4EC1E5EA353FF67EFD24B2EBF081BA44532410FAE8D5E146AF0310AA4F56B`
  - `tools/WiresharkPortable64/WiresharkPortable64.exe`: `54D98E2974E308443F03718ACDB46D66A1A3AB56BBD2A40B0748E08A56147340`
  - `tools/Ip_Scanner/advanced_ip_scanner.exe`: `4B036CC9930BB42454172F888B8FDE1087797FC0C9D31AB546748BD2496BD3E5`
  - `tools/sadp/sadptool.exe`: `6E7A1C3EDB898132EC7561C2DB62A8E648BCE45942695F0870F6E61D0694019A`

## Kritik Bulgular

### 1. Supabase `licenses` tablosu anon rol ile satir donduruyor

Kanıt:
- Kod: `Services/LicenseService.cs:121` istemci dogrudan `/rest/v1/licenses?...&select=*` sorguluyor.
- Canli test: `GET /rest/v1/licenses?select=id&limit=1` -> HTTP 206, `row-visible`.
- `AGENTS.md` notlarinda da `license_validate FOR SELECT TO anon USING (true)` olarak ertelenmis risk tarifli.

Etki:
- Binary'den anon key cikarildiginda lisans tablosu istemci disindan sorgulanabiliyor.
- `select=*` mevcut oldugu icin kolon izinleri kisitli degilse key, machine_id, expires_at, notes gibi alanlar aciga cikabilir.

Oncelikli iyilestirme:
- Kisa vade: `SELECT` policy'yi `USING (key = current_setting('request.headers', true)::json->>'x-license-key')` benzeri bir header kosuluyla daralt; istemci `x-license-key` gondersin.
- Daha dogru mimari: istemciyi Supabase REST'ten cek, Edge Function/proxy uzerinden `service_role` ile dogrula. Istemcide Supabase URL/anon key kalmasin.
- Supabase dokumanlarina gore Data API erisimi GRANT + RLS birlikte dusunulmeli; minimum grant ve RLS zorunlu olmali.

### 2. `licenses_view` anon rol ile satir donduruyor ve RLS bypass riski tasiyor

Kanıt:
- Canli test: `GET /rest/v1/licenses_view?select=*&limit=1` -> HTTP 206, `row-visible`.
- `AGENTS.md` notlarinda `public.licenses_view` icin `SECURITY DEFINER` ve `security_invoker = true` TODO'su belirtilmis.

Etki:
- View `SECURITY DEFINER` davranisiyla olusturulduysa alttaki tablo RLS politikalarini beklenen sekilde uygulamayabilir.
- View formatli/veri-maskelemesiz alanlari donduruyorsa tablo policy'si daraltilsa bile view ayri bir sizinti kanali olur.

Oncelikli iyilestirme:
- Postgres 15+ ise: `ALTER VIEW public.licenses_view SET (security_invoker = true);`
- Gerek yoksa anon/authenticated grant'lerini kaldir: `REVOKE ALL ON public.licenses_view FROM anon, authenticated;`
- View'i public yerine Data API'ye acik olmayan private semaya tasimayi degerlendir.

### 3. Supabase anon key istemciden dakikalar icinde cikarilabiliyor

Kanıt:
- Kod: `Services/LicenseService.cs:21-57`, XOR `0xA7` ile `_eu` ve `_ek` cozuluyor.
- Testte URL ve 208 karakterlik anon JWT basariyla cozuldu.

Etki:
- Obfuscar ve XOR sadece tersine muhendisligi yavaslatir; yetkili bir sir saklama mekanizmasi degildir.
- Anon key tek basina tabloya erisebiliyorsa lisans sisteminin asil guvenligi Supabase grant/RLS hatasizligina bagli kalir.

Oncelikli iyilestirme:
- Client -> Edge Function/proxy -> Supabase modeline gec.
- Gecis sonrasi Supabase anon key'i rotate et.
- Eski client'lar icin kisa bir dual-mode pencere planla, sonra dogrudan REST erisimini kapat.

### 4. Aktivasyon PATCH'i "satir guncellendi" bilgisini dogrulamiyor

Kanıt:
- Kod: `Services/LicenseService.cs:233-249` `Prefer: return=minimal` ile PATCH atiyor ve sadece `resp.IsSuccessStatusCode` kontrol ediyor.
- Canli non-destructive test: var olmayan lisans anahtarina PATCH -> HTTP 204.

Etki:
- PostgREST, 0 satir etkilenince de basarili HTTP dondurebilir.
- Race condition senaryosu:
  1. Iki makine ayni anda `machine_id is null` satiri SELECT eder.
  2. Ilk makine PATCH ile lisansi baglar.
  3. Ikinci makinenin PATCH'i 0 satir etkiler ama HTTP 204 donerse client `ok=true` kabul eder.
  4. Ikinci makine de `SaveCache` calistirip 24 saate kadar gecici valid cache elde edebilir.

Oncelikli iyilestirme:
- PATCH'te `Prefer: return=representation` kullan ve donen row sayisini 1 olarak dogrula.
- Alternatif: RPC/Edge Function icinde atomik `UPDATE ... WHERE key = ... AND machine_id IS NULL RETURNING ...` kullan.
- Basarisiz/0-row aktivasyonu `MachineConflict` veya `Invalid` olarak fail-closed donmeli.

### 5. Guncelleme zincirinde hash, imza ve asset dogrulamasi yok

Kanıt:
- Kod: `Services/UpdateService.cs:31-56` GitHub latest release'teki ilk `.zip` asset'ini seciyor.
- Kod: `Services/UpdateService.cs:67-90` indirilen zip icin SHA-256 veya imza dogrulamasi yok.
- Kod: `Services/UpdateService.cs:101-137` zip cikarilip PowerShell `-ExecutionPolicy Bypass` ile kopyalama/restart betigi calistiriliyor.

Etki:
- GitHub release hesabi/repo/token ele gecerse veya release asset'i zehirlenirse uygulama kendini trojan'li dosyalarla degistirir.
- TLS MITM normal sartlarda zor olsa da supply-chain riskini azaltan ikinci dogrulama katmani yok.

Oncelikli iyilestirme:
- Release asset icin sabit formatli manifest ekle: `version`, `sha256`, `minVersion`, `signature`.
- Manifest'i Ed25519/RSA public key ile dogrula; public key istemcide olabilir, private key release pipeline'da kalmali.
- Zip cikarildiktan sonra ana exe ve kritik dll'lerde Authenticode publisher dogrulamasi yap.
- Ilk `.zip` yerine beklenen dosya adi pattern'i kullan: ornek `AgTarama-v{version}-win-x64.zip`.

## Yuksek Bulgular

### 6. Network hatalarinda cache fail-open davraniyor

Kanıt:
- Kod: `Services/LicenseService.cs:178-189` timeout, `HttpRequestException` ve genel exception icin `FallbackToCache`.
- Kod: `Services/LicenseService.cs:225-230` cache varsa `LicenseStatus.Valid` donuyor.

Etki:
- Sunucuda lisans iptal edilmis, makine degismis veya subscription yeni dolmus olsa bile kullanici 24 saatlik cache penceresinde devam edebilir.
- Saldirgan agi keserek online red cevabini engellerse cache penceresinden yararlanabilir.

Oncelikli iyilestirme:
- Cache fallback sadece son online dogrulama "valid" ve `CachedAt` taze ise calissin, bu zaten kismen var; fakat lisans iptali gibi olaylar icin daha kisa risk penceresi veya server-side signed lease kullan.
- Kritik lisans tiplerinde offline toleransi 24 saat yerine daha kisa yap veya lisans kaydindan imzali `lease_until` uret.

### 7. NTP yok + floor yok durumunda startup fail-open

Kanıt:
- Kod: `Services/TrustedTimeService.cs:137-142` NTP yok ve floor yoksa `ClockVerifyResult(true, None, null)`.
- `App.xaml.cs:39-49` cache valid ama floor yoksa online dogrulama deniyor; fakat online dogrulama network hatasinda tekrar cache'e dusebiliyor.

Etki:
- `tg.dat` silinmis, NTP engellenmis ve cache dosyasi mevcutsa online dogrulama network hatasi uzerinden cache valid donebilir.
- `LoadCache` HMAC ve 24 saat kontrolu iyi bir katman; yine de floor yokken "guvenilir zaman" yerel saate dusuyor.

Oncelikli iyilestirme:
- NTP yok + floor yok + cache var ise `ValidateAsync` icinde `FallbackToCache`'i kapat; online basari zorunlu olsun.
- `TrustedTimeService` icin `ClockVerifySource.None` durumunu lisans servisinde fail-closed sinyal olarak kullan.

### 8. `tg.dat` floor dosyasi AES-CBC ile MAC'siz saklaniyor

Kanıt:
- Kod: `Services/TrustedTimeService.cs:250-270` AES-CBC kullaniliyor, HMAC yok.
- `LicenseService` cache'i AES-CBC + HMAC ile daha iyi korunmusken floor dosyasi ayni seviyede degil.

Etki:
- Dosya bozulursa `LoadFloor` null'a duser ve sistem "floor yok" yoluna girer.
- Dogrudan okunabilirlik dusuk olsa da authenticated encryption olmadigi icin tamper tespiti net degil.

Oncelikli iyilestirme:
- `TrustedTimeService` floor dosyasinda da `EncryptAesHmac` benzeri encrypt-then-MAC kullan.
- Daha iyi: Windows DPAPI `ProtectedData.Protect(..., CurrentUser)` + HMAC veya AES-GCM kullan.

### 9. Obfuscar publish hatasi build'i bozmayacak sekilde ayarlanmis

Kanıt:
- `AgTarama.csproj:42-47` Release publish sonrasi Obfuscar calisiyor, `ContinueOnError="true"`.

Etki:
- Obfuscation calismazsa CI/release basarili gorunebilir ve korunmasiz binary yayinlanabilir.

Oncelikli iyilestirme:
- Release pipeline'da obfuscation hatasini fail et.
- Obfuscated cikti icin otomatik smoke test ekle.
- Build artifact olarak sadece `Obfuscated` cikti yayinlansin.

## Orta Bulgular ve Buglar

### 10. Release build CA2024 uyarisi

Kanıt:
- `dotnet build -c Release` uyarisi: `Services/CaptureService.cs(44,25)` async metotta `StandardOutput.EndOfStream`.

Etki:
- Deadlock/askida kalma ihtimali dusuk-orta; ozellikle surekli stdout ureten processlerde iptal davranisini etkileyebilir.

Oncelikli iyilestirme:
- `ReadLineAsync(token)` dongusune gec; process exit ve token iptalini `Task.WhenAny` ile koordine et.

### 11. Anti-debug/anti-tool kontrolu kolay patch'lenebilir

Kanıt:
- `Services/SecurityService.cs:29-39` release'de tek startup cagrisi ile debugger/tool kontrolu yapiyor.
- `App.xaml.cs:11` tek giris noktasi.

Etki:
- Lisans kararlarini guvenli hale getirmez; sadece analiz maliyetini artirir.
- IL patch ile `SecurityService.Dogrula()` veya `LicenseAccepted` akisi atlanabilir.

Oncelikli iyilestirme:
- Anti-debug'i tek savunma olarak gorme.
- Lisans sonucunu server-side signed token/lease ile dogrula; kritik ozellik acilislarinda lease imzasini tekrar kontrol et.

### 12. Harici araclar output'a topluca kopyalaniyor

Kanıt:
- `AgTarama.csproj:22-27` `tools/**` ve `Req/**` output'a kopyalaniyor.

Etki:
- Release paketi buyuk ve supply-chain yuzeyi genis.
- Harici exe'ler icin publish sirasinda bilinen-guvenilir hash/Authenticode kontrolu yok.

Oncelikli iyilestirme:
- Release pipeline'da harici binary hash allowlist kontrolu yap.
- Kullanilmayan Wireshark dosyalarini azalt veya resmi installer/hash kaynagi ile eslestir.

## Olumlu Noktalar

- Debug build ve Release build calisiyor.
- Lisans cache'i artik AES-CBC + HMAC ile korunuyor (`Services/LicenseService.cs:286-320`).
- Machine ID birden fazla donanim kaynagindan uretiliyor (`Services/LicenseService.cs:73-95`).
- Baslangicta lisans gecersiz/makine cakismasi/expired durumlarinda ana pencere kapatiliyor ve `MasterCts` iptal ediliyor (`App.xaml.cs:80-100`, `MainWindow.xaml.cs:43-47`).
- Supabase aktivasyonunda `machine_id=is.null` filtresi eklenmis; ancak 0-row kontrolu eksik oldugu icin tek basina yeterli degil.

## Tavsiye Edilen Oncelik Sirasi

1. Supabase `licenses_view` erisimini hemen kapat veya `security_invoker=true` yap.
2. `licenses` SELECT policy'sini daralt; anon ile tum tabloyu okunamaz hale getir.
3. `ActivateMachineAsync` icin row count / returned representation dogrulamasi ekle.
4. Client'i Supabase REST'ten Edge Function/proxy mimarisine tasi; anon key'i rotate et.
5. UpdateService'e imzali manifest + SHA-256 + Authenticode kontrolu ekle.
6. NTP yok + floor yok + cache var senaryosunda fail-closed davran.
7. `tg.dat` icin HMAC/DPAPI korumasi ekle.
8. Obfuscar hatalarini release pipeline'da fail edecek sekilde degistir.

## Kullanilan Referanslar

- Supabase API guvenligi: https://supabase.com/docs/guides/api/securing-your-api
- Supabase guvenlik dokumanlari: https://supabase.com/docs/guides/security
