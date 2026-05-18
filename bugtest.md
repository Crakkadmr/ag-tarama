# Bug Test Etki Raporu

Tarih: 2026-05-18
Kapsam: AgTarama v0.4.0, son 5 committe eklenen AI chat, pcap AI analizi, cihaz AI raporu, Wi-Fi/port AI yorumlari ve ilgili lifecycle/config kodlari.

Bu rapor kod okuma ile hazirlandi. Dosya:satir referanslari mevcut kaynak agacindaki satir numaralarina gore verilmistir.

## Kisa Ozet

| Oncelik | Bulgu | Etkilenen ana bolumler |
|---|---|---|
| P0 | Varsayilan AI API anahtari kaynak koddan cikarilabilir | AI altyapisi, ayarlar, tum AI ozellikleri, maliyet/kota guvenligi |
| P1 | Custom AI base URL HTTP kabul ediyor | Ayarlar > AI, AI chat, pcap analizi, cihaz analizi, Wi-Fi/port AI |
| P1 | Update imza dogrulamasi varsayilan bypass | Guncelleme sistemi, release dagitimi, uygulama butunlugu |
| P1 | AI token sayaci kilitsiz | AI kota/usage takibi, paralel AI istekleri |
| P1 | AI iptali hata gibi donuyor | AI chat, pcap AI, Wi-Fi/port AI, lisans iptali lifecycle |
| P1 | Harici process cleanup eksikleri | Pcap yakalama/analiz, Wi-Fi tarama, tshark/netsh surecleri |
| P1 | Wi-Fi adaptor kontrolu UI thread'i blokluyor | Uygulama acilisi, Wi-Fi sekmesi |
| P2 | Cihaz AI modalinda iptal yok | Cihaz Tara > AI rapor penceresi |
| P2 | Konsol AI onerisi iptal edilmiyor | F12 konsol, AI komut onerisi, kota kullanimi |
| P2 | CIDR /31-/32 parser tarafindan reddediliyor | Cihaz Tara subnet girisi, edge network taramalari |

## Etkilenen Bolumler

### 1. AI Altyapisi ve API Anahtari Yonetimi

**Etkilenen dosyalar**
- `Services/Ai/AiDefaultKey.cs:7`
- `Services/Ai/AiKeyVault.cs:14`
- `MainWindow.xaml.cs:180`
- `SettingsWindow.xaml.cs:163`
- `Services/Ai/AiClient.cs:116`

**Etkilenen islevler**
- AI anahtarinin ilk calistirmada otomatik yuklenmesi
- AI saglayici/base URL ayarlari
- Tum AI istekleri: chatbot, pcap analizi, cihaz analizi, Wi-Fi raporu, port yorumu, F12 AI onerisi

**Risk**
- P0: Default OpenRouter key XOR ile kaynak/binary icinden geri elde edilebilir.
- P1: Custom provider icin `http://` kabul edildiginde Bearer API key acik metin tasinabilir.

**Kanita dayali referans**
- `AiDefaultKey.Get()` encoded byte dizisini `XorKey` ile cozer.
- `MainWindow()` icinde `AiKeyVault.EnsureDefaultKey()` cagriliyor.
- `SettingsWindow` base URL icin sadece bosluk kontrolu yapiyor.
- `AiClient` bu URL'ye `Authorization: Bearer` basligi gonderiyor.

**Beklenen duzeltme yonu**
- Client icindeki default key kaldirilmali.
- Base URL `Uri.TryCreate` ile dogrulanmali ve `https` zorunlu olmali.

### 2. AI Kota ve Usage Takibi

**Etkilenen dosyalar**
- `Services/Ai/AiUsageMeter.cs:42`
- `Services/Ai/AiClient.cs:72`

**Etkilenen islevler**
- Gunluk/aylik AI token limiti
- Paralel AI istekleri
- Maliyet kontrolu

**Risk**
- P1: `Load -> kontrol -> istek -> Load -> AddUsage -> Save` akisi kilitsiz. Iki AI istegi ayni anda tamamlanirsa eski snapshot uzerine yazarak kullanim dusuk kaydedilebilir.

**Etkilenen kullanici aksiyonlari**
- Chatbot sorusu devam ederken pcap AI analizi baslatmak
- Cihaz AI raporu ve Wi-Fi AI raporunu paralel calistirmak
- Port AI yorumunu diger AI istekleriyle ayni anda kullanmak

**Beklenen duzeltme yonu**
- Limit kontrolu ve usage ekleme tek atomik kritik bolge olmali.
- Dosya yazimi temp dosya + replace ile atomic hale getirilmeli.

### 3. AI Iptal ve Lifecycle Davranisi

**Etkilenen dosyalar**
- `Services/Ai/AiClient.cs:141`
- `Partials/MainWindow.Ai.cs:85`
- `AiDeviceReportWindow.xaml.cs:92`
- `Partials/MainWindow.Console.cs:109`

**Etkilenen islevler**
- Chatbot AI istegi
- Cihaz AI modal penceresi
- F12 konsol Ctrl+Tab AI onerisi
- Lisans iptali veya uygulama kapanisi sirasinda devam eden AI istekleri

**Risk**
- P1: `TaskCanceledException`, token iptal edilmisse bile genel `catch (Exception)` icine dusup hata yanitina cevriliyor.
- P2: Cihaz AI modalinda window-level CTS yok; pencere kapansa da istek devam ediyor.
- P2: F12 AI onerisi `_ = AiKomutOneriAsync(...)` ile fire-and-forget basliyor ve Esc ile iptal edilmiyor.

**Etkilenen kullanici aksiyonlari**
- AI istegi surerken lisansin gecersiz hale gelmesi
- Cihaz AI rapor penceresini analiz surerken kapatmak
- F12 konsolda Ctrl+Tab'e arka arkaya basmak, sonra Esc yapmak

**Beklenen duzeltme yonu**
- Kullanici/lifecycle iptallerinde `OperationCanceledException` propagate edilmeli.
- Modal ve konsol AI akislari kendi CTS'leriyle iptal edilebilir olmali.
- Fire-and-forget AI task'lari guard/await edilen akisa alinmali.

### 4. Pcap Yakalama ve Pcap AI Analizi

**Etkilenen dosyalar**
- `Services/Ai/AiPcapAnalyzer.cs:74`
- `Services/CaptureService.cs:55`
- `Partials/MainWindow.Capture.cs:347`

**Etkilenen islevler**
- `tshark` ile pcap istatistik toplama
- Yakalama durdurma
- AI ile pcap analiz et butonu

**Risk**
- P1: `RedirectStandardError=true` olmasina ragmen stderr okunmayan surecler var.
- P1: Iptal sirasinda `WaitForExitAsync(ct)` firlarsa `tshark` child process oldurulmuyor.
- P1: `CaptureService.YakalaAsync` token iptalinde loop'tan cikiyor ama kendi icinde process cleanup garantisi vermiyor.

**Etkilenen kullanici aksiyonlari**
- Pcap AI analizi baslatip uygulama/lisans iptali ile token cancel olmasi
- Yakalama sirasinda durdurma
- `tshark` stderr'i dolu veya hatali pcap dosyasi analizi

**Beklenen duzeltme yonu**
- stdout ve stderr birlikte drain edilmeli.
- Iptal/finally yolunda `Kill(entireProcessTree: true)` + `WaitForExitAsync(CancellationToken.None)` uygulanmali.

### 5. Wi-Fi Tarama ve Uygulama Acilisi

**Etkilenen dosyalar**
- `Services/WlanService.cs:172`
- `MainWindow.xaml.cs:180`
- `Partials/MainWindow.Wlan.cs:31`

**Etkilenen islevler**
- Uygulama acilisi
- Wi-Fi sekmesinin aktif/pasif belirlenmesi
- Wi-Fi tarama ve AI rapor

**Risk**
- P1: `MainWindow` constructor icinde `WlanPanelBaslat()` calisiyor; bu metot senkron `netsh` calistirip `WaitForExit()` yapiyor. `netsh` gecikirse WPF UI thread acilista donar.
- P1/P2: `ScanAsync` iptalinde child process cleanup garantisi yok.

**Etkilenen kullanici aksiyonlari**
- Uygulamayi Wi-Fi adaptoru sorunlu veya netsh'in yavas oldugu bir sistemde acmak
- Wi-Fi taramayi iptal etmek

**Beklenen duzeltme yonu**
- Wi-Fi adaptor kontrolu async, timeout'lu ve `Loaded` sonrasi yapilmali.
- `ScanAsync` iptal yolunda process kill/finally cleanup eklenmeli.

### 6. Guncelleme ve Release Guvenligi

**Etkilenen dosyalar**
- `Services/UpdateService.cs:251`

**Etkilenen islevler**
- GitHub Releases update kontrolu
- ZIP indirme ve restart ile kurulum
- Release dagitim zinciri

**Risk**
- P1: `AGT_UPDATE_SIGNER_THUMBPRINT` env var yoksa imza dogrulama `true` donuyor. ZIP hash'i de ayni release asset kaynagindan gelen `.sha256` dosyasina bagli.

**Etkilenen kullanici aksiyonlari**
- Uygulama yeni release indirip kurdugunda

**Beklenen duzeltme yonu**
- Imza dogrulamasi varsayilan zorunlu olmali.
- Beklenen thumbprint uygulama icinde veya guvenli config'te pinlenmeli.

### 7. Cihaz Tara ve AI Cihaz Raporu

**Etkilenen dosyalar**
- `Partials/MainWindow.DeviceScan.cs:1290`
- `AiDeviceReportWindow.xaml.cs:92`
- `Services/Ai/AiDeviceAnalyzer.cs:50`

**Etkilenen islevler**
- Cihaz Tara sonuc listesinden AI raporu acma
- AI rapor penceresi presetleri
- "Bu IP'leri yeniden tara" akisi

**Risk**
- P2: AI modal analizinde token verilmedigi icin pencere kapaninca istek devam ediyor.
- P2: `AnalizeBasla` event handler olmayan `async void`; exception/lifecycle yonetimi zayif.

**Etkilenen kullanici aksiyonlari**
- AI cihaz raporu baslatip pencereyi kapatmak
- AI yanitindan IP yeniden tarama baslatmak

**Beklenen duzeltme yonu**
- Modal icin CTS eklenmeli; `Closed` eventinde cancel edilmeli.
- `AnalizeBaslaAsync` `Task` donmeli, buton handler'lari tarafindan await edilmeli.

### 8. F12 Komut Konsolu

**Etkilenen dosyalar**
- `Partials/MainWindow.Console.cs:52`
- `Partials/MainWindow.Console.cs:109`
- `Services/CommandRouter.cs:204`

**Etkilenen islevler**
- Komut calistirma
- Ctrl+Tab AI komut onerisi
- Esc ile iptal
- `ssl`, `web`, `snmp`, `banner` komutlari

**Risk**
- P2: Ctrl+Tab AI onerileri konsol CTS'sine bagli degil; Esc sadece `_konsoleCts` iptal ediyor.
- Incelendi: `ssl` komutu sertifika dogrulamasini bilerek bypass ediyor, ancak bu komut sertifika bilgisi toplama amacli gorunuyor; update veya AI HTTP guvenligiyle karistirilmamali.

**Beklenen duzeltme yonu**
- AI onerisi de konsol CTS'sine baglanmali.
- Ayni anda birden fazla AI onerisi baslatilmasi engellenmeli.

### 9. Network Logic / Subnet Parser

**Etkilenen dosyalar**
- `Partials/MainWindow.DeviceScan.cs:616`
- `Partials/MainWindow.DeviceScan.cs:671`

**Etkilenen islevler**
- Cihaz Tara subnet/CIDR girisi
- `/31` point-to-point ve `/32` tek host edge case'leri

**Risk**
- P2: Parser `mask is < 16 or > 30` ile `/31` ve `/32` girislerini reddediyor; fakat alttaki cozumleyicide `/31` ve `/32` branch'leri var. Kod ile davranis tutarsiz.

**Etkilenen kullanici aksiyonlari**
- `192.168.1.10/32` veya `192.168.1.10/31` girerek cihaz tarama baslatmak

**Beklenen duzeltme yonu**
- Ya parser `/16..32` kabul etmeli ya da unreachable `/31`/`/32` kodu ve dokumani kaldirilmali.

## Kategori Bazli Durum

| Kategori | Durum | En onemli etki |
|---|---|---|
| Concurrency & Async | Sorun var | AI usage race, AI iptal semantigi, fire-and-forget konsol AI |
| Resource Management | Sorun var | tshark/netsh process cleanup ve modal AI lifecycle |
| Null Safety & Exception | Sinirli sorun var | Iptalin genel exception olarak hata mesajina cevrilmesi |
| Guvenlik | Yuksek risk var | Default AI key, HTTP Bearer, optional update signer |
| AI Modulu | Yuksek risk var | Ana bug yogunlugu bu bolumde |
| WPF / UI | Sorun var | Acilista UI freeze, modal kapatma lifecycle |
| Network Logic | Edge bug var | `/31`/`/32` tarama tutarsizligi |
| Build / Config / Lifecycle | Sorun var | Update trust chain ve uygulama kapanisinda aktif isler |

## Ilk 5 Duzeltme Onceligi

1. Client icindeki default AI key'i kaldir.
2. AI base URL icin HTTPS zorunlulugu getir.
3. Update imza dogrulamasini varsayilan zorunlu yap.
4. `AiUsageMeter` limit kontrolu + usage yazimini atomik hale getir.
5. AI cancellation ve harici process cleanup yollarini duzelt.

