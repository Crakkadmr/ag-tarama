# Değişiklik Geçmişi

## Geliştirme Dalı (bugveyeniozellikler) — 2026-05-24

### Tarama Hızı (~3-5x iyileştirme)

- **`ScanOptions.SkipDeadHosts = true`** (yeni default) — `TcpPortProbe` ARP'ta görülmeyen host'lara TCP denemez; mevcut store boşsa devre dışı (geriye dönük uyumlu).
- **`TcpPortProbe.ShouldSkip(ip, store, options)`** static helper — `store.Count > 0 && !store.TryGet(ip, out _)` koşulunu tek yerde merkezileştirir.
- **Engine Faz 0 — ARP ön tarama** — `StartScanAsync` hızlı probe'lardan önce `ArpProbe` ayrıca çalıştırır; store'a online host'ları yazar, ardından `BuildFastProbesWithoutArp` listesiyle FastProbes başlar. Faz 0 öncesinde store boşken `SkipDeadHosts` kural dışı bırakılmaz çünkü ARP tamamlanmadan port denemesi yapılmaz.
- **`BuildFastProbes` → `BuildFastProbesWithoutArp`** — ARP Faz 0'a taşındığı için hızlı listeden çıkarıldı; isim değişikliği kasıtlı.
- **Timeout azaltma** — `PingTimeoutMs` 1000→**600 ms**, `PortTimeoutMs` 800→**450 ms**, `LlmnrProbe.CancelAfter` 4000→**2000 ms**.

### Yeni Derin Probe'lar

- **`TelnetBannerProbe`** (port 23, `Services/Discovery/Probes/`) — `SemaphoreSlim(32)`, port açıksa TCP bağlan, 1s içinde banner al, IAC baytlarını (`0xFF`) sızdırmadan filtrele. `AyrıştırBanner(string)` → (Marka?, Tur) döner: MikroTik / Cisco / HP-ProCurve / Juniper / Aruba / Fortinet / pfSense / OPNsense / OpenWrt / DD-WRT / ZyXEL / BusyBox eşlemesi; fallback Router veya Switch.
- **`RtspProbe`** (port 554, `Services/Discovery/Probes/`) — `OPTIONS rtsp://<ip>/ RTSP/1.0` isteği gönderir, `Server:` header'ını çıkarır. `RtspYanitiMi(string)` — `"RTSP/"` prefix'ini doğrular. `AyrıştırSunucu(string)` case-insensitive header arama.
- **`MqttProbe`** (port 1883, `Services/Discovery/Probes/`) — MQTT 3.1.1 CONNECT paketi gönderir (14 bayt, protocol name "MQTT", clean session). `ConnackMi(byte[])` — `buf[0]==0x20 && buf[1]==0x02` CONNACK doğrulaması. IoT cihaz/broker onayı.
- **`DeviceInfo` yeni alanlar** — `TelnetBanner string?`, `RtspServerHeader string?`, `MqttBulundu bool`.

### Sınıflandırma Entegrasyonu

- **`KanitKaynak`** +Telnet, +Rtsp, +Mqtt.
- **`KanitAgirlik`** — `TelnetBanner=30`, `RtspServer=35`, `MqttDevice=20`.
- **`KanitTopla_Telnet`** — banner varsa `TelnetBannerProbe.AyrıştırBanner` çağırır, tür+marka ağırlık 30 ekler.
- **`KanitTopla_Rtsp`** — RTSP sunucu header'ından "Kamera" türü (ağırlık 35) + `MarkaIpuclari` üzerinden marka çıkarımı.
- **`KanitTopla_Mqtt`** — CONNACK varsa "Akıllı Cihaz" türü (ağırlık 20).
- **`DeepProbes`** güncel liste: `SnmpProbe, HttpFingerprintProbe, SmbProbe, SshBannerProbe, TelnetBannerProbe, RtspProbe, MqttProbe`.

### Sınıflandırma Bug Fix — WIN- hostname

- **Root cause** — Amazon/diğer arka plan yazılımları mDNS üzerinden servis kaydı yayınlıyor (`_amzn-wplay._tcp.local` → "Amazon"/"Akıllı TV", ağırlık 40). Windows default hostname "WIN-XXXX" yalnızca LLMNR (15) + `AdHostname` `win-` pattern (eski: `AdHostnameTur-10=20`) üretiyordu → toplam 35 < mDNS 40 → "Cihaz"/"Akıllı TV" kazanıyordu.
- **Fix 1** — `KanitTopla_AdHostname` `win-`/`pc-` pattern ağırlığı `AdHostnameTur-10` → **`AdHostnameTur`** (30). Yeni toplam: LLMNR(15)+AdHostname(30) = **45** > mDNS(40).
- **Fix 2** — `KanitTopla_AdHostname` `ad` string'ine `b.DhcpHostname` eklendi; sadece DHCP kanalıyla gelen "WIN-" hostname'ler de sınıflandırmaya dahil olur.

## v0.4.2 — Cihaz Tara Sınıflandırma + Opensource Entegrasyon (2026-05-20)

### Sınıflandırma Bug Fix (Marka→Tür aktarımı)

- **`KanitAgirlik.OuiTur` 10 → 18** — `MinKararEsigi=12` eşiğini geçemediği için OUI'den gelen tür ipucu (TP-Link → Router/AP, Samsung → Telefon, Hikvision → Kamera) UI'a yansımıyordu. Marka 40 puan eşiği geçiyordu, tür kalıyordu.
- **`GatewayTur=50`** yeni ağırlık + `KanitTopla_Gateway` — NIC default gateway IP'leri zorunlu `Router/AP` türü alır. `DeviceDiscoveryEngine.StartScanAsync` sonunda `NetworkInterface.GetAllNetworkInterfaces().GatewayAddresses` ile eşleme yapılır, `DeviceInfo.IsGateway=true` set edilir.
- **Samsung Galaxy model regex** — `KanitTopla_AdHostname`'e `SM-G/A/N` (telefon) + `SM-T` (tablet) + `samsung-sm-` pattern'leri eklendi.

### Cihaz Keşif (Tier 2 — Opensource Entegrasyon)

- **Wireshark `manuf` dosyası** entegre — `Req/wireshark-manuf` (3 MB, 57K kayıt). IEEE `oui.csv`'nin tamamlayıcısı (MA-M/MA-S/private allocation). Lookup sıralaması: `manuf → oui.csv → fallback`. Full vendor adı `KisaltVendor` ile temizlenir ("Reolink Innovation Limited" → "Reolink"; "Tp-Link Technologies Co.,Ltd." → "TP-Link").
- **DHCP pasif sniff** — yeni `Services/Discovery/Listeners/DhcpListener.cs`. SharpPcap BPF `udp port 67 or 68`, BOOTP magic cookie + TLV options parse. Yakalanan alanlar:
  - **Option 12** (Host Name) → `DeviceInfo.DhcpHostname` → `CihazAdiSec` sıralamasına eklendi.
  - **Option 60** (Vendor Class Identifier) → `DeviceInfo.DhcpVendorClass`. İmzalar: `MSFT 5.0` → Windows/Bilgisayar, `android-dhcp-13` → Telefon, `udhcp`/`dhcpcd`/`busybox` → Linux IoT, `dahua`/`hikvision`/`axis` → Kamera, `ubiquiti`/`ubnt` → Router/AP, `apple`/`iphone` → Apple.
  - **Option 55** (Parameter Request List) → `DhcpFingerprint` (gelecek nmap-os-db entegrasyonu için saklı).
- **`KanitKaynak` enum** +Gateway, +Dhcp.
- **`KanitAgirlik`** +GatewayTur=50, +DhcpHostname=22, +DhcpVendorClass=35.

### Önceki Sprint İçeriği (aynı v0.4.x sprint)

- **mDNS hostname parse** — `MdnsListener` DNS message parser. SRV target + A record name + PTR fallback'tan `<host>.local` extract edilir. iPhone/iPad/Mac/Chromecast/AirPlay/printer/IoT cihaz isimleri `Ad` sütununa düşer.
- **Default port listesi 19 → 26** — 21 (FTP), 515 (LPR), 631 (IPP), 1883 (MQTT), 5060 (SIP), 9100 (HP JetDirect), 34567 (XMeye DVR). Yazıcılar artık fingerprint'siz keşfedilir (mevcut `KanitTopla_PortPattern` zaten bu portları sınıflandırıyordu — ölü dal canlandı).
- **ARP cache merge** — `ArpProbe.TryRunWithPcapAsync` sonunda `RunWithArpCacheAsync` çağrılır. Pcap probe'a yanıt vermeyen ama Windows ARP cache'inde olan cihazlar yakalanır.
- **Sınıflandırma pattern + SNMP imza ek** — `MarkaIpuclari` +34 kayıt (Yealink/Polycom/Grandstream SIP, Eero/Nest/Orbi/Deco mesh router, Yi/Tapo/Wyze/Ring/Arlo/Eufy/Imou kamera, OKI/Sharp yazıcı, Buffalo/WD NAS). `SnmpImzalari` +7 regex (Ubiquiti/EdgeOS/UniFi/DSM/QTS/Buffalo/My Cloud).
- **`MarkaNormalize`** +16 satır (Yi/Wyze/Ring/Arlo/Eero/Yealink/Polycom/Grandstream/Snom/Buffalo/WD/Philips/Ecobee/OKI/Sharp/Snom).

### Cihaz Tara UI

- **Tarama ilerleme paneli** (`ScanProgressPanel`) — `KameraPanel` Grid.Row=5 (DataGrid altı, sticky, mavi çerçeve). Yapı:
  - `ScanProgressAsama` (üst-sol) — aşama metni (subnet/host sayısı/faz)
  - `ScanProgressYuzde` (üst-sağ, bold mavi) — `%67 · 8 cihaz`
  - `ScanProgressBar` (orta, 6px) — ProgressBar yüzde değeri
  - `ScanProgressDetay` (alt) — probe/listener bazlı son işlem (`▶ Subnet ... başlatıldı`, `📡 Listener'lar açıldı (5 adet, 8s)`, `⚡ Faz 1 — Hızlı probe'lar başladı (6 adet)`, `✓ ICMP tamamlandı`, vb.)
- **`ScanProgress.Detay`** opsiyonel alan — engine probe/listener başlangıç/bitiş bildirimi için ek kanal.

### Yüzde Sayacı Fix

- **`TcpPortProbe`'a `onHostDone` callback** eklendi (`IcmpProbe` ile aynı pattern). `DeviceDiscoveryEngine.BuildFastProbes` ikisine de callback bağlar.
- **`toplam = hostCount * 2`** — ICMP + TCP-Port iki kaynak host başına +1 atar. ICMP hızlı biter (~3s) → bar %50; TCP-Port uzun sürer (~30-60s) → bar 50→100. Önceden sadece ICMP sayıyordu → %100 erken görünüyordu, tarama devam ediyordu.

### Önceki Bug Fix (aynı sprint)

- **`DeviceDiscoveryEngine` host counter** — eskiden subnet başına `WhenAll` sonu tek seferde `+= 254` atılıyordu → tarama esnasında bar `0/254` sabit kalıyordu. `IcmpProbe.onHostDone` callback + `Interlocked.Increment` ile real-time progress.
- **`DeviceStoreTests.GetOrAdd_NewIp_CreatesEntry`** fail fix — `Online` default `true` → `false` (phantom guard sonrası).

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
