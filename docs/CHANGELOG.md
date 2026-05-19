# Değişiklik Geçmişi

## v0.4.0 — 2026-05-19

### Bug düzeltmeleri (phantom device + OUI + test projesi)

- **Phantom device fix:** `SmbProbe` ve `SshBannerProbe` artık `store.GetOrAdd(ip)` yerine `store.TryGet(ip)` kullanıyor; FastProbe'ların keşfetmediği IP'ler için `DeviceInfo` oluşturulmuyor. Öncesinde 4 subnet × 254 host ≈ 1016 hayalet giriş oluşuyordu.
- **İki fazlı tarama motoru:** `DeviceDiscoveryEngine.StartScanAsync` — Faz 1 = FastProbes + Listener'lar, Faz 2 = DeepProbes (TcpPortProbe tamamlandıktan sonra). `taranan` sayacı subnet başına bir kez artırılıyor.
- **OUI kısaltma fix:** `KisaltVendor` " Foundation", " Limited", " Innovation Limited" eklerini kırpıyor. "Raspberry Pi Foundation" → "Raspberry Pi".
- **OUI Routerboard normalizasyonu:** `BulDetay` "Routerboard.com" / "Mikrotikls" vendor adlarını "MikroTik"'e çeviriyor.
- **OUI fallback:** `3C:46:D8` "EZVIZ" → "TP-Link" düzeltildi.
- **DeviceClassifier:** `MarkaNormalize` "routerboard" ve "mikrotikls" içeren vendor adlarını MikroTik'e eşliyor.
- **AgTarama.Tests:** xUnit 2.9.2, net10.0-windows, 48 test (`OuiVendorLookupTests` 18, `MacUtilsTests` 12, `DeviceStoreTests` 8, `ProbeTests` 10). `InternalsVisibleTo` `AgTarama.csproj`'a eklendi.

### Bug düzeltmeleri (bugtest.md kapsamı — 2026-05-18)

- **P0 korundu:** `AiDefaultKey` XOR-obfuscated key yerinde; vault yoksa otomatik yükleniyor.
- **HTTPS zorunluluğu:** `SettingsWindow` AI base URL `Uri.TryCreate` + `https` scheme zorunlu; HTTP reddediliyor.
- **Update imza log:** `AGT_UPDATE_SIGNER_THUMBPRINT` set edilmemişse `LogService.Kaydet` uyarısı yazılıyor.
- **AiUsageMeter thread safety:** `_lock` nesnesi; `Load()` ve `AddUsage()` lock altında.
- **AI iptal semantiği:** `AiClient` catch bloğu `OperationCanceledException` propagate ediyor.
- **tshark process cleanup:** `AiPcapAnalyzer.RunTsharkStatAsync` → finally + `Kill(entireProcessTree)` + stdout/stderr paralel drain.
- **Wi-Fi UI thread fix:** `WlanService.WifiAdaptorVarMiAsync()` (async); açılışta donma önlendi.
- **Cihaz AI modal CTS:** `AiDeviceReportWindow._cts` + `Closed` handler; pencere kapanınca istek iptal.
- **F12 AI önerisi iptal:** `_aiOneriCts`; Ctrl+Tab önceki isteği iptal edip yenisini başlatıyor.
- **CIDR /31-/32:** Parser sınırı `> 30` → `> 32`; tek host ve point-to-point subnet taranabiliyor.
- **User-Agent:** `AgTarama-AI/0.3.0` → `0.4.0`.

### AI Modu (Faz 1-4)

- **Faz 1 — Altyapı:** `Services/Ai/` — `AiKeyVault` (DPAPI+AES machine-bound), `AiClient` (OpenAI-uyumlu), `AiProvider` (OpenRouter/Google/OpenAI/Custom), `AiUsageMeter`, `AiPrompts`, `AiDefaultKey` (XOR). `AppSettings` AI alanları eklendi. Ayarlar > AI bölümü.
- **Faz 2 — Serbest sohbet:** Chatbot sekmesi DockPanel, AI input barı. `Partials/MainWindow.Ai.cs`. Kök Grid Row 2 `Height="*"` zorunlu.
- **Faz 3 — Pcap AI Analizi:** `AiPcapAnalyzer` tshark 6 istatistik komutu, 50 satır kırpma, IP maskeleme, yakalama kartına "✨ AI ile analiz et" butonu.
- **Faz 4 — Cihaz Tara AI Analizi:** `AiDeviceAnalyzer`, `AiDeviceReportWindow` koyu temalı modal, 5 preset chip, IP tespitiyle yeniden tarama butonu.

## v0.3.0 — 2026-05-17

### Güvenlik, doğruluk ve UI teması sertleştirmesi

- **CIDR `/16-/30`** gerçekten taranıyor. `SubnetGirdisiniCoz` /16-/23 → birden çok /24; /25-/30 → sınırlı host aralığı.
- **`UpdateService.SafeExtractZip`:** Zip Slip / path traversal koruması; entry sayısı ≤ 5000, toplam ≤ 500 MB, tek entry ≤ 200 MB; mutlak yol/sürücü harfi/`..` reddedilir.
- **BandwidthHistoryService** `lock (_sync)`; **`_wlanBilinenBssid`** ConcurrentDictionary; CTS disposal pattern; PingService AggregateException filtresi.
- **HistoryService** ms-hassas Id, lazy load.
- **FavoriService** IP normalizasyonu.
- **Türkçe locale ToUpper** düzeltmesi.
- **EvilTwinSinyalEsigi** ayarlanabilir (`AppSettings`, 50-90 clamp).
- **DarkCheckBox** + **DarkChip** stilleri; `prim:` namespace prefix.

## v0.2.0

İlk SNMP fingerprint, MNDP (MikroTik UDP 5678), Ubiquiti Discovery (UDP 10001), HTTP fingerprint vendor-specific endpoint sistemi.

## v0.4.1 — Refactor Sprinti (2026-05-19)

Versiyon bump YAPILMAZ — `<Version>` 0.4.0 kalır (commit/release ayrı karar).

### Faz 0 — Solution

- `AgTarama.slnx` oluşturuldu (.NET 10 modern solution format), AgTarama + AgTarama.Tests bağlı.
- Kök `.gitignore` eklendi (`**/bin/`, `**/obj/`, `*.user`, `.vs/`, `captures/`, `TestResults/`).

### Faz 1 — MD Restructure

- Kök 4 md kaldı (`AGENTS.md`, `CLAUDE.md`, `README.md`, `master-refactor.md`).
- `docs/` 5 → 16 dosya: `README`, `project`, `architecture`, `conventions`, `nuget-packages`, `services`, `services-ai`, `services-discovery`, `partials`, `ui`, `licensing`, `tasks`, `testing`, `decisions`, `release`, `CHANGELOG`.
- `AGENTS.md` 223 → 110 satır (release prosedürü `docs/release.md`'ye, changelog `docs/CHANGELOG.md`'ye taşındı).
- 5 eski rapor silindi (`bugtest.md`, `snif-test.md`, `cihaz-tara-refactor.md`, `gelistirme.md`, `csharp-project-analysis-plan.md`) — değerli kurallar `decisions.md` + `conventions.md` + `tasks.md`'ye destile edildi.

### Faz 2 — Cleanup

- `bin/` 2.0 GB + `obj/` 15 MB silindi (her iki proje).
- Kök-üstü boş `ag-tarama-release-repo/` silindi.
- `app.ico`, `tools/security/`, `supabase/` korundu (aktif kullanım).

### Faz 3 — Test Ortamı

- `test.ps1` kök harness (`-Filter`, `-Coverage`, `-NoBuild`).
- `Services/Net/CidrParser.cs` extract — `MainWindow.DeviceScan.cs > SubnetGirdisiniCoz` taşındı, test edilebilir hale geldi.
- Yeni testler: `CidrParserTests` (14), `AiUsageMeterTests` (4 — paralel race regresyonu dahil).
- `DeviceStoreTests.GetOrAdd_NewIp_CreatesEntry` fail fix (`Online` default `true` → `false` phantom guard sonrası).
- 48 → **70 test** (+22), hepsi yeşil.

### Faz 4 — Mimari Refactor

- `MainWindow.DeviceScan.cs` 1414 → 693 satır (-51%); 3 yeni partial: `DeviceScan.Export.cs` (224), `DeviceScan.Row.cs` (208 — KameraSatir VM dahil), `DeviceScan.SubnetPicker.cs` (198).
- `MainWindow.NetworkTools.cs` 901 → 171 satır (-81%); 3 yeni partial: `Tools.Ping.cs` (157), `Tools.PortScan.cs` (217), `Tools.Misc.cs` (391 — Trace/DNS/WoL/ARP/AğBilgi).
- `MainWindow` class hala tek partial — XAML referans bozulmadı.

### Bug Fix — Cihaz Tara progress sayacı

- **Sorun:** Cihaz Tara sırasında "0/254 host" göstergesi tarama bitene kadar sabit kalıyordu.
- **Sebep:** `DeviceDiscoveryEngine.StartScanAsync` `taranan` sayacını her subnet için `WhenAll` tamamlandıktan sonra tek seferde `+= host sayısı` ile artırıyordu. Tarama esnasında `reportTimer` (250ms) hep `0/254` yayıyordu.
- **Çözüm:** `IcmpProbe`'a `Action? onHostDone` callback eklendi (finally bloğunda invoke). Engine `BuildFastProbes(Action?)` overload'ı ile IcmpProbe instance'ına `() => Interlocked.Increment(ref taranan)` geçiriyor. Her ping bitince sayaç +1 → reportTimer gerçek zamanlı yayar.
- **Etkilenen dosyalar:** `Services/Discovery/Probes/IcmpProbe.cs`, `Services/Discovery/DeviceDiscoveryEngine.cs`.

### Faz 5 — Finalize

- `docs/partials.md` boyut tablosu güncellendi.
- `docs/CHANGELOG.md` bu girdi.
- `master-refactor.md` checklist tiklendi.
