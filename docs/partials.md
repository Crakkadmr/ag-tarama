# MainWindow Partial Dosya Haritası

> Satır numaraları ±20 satır toleransla doğrudur. Kod değişiklikleri sonrası eskime olabilir.
> "md güncelle" talimatı geldiğinde haritalar yeniden hesaplanır.
> Harita ile gerçek dosya arasında büyük sapma görülürse dosyayı `offset` yerine baştan oku ve haritayı güncelle.

`MainWindow.xaml.cs` + 10 partial dosya derleyici tarafından tek `MainWindow` sınıfında birleştirilir.
Cross-partial metot çağrıları sorunsuz çalışır (örn. `MesajEkle` NetworkTools'ta tanımlı, her partial'dan çağrılabilir).

---

## MainWindow.xaml.cs — Ana Partial (354 satır)

| Satır | İçerik |
|---|---|
| L1–L28 | using/namespace |
| L29–L161 | Alanlar, sabitler (TabChatbot..TabLisans), `OuiTablosu`, `BilindikPortlar` |
| L163–L182 | `OuiAra(mac) static` |
| L183–L251 | `MainWindow()` constructor, `BaslangicAsync`, `HataBildir`, `NpcapKurulumu`, `NpcapKontrolVeKur` |
| L252–L349 | `ArayuzSecimAsync` — ChatPanel'e toggle butonlu kart, kullanıcı arayüz seçer |
| L351–L354 | `UygulaButonSablon(Button)` static |

**Alanlar özeti (L29-L161):**
`_ayarlar`, `_taramaDevamEdiyor`, `_taramaCts`, `LisansIptal`, `MasterCts`, sekme sabitleri (TabChatbot=0..TabWlan=10, TabLisans=11), `_pingCts`, `_portScanCts`, `_traceCts`, `_kameraCts`, `_kameraBilgileri`, `_kameraSatirlari`, `_kameraSatirlar`, `_kameraSatirView`, `_bantTimer`, `_bantOnceki`, `_bantAralikSn` (int, default 300), `_toastTimer`, `_mesajGecmisi`, `_gecmisKayitlari`, `_gecmisFiltreTur`, `_gecmisdenCalistiriliyor`, `_captureService`, `_otomatikGuncelleniyor`, `_oncekiUzunluk`, `_lisansBannerGizle` (bool), `_konsoleCts`, `_konsoleCalistiriliyor` (bool), `_konsoleGecmisIndex` (int)

---

## Partials/MainWindow.Capture.cs (351 satır)

| Satır | İçerik |
|---|---|
| L1–L19 | using/namespace |
| L20–L326 | `YakalamaBaslat()` — InterfaceDiscovery → ArayuzSecim → pcap oluştur → CaptureService.YakalaAsync → `YakalamaKartiOlustur` |
| L327–L334 | `YakalamaDurdur()` |
| L335–L351 | `WiresharkIleAc(string pcap)` |

**`YakalamaKartiOlustur`** tuple döner: `Kart`, `Guncelle(mb,paket,sure)`, `Tamamla(mb,paket)`, `Durdur()`.

---

## Partials/MainWindow.NetworkTools.cs (835 satır)

| Satır | İçerik |
|---|---|
| L1–L24 | using/namespace |
| L26–L100 | `MesajEkle(tur, metin)` — Border+TextBlock chat mesajı |
| L101–L136 | `TaramaDurumunuAyarla`, `BtnTaramaBaslat_Click`, `BtnTaramaDurdur_Click`, `MainTabControl_SelectionChanged` |
| L137–L315 | Ping: `BtnPing_Click`, `GecerliIpv4Mu`, `GecerliHostnameMu`, `OtomatikNoktaUygula`, `PingIpBox_TextChanged`, `PingBaslat`, `PingKutucugaYaz`, `PingFavoriEkle_Click` |
| L317–L524 | Port tara: `BtnPortTara_Click`, `PortIpBox_TextChanged`, `PortAralikBox_TextChanged`, `AktarButonDurumu`, `PortHizliBtn_Click`, `PortTaraBaslat`, `PortKutucugaYaz` |
| L525–L699 | Traceroute: `TracerouteBaslat`; DNS: `DnsLookupBaslat`, `DnsKutucugaYaz` |
| L700–L762 | WoL: `WolMacBox_TextChanged`, `WolGonder` |
| L763–L794 | `AgAdaptorleriniGoster` — `NetworkInterface.GetAllNetworkInterfaces()` → chat kart |
| L795–L835 | `ArpTablosuGoster` — `arp -a` parse → chat kart + OUI |

**`TaramaDurumunuAyarla(bool)`:** Buton durumları + `StatusText` ("● Hazır" yeşil / "● Yakalanıyor..." sarı). `BtnTemizle` tarama sırasında `IsEnabled=false`.

---

## Partials/MainWindow.DeviceScan.cs (1636 satır)

| Satır | İçerik |
|---|---|
| L1–L27 | using/namespace |
| L28–L73 | `KameraBilgi` sealed class (tüm alanlar), `CihazKimlik` sealed class |
| L75–L313 | `MarkaTablosu` static array (40+ HTTP banner/title anahtar → marka/tür) |
| L314–L409 | `KimlikBelirle(KameraBilgi) static`, `KayitCihaziIpuclariVar`, `YaziciIpuclariVar`, `CihazAdiBilgisayarGibi` |
| L410–L526 | DataGrid event handler'ları: filtre text changed, tür filtresi, filtre temizle, çift tık, sağ tık |
| L527–L617 | `KameraWebArayuzunuAc`, `KameraDisariAktar`, `DisariAktarilanDosyayiAc`, `SeciliKameraSatiri`, `UstOgeBul` |
| L619–L826 | Export format metotları: `IpSiralamaAnahtari`, `KameraExportSatirlari`, `KameraCsvOlustur`, `KameraJsonOlustur`, `KameraTxtOlustur`, `KameraExcelXlsxOlustur` (ClosedXML → `.xlsx`), `KameraPdfQuestOlustur` (QuestPDF → `PdfReportService`), `MetniKirp` |
| L827–L830 | `KameraBaslatBtn_Click`, `KameraDurdurBtn_Click` |
| L830–L900 | `NetbiosBilgileriniGuncelleAsync`, `NetbiosSweepAsync` |
| L886–L994 | `MdnsServisler` static array, `MdnsSweepAsync`, `OlusturMdnsSorgusu`, `MdnsPaketCoz` |
| L995–L1247 | **`KameraTaramaBaslat()`** — 4 paralel görev: port tarama (SemaphoreSlim 80), ONVIF WS-Discovery, SSDP+mDNS, Ping Sweep; + NetBIOS/AIS/ARP zenginleştirme |
| L1248–L1490 | Async yardımcılar: `HttpBannerOku`, `ServisDetaylariniGuncelleAsync`, `PortBannerOku`, `RtspHizliKontrol`, `HttpBasliklariniParse`, `SsdpDetayOku`, `XmlEtiketiOku`, `AdvancedScannerKayitlariniIsleAsync`, `ArpBilgileriniTopluGuncelleAsync`, `ArpTablosuOkuAsync`, `UreticiAra`, `IpScannerMacDbYukle`, `MacFormatla` |
| L1491–L1576 | `KameraKartEkleVeyaGuncelle`, `KameraFiltreleriUygula`, `KameraSatirFiltredenGecer`, `KameraKutucugaYaz`, `CihazAdiSec`, `IlkDolu`, `KisaHostAdi`, `AnlamliSayfaBasligi`, `TemizKimlikMetni` |
| L1578–L1636 | `KameraSatir` sealed class (INotifyPropertyChanged) — DataGrid görünüm modeli |

**`KameraTaramaBaslat` paralel görevler (L995):**
1. Port tarama — 1–254 IP, SemaphoreSlim(80), 800ms, `KameraPorts` = {554,8000,8080,37777,80,8443,22,23,139,443,445,3389,9000,34567}
2. ONVIF WS-Discovery — `239.255.255.250:3702` Probe XML → 4sn ProbeMatch
3. SSDP/UPnP + mDNS/Bonjour — `239.255.255.250:1900` M-SEARCH + `224.0.0.251:5353`
4. Ping Sweep — SemaphoreSlim(64), 1000ms

**Cihaz Tara DataGrid sütunları:** IP, Ad, Tür, Marka, Model, Ping, Portlar, Keşif, MAC, Üretici, Servis
**Dışa aktarma formatları:** Excel (`.xlsx` ClosedXML), PDF (QuestPDF A4 Yatay), TXT, CSV (UTF-8 `;`), JSON — sağ tık menüsünden

---

## Partials/MainWindow.Bandwidth.cs (347 satır — #10 ile yeniden yazıldı)

| Satır | İçerik |
|---|---|
| L1–L11 | using/namespace |
| L17–L20 | `BtnBant_Click`, `BantPanelKapat_Click` — sekme geçişi |
| L28–L39 | `BantAralikBtn_Click` — `_bantAralikSn` güncelle, buton stilini ChipButton/ActiveActionButton arasında geçir |
| L41–L147 | `BantPerAppBtn_Click` — UAC kontrolü → `netstat -bno` çalıştır → `[Process.exe]` satırları parse et → top 5 `BantPerAppPanel`'de göster |
| L149–L168 | `BantIzlemeBaslat()` — snapshot al, DispatcherTimer(1s) başlatır |
| L170–L244 | `BantTimerTick()` — `GetIPv4Statistics()`, hız hesabı, `BandwidthHistoryService.RecordTick()` çağrısı, adaptör kartları |
| L246–L258 | `BantGrafigiVeStatlariGuncelle()` — grafik çiz + stat TextBlock'ları güncelle |
| L260–L336 | `BantGrafiginiCiz()` — Canvas temizle, grid çizgileri (40-alpha mavi kesikli), Rx Polyline (mavi `#58A6FF`), Tx Polyline (yeşil `#3FB950`), max etiket, legend |
| L338–L339 | `BantGrafikCanvas_SizeChanged` — `BantGrafiginiCiz()` |
| L341–L347 | `BantHizFormatla(long) static` — `≥1MB/s`, `≥1KB/s`, `B/s` |

**Yeni XAML öğeleri:** `BantBtn5dk`, `BantBtn15dk`, `BantBtn60dk`, `BantStatPeakRx/Tx`, `BantStatAvgRx/Tx`, `BantStatToplam`, `BantGrafikCanvas` (SizeChanged), `BantPerAppPanel`

---

## Partials/MainWindow.Console.cs (yeni — #13)

| Satır | İçerik |
|---|---|
| — | `KonsoleBaslat()` — `Window.KeyDown` olayına `Window_KonsoleKeyDown` ekler (F12 dinler) |
| — | `KonsoleToggle()` — `ConsolePanel` göster/gizle, `ConsoleInput` focus, hoş geldin mesajı |
| — | `ConsoleInput_KeyDown` — Enter: çalıştır; ↑/↓: `_konsoleGecmisIndex` geçmiş gezimi; Tab: autocomplete; Esc: CancellationToken iptal |
| — | `KonsoleYaz(string)` — `ConsoleOutput.AppendText` + ScrollToEnd |

**ConsolePanel XAML yapısı:** Dış `Grid` (Grid.Row=1 iç grid) → `ConsoleScrollViewer` + `ConsoleOutput` (TextBox, IsReadOnly) + `ConsoleInput` (TextBox, KeyDown handler). Panel `TabControl` template'ının **dışındadır** — code-behind'dan doğrudan erişilebilir.

**Önemli kısıt:** x:Name öğeleri ControlTemplate içine konulursa code-behind'dan erişilemez. Console öğeleri TabControl'ün dışında ayrı Grid satırındadır.

---

## Partials/MainWindow.Favorites.cs (133 satır)

| Satır | İçerik |
|---|---|
| L1–L19 | using/namespace |
| L20–L70 | `FavoriChipleriniYenile()` — Ping ve Port panellerindeki chip'leri `FavoriService.YukleHepsi()` ile yeniler |
| L71–L133 | `FavorilerPanelGuncelle()` + favori event handler'ları (ekle/sil) |

---

## Partials/MainWindow.History.cs (235 satır)

| Satır | İçerik |
|---|---|
| L1–L17 | using/namespace |
| L18–L55 | Event handler'lar: `GecmisYenile_Click`, `GecmisKlasorAc_Click`, `GecmisFiltreBtn_Click`, `GecmisKaydiSil`, `GecmisTumunuTemizle_Click` |
| L56–L91 | `GecmisPanelGuncelle()` — filtre + kart listesi oluşturma |
| L92–L153 | `GecmisKartiOlustur(HistoryRecord)` — JSON Aç, Tekrar Çalıştır, Sil chip'leri |
| L154–L187 | `GecmisKaydiAc`, `GecmisKaydiTekrarCalistir` — `_gecmisdenCalistiriliyor` flag ile çift kayıt önlenir |
| L188–L235 | `GecmisKarsilastir_Click`, `GecmisCihazIpSeti` — son iki Cihaz Tara'yı karşılaştırır |

---

## Partials/MainWindow.UI.cs (141 satır)

| Satır | İçerik |
|---|---|
| L1–L19 | using/namespace |
| L20–L60 | `BtnAyarlar_Click` (SettingsWindow), `RaporKaydet` (SaveFileDialog → .txt) |
| L61–L100 | `Window_DragOver`, `Window_Drop` — Drag-Drop desteği |
| L101–L141 | `ToastGoster(mesaj, hata)`, `BildirimCal` |

---

## Partials/MainWindow.Wlan.cs (~180 satır)

| Satır | İçerik |
|---|---|
| L1–L10 | using/namespace |
| L11–L18 | Alanlar: `_wlanCts`, `_wlanSatirlar` (ObservableCollection), `_wlanOtoTimer`, `_wlanSayac` (int), `_wlanAdaptorVar` (bool) |
| L20–L30 | `WlanPanelBaşlat()` — `WlanGrid.ItemsSource` bağla, `WifiAdaptorVarMi()` kontrol; adaptör yoksa `WlanTab.IsEnabled=false` + ToolTip |
| L32–L50 | `WlanTaraBtn_Click`, `WlanDurdurBtn_Click`, `WlanOtoYenile_Changed` |
| L52–L80 | `WlanOtoTimerBaslat()` / `WlanOtoTimerDurdur()` — DispatcherTimer(1s), geri sayım WlanSayacText |
| L82–L130 | `WlanTaramaBaslat()` — CTS, buton durumları, `WlanService.ScanAsync`, `_wlanSatirlar` güncelle, Evil-Twin toast |
| L132–L180 | `WlanSatir` sealed class — `Ssid`, `Bssid`, `Auth`, `Encryption`, `Signal`, `Channel`, `RadioType`, `EvilTwin`, `DurumMetni`, `DurumRenk` (constructor'da hesaplanır) |

**Durum renk mantığı (`WlanSatir`):**
- WPA2/WPA3 → yeşil `#3FB950` + "✓ Güvenli"
- WPA (1. nesil) → sarı `#E3B341` + "⚠ Orta"
- WEP / Open → kırmızı `#F85149` + "✖ Güvensiz"
- Evil-Twin → sarı `#E3B341` + "⚠ Evil-Twin" (öncelikli)

**XAML öğeleri:** `WlanTab` (x:Name, IsEnabled kontrolü için), `WlanPanel`, `WlanDurumText`, `WlanTaraBtn`, `WlanDurdurBtn`, `WlanOtoYenileCheck`, `WlanSayacText`, `WlanGrid` (DataGrid)

---

## Partials/MainWindow.License.cs (204 satır — #14 ile genişletildi)

| Satır | İçerik |
|---|---|
| L1–L5 | using/namespace |
| L9–L18 | `LisansPanelGuncelle()` — önbellek kontrolü → `SetLisansUI` |
| L20–L127 | `SetLisansUI(LicenseStatus, string, LicenseInfo?)` — durum kartı (geçerli/süresi doldu/geçersiz), kalan süre hesabı, sticky banner (7 günden az kaldıysa), MachineId (ilk 8 karakter), son online doğrulama UTC, NTP zamanı |
| L129–L140 | `MaskeLisansAnahtari(string)` — `****-****-SON4` formatı |
| L142–L159 | `LisansYenile_Click` — `LicenseService.ValidateAsync()` çağrısı |
| L161–L172 | `LisansSifirla_Click` — onay diyalogu + `LicenseService.ClearCache()` |
| L174–L178 | `LisansBannerKapat_Click` — `_lisansBannerGizle = true`, banner gizle |
| L180–L204 | `LisansKopyala_Click` — destek metni (durum, tür, bitiş, MachineId 16-char, son doğrulama, NTP) → `Clipboard.SetText` |

**Yeni XAML öğeleri:** `LisansBanner` (sticky, Grid.Row=1), `LisansBannerMetin`, `LisansBannerKapatBtn`, `LisansSonDogrulamaMetin`, `LisansNtpMetin`, "📋 Lisans Bilgilerini Kopyala" butonu
