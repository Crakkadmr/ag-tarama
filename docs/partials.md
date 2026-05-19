# MainWindow Partial Dosya Haritası

> Satır numaraları ±20 satır toleransla doğrudur. Kod değişiklikleri sonrası eskime olabilir.
> "md güncelle" talimatı geldiğinde haritalar yeniden hesaplanır.
> Harita ile gerçek dosya arasında büyük sapma görülürse dosyayı `offset` yerine baştan oku ve haritayı güncelle.

`MainWindow.xaml.cs` + 11 partial dosya derleyici tarafından tek `MainWindow` sınıfında birleştirilir.
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

## Partials/MainWindow.DeviceScan.cs (engine-based — v0.4.0+)

Eski ~2340 satırlık inline sweep kodu `DeviceDiscoveryEngine`'e taşındı. Bu partial artık UI bağlama katmanıdır.

| Satır | İçerik |
|---|---|
| L1–L44 | using/namespace + `CihazKimlik` sealed class (Marka, Model, Tur, TurIkon) |
| L44 | `_engine = new DeviceDiscoveryEngine()` — `IDeviceDiscoveryEngine` alanı |
| L47–L73 | `KimlikBelirle(DeviceInfo)` → `KimlikBelirleV2(b)` (DeviceClassifier'a delege); `GuvenSkoru(DeviceInfo, CihazKimlik)` — KararIzi varsa KararIzi skoru, yoksa kanıt sayımı; 0-100 clamp |
| L75–L121 | Kimlik yardımcıları: `CihazAdiSec`, `IlkDolu`, `KisaHostAdi`, `AnlamliSayfaBasligi`, `TemizKimlikMetni` |
| L122–L201 | `TaramaSubneti` sealed class (Prefix, HostStart, HostEnd, OriginalCidr, Cidr, HostCount); `YerelSubnetleriBul`, `NicSubneti` record, `YerelNicSubnetleriniBul`, `YerelSubnetiBul`, `SanalAdaptorMu` |
| L203–L318 | `SubnetGirdisiniCoz` (virgül/noktalı virgül/boşluk ayrımlı, CIDR /16-/32 + prefix + tam IP); `CidrAraligaCoz` |
| L320–L425 | Subnet chip picker: `_subnetBoxChipSenkronu` bool, `KameraNicYenileBtn_Click`, `KameraNicChipleriniYenile`, `KameraChipOlustur(NicSubneti)` (DarkChip ToggleButton), `KameraChipDegisti`, `KameraChipleriSenkronizeEt` |
| L426–L880 | DataGrid event handler'ları: panel kapat, subnet textbox sync, kolon filtreler, tür filtresi, filtre temizle, çift tık, sağ tık menüsü; `SeciliKameraSatiri`, `TekIpTaraAsync`; sağ tık aksiyon handler'ları (web, ping, port, trace, dns, kopyala, favori, export); `KameraGorunenSatirlariAl` |
| L881–L933 | `KameraBaslatBtn_Click`, `KameraDurdurBtn_Click` |
| L935–L1049 | **`KameraTaramaBaslat()`** — subnet parse, `_engine.StartLiveAsync` / `_engine.StartScanAsync`, `DeviceChanged` subscribe/unsubscribe, `HistoryService.Kaydet` |
| L1051–L1052 | `OnEngineDeviceChanged` — `Dispatcher.BeginInvoke(() => KameraKartEkleVeyaGuncelle(dev))` |
| L1054–L1083 | `HttpBannerOku(ip, port, token)` — HTTP/200 sunucu başlığı + sayfa başlığı |
| L1085–L1164 | `KameraKartEkleVeyaGuncelle(DeviceInfo)` — ObservableCollection'a ekle veya `Kopyala` ile güncelle, `KameraFiltreleriUygula` |
| L1166–L1212 | **`KameraSatirOlustur(DeviceInfo)`** — `KimlikBelirle`, `CihazAdiSec`, port/servis listeleri, `KesifSira` sıralaması, Durum/SonGorulen/Online hesabı |
| L1214–L1280 | `KesifSira`, `KameraWebUrlSec`, `KameraFiltreleriUygula`, `KameraSatirFiltredenGecer`, `Icerir`, `KameraKutucugaYaz` |
| L1282– | `KameraSatir` sealed class (INotifyPropertyChanged) + export metotları (CSV, JSON, TXT, XLSX, PDF) |

**`KameraTaramaBaslat()` akışı:**
1. Subnet parse → `ScanOptions { DeepScan, LiveMode }` hazırla
2. `_engine.Store.DeviceChanged += OnEngineDeviceChanged`
3. Live mod → `_engine.StartLiveAsync`; Normal mod → `_engine.StartScanAsync` + tamamlama sonrası `Store.All` ile `KameraKartEkleVeyaGuncelle`
4. `Progress<ScanProgress>` → `KameraIlerlemeText`, `KameraFiltreSayacText` güncelleme
5. Finally: `DeviceChanged` unsubscribe, butonlar restore, `KameraAiBtn.IsEnabled = count > 0`

**`KameraSatir` alanları:** `Ip`, `Ad`, `Tur`, `Marka`, `Model`, `Os`, `Durum` ("Online"/"Offline"), `SonGorulen` (bugün → "HH:mm:ss", önceki gün → "dd.MM HH:mm"), `Online` (bool), `Ping`, `PingMs` (int — sıralama için), `Portlar`, `Kesif` (kaynağa göre öncelikli sıralı), `Mac`, `Uretici`, `Servis`, `WebUrl`, `Guven` (0-100), `KararIzi` (özet metin).

**DataGrid sütunları:** IP, Ad, Tür, Marka, Model, OS, Durum, Son Görülen, Ping, Portlar, Keşif, MAC, Üretici, Servis, **Güven** (0-100).

**Dışa aktarma:** Excel (`.xlsx` ClosedXML), PDF (QuestPDF A4 Yatay), TXT, CSV (UTF-8 `;`), JSON — sağ tık menüsünden.

---

## Partials/MainWindow.DeviceClassifier.cs

Kanıt tabanlı ağırlıklı cihaz sınıflandırıcı. `DeviceScan.cs`'teki `KimlikBelirle(DeviceInfo)` → `KimlikBelirleV2(b)` çağrısını karşılar.

```csharp
private static string MarkaNormalize(string marka)
// → "Hikvision" / "Dahua" / "Axis" / "Reolink" / "EZVIZ" / "Ubiquiti" /
//   "MikroTik" / "TP-Link" / "D-Link" / "NETGEAR" / "ASUS" / "Cisco" / "Aruba" /
//   "HP" / "Epson" / "Brother" / "Canon" / "Kyocera" / "Xerox" /
//   "Apple" / "Samsung" / "Xiaomi" / "Huawei" / "Google" / "Amazon" /
//   "Synology" / "QNAP" / "Sonos" / "Raspberry Pi" / "Espressif" / "VMware" / "Windows"

private static CihazKimlik KimlikBelirleV2(DeviceInfo b)
// → CihazKimlik { Marka, Model, Tur, TurIkon }
```

**`MarkaNormalize` routerboard/mikrotikls fix (v0.4.0+):** `lower.Contains("routerboard")` ve `lower.Contains("mikrotikls")` → "MikroTik" eklendi (IEEE OUI kayıtlarında bu alt-string'ler geçer).

**`KimlikBelirleV2` kanıt sırası (yüksek güven → düşük güven):**
1. Ubiquiti TLV (UbntPlatform / UbntHostname)
2. MikroTik identity / RouterOS board
3. HTTP Fingerprint (HttpFpMarka — vendor-specific endpoint)
4. SNMP sysDescr
5. ONVIF + WSD birlikte
6. mDNS türü (MdnsTur)
7. SSDP manufacturer
8. NetBIOS + SMB
9. OUI vendor (MAC prefix → `OuiVendorLookup.BulDetay`)
10. Port pattern fallback (kamera portları / yazıcı portları / router portları)

`KimlikKararIzi` (Services/Discovery/Classification/) — sınıflandırma gerekçesini saklar; `GuvenSkoru` hesaplamasında `TurSiralama[0].Skor` + `MarkaSiralama` bonusu kullanılır.

**Sınıflandırma düzeltmeleri (2026-05-19):**

- **`KanitTopla_Llmnr`:** LLMNR hostname artık körce "Bilgisayar" eklemez. `epson`, `brother`, `canon`, `kyocera`, `ricoh`, `lexmark`, `xerox`, `brn` (Brother Network), `npi` (HP JetDirect) ön ekleriyle başlayan hostname'ler → **Yazıcı** + marka kanıtı ekler (ağırlık: `LlmnrHostname=15`). Böylece `EPSON0SE587` gibi hostname'ler doğru sınıflandırılır.

- **`KanitTopla_Ssdp`:** `blob.Contains("storage/nas")` dalı artık NVR belirteçleri (`nvr`, `dvr`, `xvr`, `recorder`, `dahua`, `hikvision`, `xmeye`) varsa `"NAS"` yerine `"NVR/DVR"` ekler. Dahua NVR gibi cihazlar video depolama için "storage" kelimesiyle UPnP yayın yapsa da artık NAS olarak sınıflandırılmaz.

- **`SnmpImzalari`:** Axis/Hikvision/Dahua girişlerinden önce iki yeni regex eklendi:
  - `Dahua.*NVR|NVR.*Dahua` → Dahua / NVR/DVR (SNMP sysDescr'da hem Dahua hem NVR geçerse)
  - `\b(NVR|DVR|XVR|Video Recorder)\b` → NVR/DVR (marka bağımsız genel eşleme)

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

---

## AI Faz 2 Eki (2026-05-17)

- `Partials/MainWindow.Ai.cs` eklendi.
- Sorumluluklar:
  - `AiGonderBtn_Click`
  - `AiInputBox_KeyDown`
  - `AiTemizleBtn_Click`
  - `AiSoruGonderAsync`
- `MainWindow.xaml.cs` içindeki AI alanları:
  - `_aiSohbetGecmisi`
  - `_aiSohbetCts`
  - `_aiSohbetCalisiyor`
