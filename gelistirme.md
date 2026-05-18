# AgTarama Geliştirme Planı - 2026-05-18

Rapor mevcut çalışma ağacı üzerinden hazırlanmıştır. İnceleme sırasında `git status --short` çıktısı kirliydi: `AGENTS.md`, `AgTarama.csproj`, `MainWindow.xaml`, bazı partial dosyalar, `Services/Ai/AiPrompts.cs`, `docs/services.md`, `docs/ui.md` değişmiş; `aimode.md` silinmiş; `bugtest.md` yeni görünüyor. Bu rapor hiçbir mevcut dosyayı geri almaz ve yalnızca bu `gelistirme.md` dosyasını üretir.

## 1. Durum Özeti

1. Proje tek WPF uygulaması olarak duruyor; hedef `net10.0-windows`, `UseWPF=true`, sürüm alanları `0.4.0` (`AgTarama.csproj:5`, `AgTarama.csproj:8`, `AgTarama.csproj:9`).
2. Son 30 commit yönü net: lisans/güvenlik sertleştirme, cihaz keşfi, partial ayrıştırma ve son olarak AI modülü. En son commitler AI sohbet, pcap analizi ve cihaz raporu üstünde yoğunlaşmış.
3. Paketler sınırlı ve amaca uygun: `System.Management 9.0.5`, `QuestPDF 2024.12.*`, `ClosedXML 0.102.*`, Release'te `Obfuscar 2.2.38` (`AgTarama.csproj:31`, `AgTarama.csproj:32`, `AgTarama.csproj:33`, `AgTarama.csproj:38`).
4. Runtime araçları uygulama çıktısına kopyalanıyor; `tools/**` ve `Req/**` output'a dahil (`AgTarama.csproj:20`, `AgTarama.csproj:22`, `AgTarama.csproj:25`).
5. `Services/` katmanı geniş: paket yakalama, lisans, update, Wi-Fi, keşif protokolleri, AI, geçmiş ve export işlerini içeriyor.
6. `Partials/` bölünmesi faydalı olmuş ama `MainWindow.DeviceScan.cs` 2159 satır, `MainWindow.NetworkTools.cs` 816 satır; bu iki dosya artık modül sınırı değil, mini uygulama gibi davranıyor.
7. `MainWindow.xaml` 1986 satır; `Window.Resources` içinde ana stil sistemi var ve kök Grid'in kritik `Height="*"` düzeni korunmuş (`MainWindow.xaml:18`, `MainWindow.xaml:669`, `MainWindow.xaml:672`).
8. AI Faz 1-4 tamamlanmış görünüyor: serbest sohbet, pcap AI, cihaz AI modalı çalışacak şekilde bağlanmış (`Partials/MainWindow.Ai.cs:78`, `Partials/MainWindow.Capture.cs:347`, `Partials/MainWindow.DeviceScan.cs:1311`).
9. Mevcut çalışma ağacında AI kapsamı dokümandan daha geniş: Wi-Fi AI raporu ve F12 konsol AI komut önerisi de var (`Partials/MainWindow.Wlan.cs:63`, `Partials/MainWindow.Console.cs:138`).
10. Cihaz Tara akışı ürünün en değerli alanı: CIDR çözme, çok protokollü keşif, DataGrid, sağ tık aksiyonları, export, geçmiş karşılaştırma ve AI raporu var (`Partials/MainWindow.DeviceScan.cs:599`, `Partials/MainWindow.DeviceScan.cs:1502`, `MainWindow.xaml:1109`, `Partials/MainWindow.History.cs:207`).
11. Test altyapısı bulunamadı: `rg --files` yalnızca `AgTarama.csproj` döndürdü; xUnit/NUnit/MSTest paketi veya ayrı test projesi yok.
12. TODO/FIXME/HACK/BUG taraması kodda işaretli borç bulmadı; buna rağmen eski/yarım akış adayları var.
13. Logging var ama günlük append dosyası seviyesinde; rotation, seviye, global crash handler veya opt-in telemetry yok (`LogService.cs:10`, `LogService.cs:17`, `LogService.cs:18`).
14. Update mekanizması güvenlik açısından iyi yolda: GitHub latest release, `.sha256`, Zip Slip koruması ve opsiyonel signer thumbprint var (`Services/UpdateService.cs:43`, `Services/UpdateService.cs:79`, `Services/UpdateService.cs:209`, `Services/UpdateService.cs:254`).
15. Dokümanlarda güncellik sapması var: `docs/architecture.md` AI için hala sabit `minimax/minimax-m2.5` ve SettingsWindow'da AI ayarı yok diyor; kodda `deepseek/deepseek-v4-flash` ve AI ayar UI'ı var (`docs/architecture.md:103`, `docs/architecture.md:104`, `Services/AppSettings.cs:16`, `SettingsWindow.xaml:323`).

## 2. Boyut Analizi

### A. Özellik Olgunluğu

- Cihaz Tara akışı güçlü ama şişmiş durumda. Tarama girdisi CIDR `/16-/30` çözebiliyor, tarama çok protokollü çalışıyor ve derin tarama ek kaynaklar açıyor; kanıt: `Partials/MainWindow.DeviceScan.cs:599`, `Partials/MainWindow.DeviceScan.cs:647`, `Partials/MainWindow.DeviceScan.cs:1546`.
- Kullanıcı değer akışı büyük ölçüde tamam: tarama -> liste -> sağ tık aksiyonları -> export -> geçmiş karşılaştırma -> AI raporu. Kanıt: `MainWindow.xaml:1109`, `MainWindow.xaml:1114`, `MainWindow.xaml:1125`, `Partials/MainWindow.DeviceScan.cs:1085`, `Partials/MainWindow.History.cs:207`, `Partials/MainWindow.DeviceScan.cs:1311`.
- Cihaz detay deneyimi henüz tablo/sağ tık ağırlıklı. Ayrı bir detay paneli veya cihaz profil ekranı yok; detay aksiyonları context menu üzerinden dağıtılmış (`MainWindow.xaml:1114`-`MainWindow.xaml:1129`).
- AI sohbet çalışıyor ve bağlamı son 6 mesajla sınırlıyor; bu iyi bir maliyet kontrolü ama uzun analizlerde kullanıcı “önceki raporu hatırla” beklentisini sınırlayabilir (`Partials/MainWindow.Ai.cs:75`).
- Pcap AI analizi tamamlanmış görünüyor: tshark istatistikleri toplanıyor, özel IP maskeleme opsiyonel, sonuç geçmişe yazılıyor (`Services/Ai/AiPcapAnalyzer.cs:39`, `Services/Ai/AiPcapAnalyzer.cs:41`, `Partials/MainWindow.Capture.cs:349`).
- Cihaz AI raporu modal olarak olgun: preset, özel soru, kopyala, TXT kaydet, yeniden sor ve IP yeniden tara var (`AiDeviceReportWindow.xaml.cs:43`, `AiDeviceReportWindow.xaml.cs:92`, `AiDeviceReportWindow.xaml:151`, `AiDeviceReportWindow.xaml:166`).
- Wi-Fi AI raporu kodda var ama ana v0.4 açıklamasında yok; bu “ürün var, dokümantasyon/konumlandırma eksik” durumu (`Partials/MainWindow.Wlan.cs:63`, `Services/Ai/AiPrompts.cs:20`, `AGENTS.md:169`).
- Port taramada AI yorumlama var fakat sonuç HistoryService'e yazılmıyor; pcap AI yazarken port/Wi-Fi AI sadece ekrana basıyor (`Partials/MainWindow.NetworkTools.cs:534`, `Partials/MainWindow.Capture.cs:349`, `Partials/MainWindow.Wlan.cs:99`).
- F12 konsolda `scan` komutu kayıtlı ama doğrudan tarama başlatmıyor; kullanıcı beklentisi açısından “komut var ama stub” (`Services/CommandRouter.cs:201`, `Services/CommandRouter.cs:202`).
- Bant genişliği paneli temel akışı tamamlıyor; per-app analiz admin yetkisi gerektiriyor ve bu durum kodda kontrol ediliyor (`Partials/MainWindow.Bandwidth.cs:41`, `Partials/MainWindow.Bandwidth.cs:44`).
- Geçmiş karşılaştırma var ama çıktı chat metni olarak kalıyor; ayrı karşılaştırma görünümü/export henüz yok (`Partials/MainWindow.History.cs:253`, `Partials/MainWindow.History.cs:266`).
- Evil-Twin eşiği ayar modelinde var ama SettingsWindow'da kullanıcıya açılmamış görünüyor; kaydetme eski değeri koruyor (`Services/AppSettings.cs:11`, `SettingsWindow.xaml.cs:190`, `Partials/MainWindow.Wlan.cs:313`).

### B. Teknik Borç

- `MainWindow.DeviceScan.cs` 2159 satır ve kendi içinde domain model, CIDR parser, tarama scheduler, export, UI event handler ve view model barındırıyor. Bu dosya refactor sınırını geçmiş; kanıt: satır sayımı ve `Partials/MainWindow.DeviceScan.cs:32`, `Partials/MainWindow.DeviceScan.cs:599`, `Partials/MainWindow.DeviceScan.cs:1085`, `Partials/MainWindow.DeviceScan.cs:1502`, `Partials/MainWindow.DeviceScan.cs:2299`.
- `MainWindow.NetworkTools.cs` 816 satır; ping, port, traceroute, DNS, WoL, ARP, harici araç ve chat mesajlama aynı partial içinde (`Partials/MainWindow.NetworkTools.cs:22`, `Partials/MainWindow.NetworkTools.cs:481`, `Partials/MainWindow.NetworkTools.cs:676`, `Partials/MainWindow.NetworkTools.cs:762`).
- DI yok; servislerin çoğu `static`, UI doğrudan çağırıyor. Kanıt: `Services/LicenseService.cs:19`, `Services/UpdateService.cs:24`, `Services/Ai/AiClient.cs:13`, `Services/SettingsService.cs:7`, `MainWindow.xaml.cs:95`.
- Native/harici komut çalıştırma servislerde kısmen soyutlanmış ama ortak bir `IProcessRunner` yok. `netsh`, `tshark`, `arp`, `tracert`, `nbtstat`, Advanced IP Scanner doğrudan `ProcessStartInfo` ile çağrılıyor (`Services/WlanService.cs:26`, `Services/InterfaceDiscoveryService.cs:15`, `Partials/MainWindow.NetworkTools.cs:644`, `Services/AdvancedIpScannerService.cs:33`).
- Test yok. En riskli alanlar saf fonksiyon içermesine rağmen testlenmiyor: `PortScanService.Parse`, `SubnetGirdisiniCoz`, `UpdateService.ParseSha256Text`, `HistoryService.SonKayitlariYukle` (`Services/PortScanService.cs:12`, `Partials/MainWindow.DeviceScan.cs:599`, `Services/UpdateService.cs:284`, `Services/HistoryService.cs:57`).
- Hard-coded port/timeouts/concurrency fazla: kamera portları, semaphore değerleri, timeoutlar ve User-Agent stringleri dağınık (`Partials/MainWindow.DeviceScan.cs:89`, `Partials/MainWindow.DeviceScan.cs:1615`, `Partials/MainWindow.DeviceScan.cs:1631`, `Services/Ai/AiClient.cs:246`).
- AI client User-Agent hala `AgTarama-AI/0.3.0`; uygulama sürümü `0.4.0` (`Services/Ai/AiClient.cs:246`, `AgTarama.csproj:9`).
- Bazı hatalar sessiz yutuluyor. Cihaz tarama içinde çok sayıda `catch { }` var; ağ keşfinde makul olabilir ama debug/telemetri değeri kaybediliyor (`Partials/MainWindow.DeviceScan.cs:937`, `Partials/MainWindow.DeviceScan.cs:1371`, `Partials/MainWindow.DeviceScan.cs:1467`, `Partials/MainWindow.DeviceScan.cs:2129`).
- Eski kod kalıntısı var: `PortPanelAcAnimasyon` ve `PortPanelKapatAnimasyon` “artık kullanılmıyor” notuyla duruyor (`Partials/MainWindow.NetworkTools.cs:391`, `Partials/MainWindow.NetworkTools.cs:392`).
- Doküman drift'i teknik borç haline gelmiş; `docs/architecture.md` AI ayar UI'ı yok diyor, oysa SettingsWindow AI bölümü var (`docs/architecture.md:104`, `SettingsWindow.xaml:323`).

### C. Mimari Sınırlar

- WPF + partial yaklaşımı proje için başlangıçta makul olmuş; tek geliştirici için hız sağlamış. Ancak `DeviceScan` ve `NetworkTools` artık “UI event handler” sınırını aşmış durumda.
- Tam MVVM geçişi şu an pahalı ve riskli. Daha mantıklı sınır: yeni ve büyüyen alanlarda servis + küçük view model nesneleri, mevcut ekranlarda kademeli ayrıştırma.
- AI modülü ayrı assembly olmak zorunda değil. Şu an `Services/Ai/` klasörü yeterli; tek csproj olduğu doğrulandı. Ancak AI istemcisi, promptlar ve UI entegrasyonları arasında sınır belirginleşmeli (`Services/Ai/AiClient.cs:13`, `Services/Ai/AiPrompts.cs:3`, `Partials/MainWindow.Capture.cs:347`).
- Pcap tarafında tshark bağımlılığı `CaptureService`, `InterfaceDiscoveryService`, `AiPcapAnalyzer` içinde toplanmış; iyi bir başlangıç. Yine de test edilebilirlik için process runner arayüzü yok (`Services/CaptureService.cs:26`, `Services/InterfaceDiscoveryService.cs:15`, `Services/Ai/AiPcapAnalyzer.cs:74`).
- Wlan native bağımlılığı `WlanService` içinde; bu iyi. Ancak Wi-Fi AI ve geçmiş yazma UI partial içinde kalmış (`Services/WlanService.cs:24`, `Partials/MainWindow.Wlan.cs:63`, `Partials/MainWindow.Wlan.cs:386`).
- Lisans/update servisleri statik ve global yan etkilere sahip; testlemek için adapter gerektirir (`Services/LicenseService.cs:65`, `Services/UpdateService.cs:31`).

### D. Kullanıcı Deneyimi

- Dark tema ana pencerede kapsamlı: `ActionButton`, `DarkComboBox`, `DarkDataGrid`, `DarkCheckBox`, `FlatContextMenu` gibi ana stiller tek yerde (`MainWindow.xaml:20`, `MainWindow.xaml:310`, `MainWindow.xaml:447`, `MainWindow.xaml:548`, `MainWindow.xaml:608`).
- Ayrı pencerelerde stil tekrarı riski var. `AiDeviceReportWindow` kendi `PresetChipStyle` stilini içeriyor; SettingsWindow kendi PasswordBox stili kullanıyor (`AiDeviceReportWindow.xaml:48`, `SettingsWindow.xaml:343`).
- Uzun işlemlerde cancel genel olarak var: yakalama, cihaz tara, Wi-Fi, ping/port/traceroute CTS kullanıyor (`Partials/MainWindow.Capture.cs:389`, `Partials/MainWindow.DeviceScan.cs:1288`, `Partials/MainWindow.Wlan.cs:56`, `Partials/MainWindow.NetworkTools.cs:277`).
- AI işlemlerinde cancel deneyimi tutarsız. Chat input kilitleniyor, pcap AI butonu bekleme durumuna geçiyor, ama kullanıcıya açık “iptal et” kontrolü yok (`Partials/MainWindow.Ai.cs:61`, `Partials/MainWindow.Capture.cs:336`, `Partials/MainWindow.Wlan.cs:76`).
- Hata mesajları çoğunlukla `ex.Message` olarak kullanıcıya gidiyor; bu hızlı ama ürünleşme için ham kalıyor (`MainWindow.xaml.cs:207`, `Partials/MainWindow.Wlan.cs:198`, `Partials/MainWindow.DeviceScan.cs:1601`, `UpdateWindow.xaml.cs:85`).
- Klavye tarafı güçlü başlangıç yapmış: F12 konsol, Enter ile çalıştırma, Tab/Ctrl+Tab öneri var (`Partials/MainWindow.Console.cs:16`, `Partials/MainWindow.Console.cs:78`, `Partials/MainWindow.Console.cs:111`).
- Erişilebilirlik tarafı net değil. XAML'de çok sayıda event handler var ama AutomationProperties/access key yaklaşımı görünmedi; manuel test dışında doğrulama yok.
- Cihaz Tara tablo görünümü yoğun ve işlevsel; ancak yeni kullanıcı için “bu cihaz neden böyle tanındı?” açıklaması ayrı detay panelinde daha iyi değer üretir.

### E. Operasyonel

- Logging stratejisi var: `%APPDATA%\AgTarama\logs\yyyyMMdd.log` ve append. Ancak seviye, retention, rotation, log viewer yok (`LogService.cs:10`, `LogService.cs:17`, `LogService.cs:18`).
- Global crash reporting yok. `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` aramasında kaynak kodda kayıt bulunmadı.
- Update mekanizması ürünleşme açısından iyi: latest release, asset seçimi, `.sha256`, hash doğrulama, güvenli zip extract ve opsiyonel signer pinning mevcut (`Services/UpdateService.cs:43`, `Services/UpdateService.cs:79`, `UpdateWindow.xaml.cs:71`, `Services/UpdateService.cs:209`, `Services/UpdateService.cs:254`).
- Release prosedürü net; `.sha256` zorunlu ve `--latest` isteniyor (`AGENTS.md:101`, `AGENTS.md:114`, `AGENTS.md:117`).
- Lisanslama sıkı: Supabase REST, makine bağlama, 12 saat offline tolerans, trusted time floor var (`Services/LicenseService.cs:23`, `Services/LicenseService.cs:112`, `Services/LicenseService.cs:209`, `Services/TrustedTimeService.cs:99`).
- Lisans akışında Supabase canlı durumunu doğrulamadım; `docs/licensing.md` içinde `licenses_view` için `security_invoker` düzeltmesi “düzeltme” olarak yazıyor ama uygulanıp uygulanmadığı lokal incelemeden netleşmedi (`docs/licensing.md:98`, `docs/licensing.md:100`). Bu kullanıcıya sorulmalı veya Supabase'de ayrıca doğrulanmalı.
- AI default key otomatik vault'a yazılıyor; bu ürün kolaylığı sağlıyor ama dağıtım/güvenlik/model maliyeti kararı gerektiriyor (`MainWindow.xaml.cs:183`, `Services/Ai/AiKeyVault.cs:14`, `Services/Ai/AiDefaultKey.cs:9`).

### F. Yeni Özellik Fırsatları

- Tarama profili kaydet/yükle ucuz ve değerli: subnet, derin tarama, port seti, filtreler, export formatı saklanabilir. Mevcut `AppSettings` ve `HistoryService` altyapısı bunu destekler (`Services/AppSettings.cs:5`, `Services/HistoryService.cs:31`).
- Geçmiş tarama karşılaştırması zaten var; bunu ayrı “değişim raporu” paneline ve PDF/TXT export'a taşımak düşük maliyetli değer üretir (`Partials/MainWindow.History.cs:207`, `Partials/MainWindow.History.cs:258`).
- AI doğal dil sorgu “son taramadaki kameraları listele / yeni açılan portları açıkla” şeklinde mevcut `CihazlarJson` metadata üzerinden yapılabilir (`Partials/MainWindow.DeviceScan.cs:1589`, `Partials/MainWindow.History.cs:284`).
- AI pcap tarafında “anomaly checklist” ve “aksiyon önerisi” zaten prompt'a yakın; sonuçları HistoryService'e daha yapılandırılmış yazmak sonraki özellikleri açar (`Services/Ai/AiPrompts.cs:31`, `Partials/MainWindow.Capture.cs:349`).
- Rakiplerden eksik temel alan: Fing benzeri cihaz profili ve güven skoru açıklaması; Advanced IP Scanner benzeri hızlı profiller; Wireshark benzeri daha güçlü pcap filtre/özet seçenekleri. Bu öneriler mevcut ürünün ağ tarama odağına uyuyor, platform değişikliği gerektirmiyor.

## 3. Aday Öğeler (skorlu tablo)

Skor formülü: `Etki x 2 - Maliyet - Risk`. Skor sadece sıralama yardımcısıdır.

| Öğe | Etki | Maliyet | Risk | Skor | Kategori |
|-----|------|---------|------|------|----------|
| Global exception logging + log rotation/retention | 4 | 1 | 1 | 6 | Operasyonel |
| Servis odaklı test projesi: parser, history, update hash, port parse | 5 | 2 | 2 | 6 | Teknik borç |
| AI default key politikasını netleştir ve User-Agent/version drift düzelt | 5 | 2 | 3 | 5 | Güvenlik/AI |
| Cihaz Tara profil kaydet/yükle | 4 | 2 | 2 | 4 | Özellik |
| Geçmiş karşılaştırmayı rapor paneli/export akışına taşı | 4 | 2 | 2 | 4 | UX/Özellik |
| Settings drift düzelt: EvilTwin eşiği UI, AI/Wi-Fi doküman hizalama | 3 | 1 | 1 | 4 | UX/Doküman |
| Release/update doğrulama komutlarını tek script/komut haline getir | 4 | 2 | 2 | 4 | Operasyonel |
| AI doğal dil sorgu: son cihaz taraması üstünden soru-cevap | 5 | 3 | 3 | 4 | AI |
| Cihaz detay paneli: “neden böyle tanındı?” ve hızlı aksiyonlar | 4 | 3 | 2 | 3 | UX |
| Erişilebilirlik ve klavye geçiş pass'i | 3 | 2 | 1 | 3 | UX |
| `DeviceScan` saf çekirdeğini servis/helper katmanına çıkar | 5 | 4 | 4 | 2 | Mimari |
| Native komut runner adapter'ı (`IProcessRunner`) | 4 | 3 | 3 | 2 | Mimari/Test |
| AI sonuçlarını HistoryService'e tutarlı ve metadata'lı kaydet | 3 | 2 | 2 | 2 | AI/Geçmiş |
| Export şablonları ve rapor metadata UI'ı | 3 | 2 | 2 | 2 | Raporlama |
| F12 `scan` komutunu gerçek tarama profiliyle bağla | 3 | 3 | 3 | 0 | Konsol/UX |
| Opt-in crash telemetry | 3 | 3 | 3 | 0 | Operasyonel |
| Periyodik arka plan tarama ve e-posta/rapor üretimi | 4 | 4 | 4 | 0 | Büyük özellik |
| Yeni kodda MVVM standardı, eski kodda kademeli adaptasyon | 3 | 4 | 3 | -1 | Mimari |

## 4. Yol Haritası

### 🔥 Şimdi (1-2 hafta)

1. **Test zeminini kur ve kritik saf fonksiyonları kilitle**
   - Neden: Cihaz Tara ve Update/Lisans tarafı değişirken manuel test tek başına regresyon yakalamaz.
   - Nereden başla: yeni test projesi; ilk hedefler `PortScanService.Parse`, `HistoryService.SonKayitlariYukle`, `UpdateService` hash/zip yardımcıları, CIDR çözme için çıkarılacak küçük helper.
   - Bitti tanımı: CI şartı olmadan lokal `dotnet test` çalışır; en az 25-40 düşük maliyetli unit test vardır; UI açmadan çalışır.
   - Tahmini efor: 1-2 gün.

2. **Cihaz Tara çekirdeğini küçük parçalara ayırmaya başla**
   - Neden: `MainWindow.DeviceScan.cs` 2159 satır; yeni özellik eklemek ve bug düzeltmek giderek pahalılaşıyor.
   - Nereden başla: `SubnetGirdisiniCoz`/`CidrAraligaCoz`, `KameraPorts`, export oluşturucular ve kimlik/güven skoru fonksiyonlarını UI'dan ayrılabilir sınıflara taşı.
   - Bitti tanımı: ilk etapta davranış değişmeden 3-4 saf sınıf oluşur; mevcut UI aynı çalışır; yeni testler bu sınıfları doğrular.
   - Tahmini efor: 2-4 gün.

3. **AI ürün sertleştirme turu**
   - Neden: AI artık chat, pcap, cihaz, Wi-Fi, port ve konsola yayılmış; tutarlılık ve güvenlik kararı gecikirse maliyet büyür.
   - Nereden başla: `AiDefaultKey` politikası, `AgTarama-AI/0.3.0` User-Agent, AI sonuçlarının history'ye tutarlı yazılması, AI istekleri için kullanıcıya cancel/timeout durumu.
   - Bitti tanımı: default key kararı uygulanmış; User-Agent sürümle uyumlu; pcap/cihaz/Wi-Fi/port AI sonuçları aynı history modeline kaydedilir; hata mesajları kullanıcı dostudur.
   - Tahmini efor: 1-2 gün.

4. **Ayarlar ve doküman drift'ini kapat**
   - Neden: kodda Wi-Fi AI ve EvilTwin eşiği var ama kullanıcı/doküman akışı tam değil; docs bazı yerde eski model bilgisi taşıyor.
   - Nereden başla: `SettingsWindow` içine Evil-Twin sinyal eşiği alanı; `docs/architecture.md` AI Faz 2 eski notu; `docs/ui.md` Wi-Fi AI butonu. Not: docs sadece kullanıcı “md güncelle” derse güncellenmeli.
   - Bitti tanımı: UI'dan eşik değişir; kaydedilen ayar `_ayarlar` ve Wlan taramasında tutarlı kullanılır; doküman güncellemesi kullanıcı onayıyla yapılır.
   - Tahmini efor: 2-4 saat kod, 1-2 saat doküman.

5. **Operasyonel minimum: crash log + log retention**
   - Neden: solo/manual test akışında üretim hatasını teşhis etmek için en hızlı geri dönüş budur.
   - Nereden başla: `App.xaml.cs` global exception handlerları; `LogService` seviye/retention; Ayarlar/Lisans ekranında “log klasörünü aç/kopyala” küçük aksiyonu.
   - Bitti tanımı: UI thread ve background exception loglanır; loglar örn. 14/30 gün tutulur; kullanıcı destek metninde son log yolu yer alır.
   - Tahmini efor: 4-8 saat.

### 🎯 Sonra (1-2 ay)

1. **Cihaz detay paneli ve güven skoru açıklaması**
   - Bağımlılık: Cihaz Tara çekirdeğinin en az kimlik/güven skoru kısmı ayrılmalı.
   - Neden: Kullanıcı sadece “Marka=Hikvision, Güven=80” değil, “hangi kaynaklarla tanındı?” bilgisinden değer alır.
   - Bitti tanımı: DataGrid satır seçimiyle sağ panel açılır; keşif kaynakları, açık portlar, banner, üretici ve önerilen aksiyonlar gösterilir.
   - Tahmini efor: 3-6 gün.

2. **Tarama profilleri**
   - Bağımlılık: Ayar modelinin küçük profil dosyası formatına ayrılması.
   - Neden: Tek geliştirici ve manuel test için de faydalı; kullanıcı aynı ağ/port/derin tarama kombinasyonunu tekrarlar.
   - Bitti tanımı: “Profil kaydet/yükle/sil”; subnet, port seti, derin tara, filtreler ve export tercihi saklanır.
   - Tahmini efor: 2-4 gün.

3. **Geçmiş karşılaştırma v2**
   - Bağımlılık: History metadata formatının stabil kalması.
   - Neden: Mevcut karşılaştırma chat metni; ürün değeri “ağda ne değişti?” raporunda.
   - Bitti tanımı: ayrı panel veya modal; yeni/kaybolan/değişen cihazlar tablo halinde; PDF/TXT export.
   - Tahmini efor: 3-5 gün.

4. **Native command adapter ve servis test edilebilirliği**
   - Bağımlılık: ilk test zemini.
   - Neden: `tshark`, `netsh`, `arp`, `tracert`, `nbtstat` çağrıları gerçek makineye bağlı; adapter test ve hata yönetimini kolaylaştırır.
   - Bitti tanımı: process çağrıları ortak adapterdan geçer; en az WlanService/InterfaceDiscovery/AdvancedIpScanner test doubles ile denenebilir.
   - Tahmini efor: 4-8 gün.

5. **AI doğal dil sorgu ve lokal bağlam**
   - Bağımlılık: AI history metadata tutarlılığı ve cihaz JSON formatı.
   - Neden: “Bu taramadaki riskli kameraları söyle” gibi ürün odaklı istekler chat'ten daha değerli.
   - Bitti tanımı: son cihaz taraması/son Wi-Fi taraması/son pcap analizi seçilip AI'ya bağlam olarak gönderilir; IP maskeleme uygulanır.
   - Tahmini efor: 3-6 gün.

6. **Release güvence akışı**
   - Bağımlılık: test zemini ve update doğrulama helperları.
   - Neden: Release prosedürü manuel ve hassas; `.zip` + `.sha256` hatası UpdateService'i boşa düşürür.
   - Bitti tanımı: lokal “release dry-run” komutu build, zip, sha, asset isim kontrolü ve UpdateService pattern uyumu doğrular; GitHub release yine kullanıcı kararıyla yapılır.
   - Tahmini efor: 1-3 gün.

7. **UX/erişilebilirlik pass'i**
   - Bağımlılık: ana akışlarda büyük refactor bitmeden de yapılabilir ama ideal olarak detay panelinden sonra.
   - Neden: Çok yoğun WPF ekranlarında keyboard navigation, focus, tooltip ve hata metinleri ürün hissini belirler.
   - Bitti tanımı: ana akışlar klavyeyle gezilir; kritik butonlarda access key/automation name; raw exception mesajları kullanıcı dostu metne çevrilir.
   - Tahmini efor: 2-5 gün.

### 🌅 Sonraki Ufuk (3+ ay)

1. **Kademeli MVVM standardı**
   - Karar: tam geçiş değil, yeni/yenilenen ekranlarda MVVM.
   - Neden: Tam geçiş haftalar alır; ama yeni cihaz detay paneli, profil yönetimi, geçmiş karşılaştırma gibi ekranlar MVVM için iyi aday.
   - Tahmini efor: dalga başına 1-3 hafta.

2. **Periyodik izleme ve ağ baseline**
   - Karar: masaüstü açıkken mi, Windows görev zamanlayıcısıyla mı?
   - Neden: Cihaz Tara + History zaten baseline üretmeye yakın; “ağda yeni cihaz var” ürün değeri yüksek.
   - Tahmini efor: 2-4 hafta.

3. **AI destekli anomali ve rapor otomasyonu**
   - Karar: lokal cihaz/pcap bağlamı mı, bulut proxy mi?
   - Neden: AI modülü artık çekirdek ürün parçası olabilir; maliyet/gizlilik kararları net olmalı.
   - Tahmini efor: 2-5 hafta.

4. **Discovery provider mimarisi**
   - Karar: mevcut servisler sade class olarak mı kalacak, yoksa `IDiscoveryProvider` modeli mi gelecek?
   - Neden: Ubiquiti, MNDP, SNMP, WSD, mDNS, HTTP-FP gibi kaynaklar artıyor; plugin değil ama provider listesi yönetilebilirlik sağlar.
   - Tahmini efor: 2-4 hafta.

5. **Opt-in crash/telemetry ve lisans operasyon paneli**
   - Karar: tamamen lokal destek paketi mi, anonim remote crash mi?
   - Neden: Solo geliştirici için gerçek kullanıcı hatalarını görmek kritik; lisans modeliyle güven dengesi iyi kurulmalı.
   - Tahmini efor: 1-3 hafta.

## 5. Karar Gereken Çatallar

### MVVM'e Geçiş

- Seçenek A: Tam MVVM geçişi. Artı: uzun vadeli temizlik. Eksi: mevcut ürün akışını haftalarca riske atar.
- Seçenek B: Yeni/yenilenen ekranlarda MVVM. Artı: risk kontrollü, solo geliştiriciye uygun. Eksi: bir süre karma stil yaşanır.
- Seçenek C: Partial devam. Artı: en hızlı. Eksi: DeviceScan benzeri dosyalar büyümeye devam eder.
- Önerim: Seçenek B. İlk adaylar cihaz detay paneli, profil yönetimi ve geçmiş karşılaştırma.

### Test Çatısı

- Seçenek A: xUnit servis/unit test. Artı: düşük maliyet, yüksek regresyon değeri. Eksi: UI hatalarını yakalamaz.
- Seçenek B: xUnit + birkaç WPF smoke/UI test. Artı: kritik ekran açılışları yakalanır. Eksi: Windows/CI kurulumu daha zahmetli.
- Seçenek C: Test yok, manuel devam. Artı: sıfır kurulum. Eksi: Cihaz Tara refactor riskli hale gelir.
- Önerim: Önce A. 1-2 ay sonra birkaç WPF smoke testi eklenir.

### Lisanslama Modeli

- Seçenek A: Mevcut sıkı model, 12 saat offline. Artı: güvenli ve lisans kaçaklarına dirençli. Eksi: NTP/internet problemi yaşayan kullanıcı sert blok yer.
- Seçenek B: 24-72 saat daha kullanıcı dostu offline pencere. Artı: destek yükü azalır. Eksi: güvenlik gevşer.
- Seçenek C: Lisans tipine göre tolerans. Artı: lifetime/subscription ayrımı yapılabilir. Eksi: backend ve test karmaşıklığı artar.
- Önerim: Ürün erken aşamada A veya C. Eğer destek şikayeti artarsa C'ye geç.

### AI Anahtar ve Maliyet Politikası

- Seçenek A: Bundled default key devam. Artı: kullanıcı kurar kurmaz AI çalışır. Eksi: anahtar sızma, kota ve maliyet riski.
- Seçenek B: Kullanıcı kendi anahtarını girer. Artı: maliyet/güvenlik net. Eksi: ilk deneyim zorlaşır.
- Seçenek C: Kendi proxy/lisans kontrollü AI gateway. Artı: merkezi kota, model kontrolü, abuse yönetimi. Eksi: backend maliyeti ve operasyon.
- Önerim: Kısa vadede A için kota ve kill-switch düşün; 1-2 ay içinde B veya C kararı ver.

### Dokümantasyon Politikası

- Seçenek A: Mevcut politika, sadece “md güncelle” ile docs güncellenir. Artı: kod işi sırasında doküman gürültüsü yok. Eksi: drift birikir.
- Seçenek B: Release öncesi zorunlu docs check. Artı: master index güncel kalır. Eksi: release öncesi ek iş.
- Seçenek C: Her özellik PR/commit'inde docs. Artı: en güncel. Eksi: solo developer hızını keser.
- Önerim: B. Günlük geliştirmede dokunma, release öncesi docs check listesi zorunlu olsun.

### Crash/Telemetry

- Seçenek A: Yalnız lokal log ve destek paketi. Artı: gizlilik güçlü. Eksi: kullanıcı göndermedikçe hata görünmez.
- Seçenek B: Opt-in anonim crash. Artı: gerçek hata görünürlüğü. Eksi: gizlilik metni, izin ve backend gerekir.
- Seçenek C: Telemetry yok. Artı: basit. Eksi: üretim sorunları kör kalır.
- Önerim: Önce A; kullanıcı tabanı büyürse B.

### Release Paket Adlandırma

- Seçenek A: `AgTarama-vX.Y.Z.zip` devam. Artı: AGENTS prosedürüyle uyumlu. Eksi: mimari/platform bilgisi yok.
- Seçenek B: `AgTarama-vX.Y.Z-win-x64.zip`. Artı: UpdateService pattern'i destekliyor, platform net. Eksi: prosedür güncellemesi gerekir.
- Önerim: A kısa vadede yeterli; publish mimarisi çoğalırsa B'ye geç.

## 6. İlk Adım Önerisi

Kullanıcı “evet başla” derse ilk görev: **servis odaklı test zemini + Cihaz Tara saf helper çıkarımı**.

Neden bu? Çünkü `MainWindow.DeviceScan.cs` ürünün en değerli ama en riskli dosyası. Refactor yapmadan önce `PortScanService.Parse`, CIDR çözümleme, history compare JSON okuma ve update hash parsing gibi düşük maliyetli alanları testle kilitlemek, sonraki 1-2 aylık işi daha güvenli hale getirir. İlk PR/commit küçük tutulmalı: test projesi, 3-4 helper çıkarımı, davranış değişikliği yok, manuel UI akışı aynı.
