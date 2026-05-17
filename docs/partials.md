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

## Partials/MainWindow.DeviceScan.cs (~2340 satır — v0.3.0 CIDR enumerator)

| Satır | İçerik |
|---|---|
| L1–L27 | using/namespace |
| L28–L86 | `KameraBilgi` sealed class — 30+ alan (Ubiquiti, MikroTik, SNMP, HTTP-FP, WSD, `KesifKaynaklari HashSet<string>`), `CihazKimlik` sealed class |
| L88–L220 | `KameraPorts` array, `MarkaTablosu` static array (~100 anahtar kelime → marka/tür) |
| L222–L420 | `KimlikBelirle(KameraBilgi) static` — vendor-specific yüksek güven kaynakları → mDNS → yazıcı/NVR/PC heuristikleri → MarkaTablosu → port-based fallback → TTL fallback |
| L422–L520 | `GuvenSkoru(b, k) static`, `CihazAdiBilgisayarGibi`, `KayitCihaziIpuclariVar`, `YaziciIpuclariVar`, `CihazAdiSec`, `IlkDolu`, `KisaHostAdi`, `AnlamliSayfaBasligi`, `TemizKimlikMetni` |
| L517–L720 | `TaramaSubneti` (v0.3.0: `HostStart`/`HostEnd`/`HostCount`/`OriginalCidr`), `YerelSubnetleriBul`, `NicSubneti` record, `YerelNicSubnetleriniBul`, `YerelSubnetiBul`, `SanalAdaptorMu`, `SubnetGirdisiniCoz` + `CidrAraligaCoz` (/16-/23 → çoklu /24; /25-/30 → kısıtlı aralık) |
| L641–L759 | `_subnetBoxChipSenkronu` bool, `BtnKamera_Click`, `KameraNicYenileBtn_Click`, `KameraNicChipleriniYenile(bool)`, `KameraChipOlustur(NicSubneti)`, `KameraChipDegisti`, `KameraChipleriSenkronizeEt` |
| L760–L984 | DataGrid event handler'ları: panel kapat, subnet textbox sync, kolon filtre, tür filtresi, filtre temizle, çift tık, sağ tık; `KameraMenuYenidenTara_Click`, `TekIpTaraAsync(ip)` |
| L985–L1210 | Sağ tık menü aksiyon handler'ları (web, ping, port, trace, dns, kopyala, favori, export); `KameraWebArayuzunuAc`, `KameraDisariAktar`, `KameraGorunenSatirlariAl` |
| L1084–L1210 | Export: `IpSiralamaAnahtari`, `KameraExportSatirlari`, `KameraCsvOlustur`, `KameraJsonOlustur`, `KameraTxtOlustur`, `KameraExcelXlsxOlustur`, `KameraPdfQuestOlustur`, `MetniKirp` |
| L1212–L1213 | `KameraBaslatBtn_Click`, `KameraDurdurBtn_Click` |
| L1215–L1271 | `NetbiosBilgileriniGuncelleAsync`, `NetbiosSweepAsync` |
| L1272–L1392 | `MdnsServisler` static array (25 servis), `MdnsSweepAsync`, `OlusturMdnsSorgusu`, `MdnsPaketCoz` |
| L1394–L1736 | **`KameraTaramaBaslat()`** — 4+3 paralel görev; `derinTara` flag kontrolü; `KesifKaynaklari` güncelleme |
| L1737–L1813 | `UbiquitiSweepAsync`, `MndpSweepAsync`, `SnmpSweepAsync` — ek keşif protokolleri |
| L1814–L1950 | `HttpBannerOku` (HTTP 200 kontrolü), `ServisDetaylariniGundelleAsync`, `PortBannerOku`, `BannerTemizle`, `RtspHizliKontrol` |
| L1913–L1990 | `HttpBasliklariniParse`, `SsdpDetayOku`, `XmlEtiketiOku`, `AdvancedScannerKayitlariniIsleAsync` |
| L1971–L2065 | `ArpBilgileriniTopluGuncelleAsync` (OUI fallback), `ArpTablosuOkuAsync`, `UreticiAra`, `IpScannerMacDbYukle`, `MacFormatla` |
| L2066–L2185 | `KameraKartEkleVeyaGuncelle`, `KesifSira()`, `KameraWebUrlSec` (snapshot lock), `KameraFiltreleriUygula`, `KameraSatirFiltredenGecer`, `Icerir`, `KameraKutucugaYaz` |
| L2186–L2245 | `KameraSatir` sealed class (INotifyPropertyChanged) — `Guven` (int) dahil tüm alanlar |

**`KameraTaramaBaslat` paralel görevler (L1394):**
1. Port tarama — `hostStart..hostEnd` IP (v0.3.0: önceden sabit 1-254), SemaphoreSlim(80), 800ms, `KameraPorts` = {554,8000,8080,37777,80,8443,22,23,139,443,445,3389,9000,34567}; `HttpFingerprintService.ProbeAsync` derin modda
2. ONVIF WS-Discovery (`239.255.255.250:3702`) + WSD `wsdp:Device` ikinci probe → yazıcı/PC
3. SSDP/UPnP (`239.255.255.250:1900`) + mDNS (`224.0.0.251:5353`)
4. Ping Sweep — SemaphoreSlim(64), 1000ms
5. *(Derin mod)* `UbiquitiSweepAsync` — UDP 10001
6. *(Derin mod)* `MndpSweepAsync` — UDP 5678
7. *(Derin mod)* `SnmpSweepAsync` — UDP 161

**`KameraBilgi` yeni alanlar (v0.2.0):** `UbntPlatform`, `UbntFirmware`, `UbntHostname`, `MikroTikBoard`, `MikroTikVersion`, `MikroTikIdentity`, `SnmpSysDescr`, `SnmpSysName`, `HttpFpMarka`, `HttpFpTur`, `HttpFpModel`, `WsdTipi`, `KesifKaynaklari HashSet<string>`.

**Cihaz Tara DataGrid sütunları:** IP, Ad, Tür, Marka, Model, Ping, Portlar, Keşif (130px), MAC, Üretici, Servis, **Güven** (60px — `Guven` confidence score 0-100).
**Not:** Risk sütunu kaldırıldı. Sütun başlıklarında `ⓘ` tooltip kullanılır.
**Dışa aktarma formatları:** Excel (`.xlsx` ClosedXML), PDF (QuestPDF A4 Yatay), TXT, CSV (UTF-8 `;`), JSON — sağ tık menüsünden.

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

## Partials/MainWindow.Wlan.cs (~420 satır — v0.3.0 ConcurrentDictionary)

| Satır | İçerik |
|---|---|
| L1–L14 | using/namespace (System.Collections.Concurrent dahil) |
| L17–L26 | Alanlar: `_wlanCts`, `_wlanSatirlar` (ObservableCollection), `_wlanBilinenBssid` **`ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>`** (v0.3.0), `_wlanOtoTimer`, `_wlanSayac` (int), `_wlanAdaptorVar` (bool) |
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

**Evil Twin eşiği (v0.3.0):** `SupheliEvilTwinSinyalleriniGuncelle` artık sabit 75 yerine `Math.Clamp(SettingsService.Yukle().EvilTwinSinyalEsigi, 50, 90)` kullanıyor.

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
