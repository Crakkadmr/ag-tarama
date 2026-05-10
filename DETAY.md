# DETAY.md — Proje Tam Referans

> Bu dosya yeni Claude Code session'larında dosyaları taramadan tüm projeyi anlayabilmek için hazırlanmıştır.
> **Kaynak kodda her değişiklik yapıldığında bu dosya da güncellenmelidir.** (Bkz. CLAUDE.md)
> Son güncelleme: 2026-05-10 (SNMP Sorgusu paneli — Lextm.SharpSnmpLib, MIB-II sysGroup)

---

## 1. Proje Kimliği

| Alan | Değer |
|---|---|
| Ad | AG TARAMA PROGRAMI (AgTarama) |
| Tip | WPF Desktop Uygulaması |
| Hedef | .NET 10 (`net10.0-windows`), `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable` |
| Output | `WinExe` |
| Namespace | `AgTarama` |
| Sürüm | v0.1.0 |
| Branch | `ping_kismi` (main: `main`) |
| Git user | Crakkadmr |
| Kök yol | `C:\Projects\AG TARAMA PROGRAMI\AgTarama` |

## 2. Amaç

WPF tabanlı, **chatbot arayüzlü ağ paket yakalama uygulaması**. Aktif ağ arayüzlerini otomatik tespit eder, seçilen arayüzler üzerinde **tshark** ile paket yakalama gerçekleştirir, sonuçları **Wireshark Portable** ile analiz etmeye hazırlar. Ayrıca ping testi ve harici Advanced IP Scanner entegrasyonu mevcut.

---

## 3. Klasör Yapısı

```
AG TARAMA PROGRAMI/
└── AgTarama/
    ├── AgTarama.csproj           ← .NET 10 WPF proje dosyası
    ├── App.xaml / App.xaml.cs    ← Application giriş noktası (boş)
    ├── AssemblyInfo.cs           ← ThemeInfo
    ├── MainWindow.xaml           ← UI tasarımı + stiller (Window.Resources)
    ├── MainWindow.xaml.cs        ← TÜM iş mantığı buradadır (~1100 satır)
    ├── CLAUDE.md                 ← Claude için proje rehberi
    ├── DETAY.md                  ← (bu dosya) tam referans
    ├── README.md                 ← Türkçe kullanıcı dokümantasyonu
    ├── .claude/settings.local.json
    ├── Req/
    │   └── npcap-1.88.exe        ← Npcap installer (sessiz kurulum)
    ├── tools/
    │   ├── WiresharkPortable64/
    │   │   ├── WiresharkPortable64.exe
    │   │   └── App/Wireshark/tshark.exe   ← Yakalama motoru
    │   └── Ip_Scanner/
    │       └── advanced_ip_scanner.exe    ← Harici cihaz tarayıcı
    ├── captures/                 ← Otomatik oluşur, .pcap dosyaları
    ├── logs/                     ← Otomatik oluşur, log.txt işlem günlüğü
    ├── bin/                      ← Build çıktısı (gitignore)
    └── obj/                      ← Build ara dosyaları (gitignore)
```

---

## 4. Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build                   # Debug build
dotnet run                     # Çalıştır
dotnet build -c Release        # Release build
```

---

## 5. Mimari

**Tek pencere — MVVM yok.** Tüm UI ve iş mantığı `MainWindow.xaml` (~458 satır) + `MainWindow.xaml.cs` (~965 satır) çiftinde. Servis katmanı, ViewModel veya DI container yok.

### 5.1 UI Düzeni (MainWindow.xaml)

İki sütunlu Grid:
- **Sol** (`*` genişlik): Başlık (`AG TARAMA` + `StatusText`) + Chat alanı (`ChatScrollViewer` → `ChatPanel` StackPanel) + Animasyonla açılan **Ping paneli** (`PingCol` ColumnDefinition, sıfırdan 340px'e açılır)
- **Sağ** (220px sabit): Kontrol butonları StackPanel'i + alt versiyon yazısı

### 5.2 Stil Sistemi (Window.Resources)

| Kaynak Anahtar | Tip | Kullanım |
|---|---|---|
| `ActionButton` | Button | Standart sağ panel butonu (mavi, 44px) |
| `PrimaryButton` | Button | Yeşil "Taramayı Başlat" (48px, BasedOn ActionButton) |
| `DangerButton` | Button | Kırmızı "Taramayı Durdur" (BasedOn ActionButton) |
| (default) | ScrollBar | 6px ince ScrollBar |
| `PingInputBox` | TextBox | Ping IP giriş kutusu |
| `ChipButton` | Button | Hızlı IP seçim chip'leri (yuvarlak, 12 corner) |
| `SelectedChipButton` | Button | Seçili chip (BasedOn ChipButton, mavi arka plan) — SNMP versiyon toggle. **`ChipButton`'dan SONRA tanımlanmalı** (StaticResource forward-ref hatası) |

### 5.3 Renk Paleti (GitHub Dark teması)

```
Background:    #0D1117 (ana), #161B22 (yüzey), #21262D (ayırıcı)
Border:        #30363D (varsayılan), #21262D (silik)
Mavi:          #58A6FF (vurgu), #1F6FEB (seçim), #0D3B66 (basılı)
Yeşil:         #3FB950 (başarı), #1A4A2E (PrimaryButton bg), #238636
Kırmızı:       #F85149 (hata), #3D1A1A (DangerButton bg), #8B1A1A
Metin:         #E6EDF3 (parlak), #C9D1D9 (orta), #8B949E (silik), #484F58 (devre dışı)
```

### 5.4 Kontrol Butonları (sağ panel)

| Adı | x:Name | Stil | Click handler | Durum |
|---|---|---|---|---|
| Taramayı Başlat | `BtnTaramaBaslat` | PrimaryButton | `BtnTaramaBaslat_Click` | ✅ Aktif (tshark yakalama) |
| Taramayı Durdur | `BtnTaramaDurdur` | DangerButton | `BtnTaramaDurdur_Click` | ✅ Aktif (başlangıçta IsEnabled=False) |
| Ping Testi | `BtnPing` | ActionButton | `BtnPing_Click` | ✅ Yan panel |
| Port Tara | `BtnPortTara` | ActionButton | `BtnPortTara_Click` | ✅ Yan panel |
| Traceroute | `BtnTrace` | ActionButton | `BtnTrace_Click` | ✅ Yan panel (`tracert -d`) |
| DNS Lookup | `BtnDns` | ActionButton | `BtnDns_Click` | ✅ Yan panel (`Dns.GetHostEntryAsync`) |
| Cihazları Listele | `BtnCihazlar` | ActionButton | `BtnCihazlar_Click` | ✅ Advanced IP Scanner |
| ARP Tablosu | `BtnArp` | ActionButton | `BtnArp_Click` | ✅ `arp -a` → chat kart |
| Ağ Bilgisi | `BtnAgBilgi` | ActionButton | `BtnAgBilgi_Click` | ✅ `NetworkInterface` → chat kartı |
| SADP | `BtnSadp` | ActionButton | `BtnSadp_Click` | ✅ `tools/sadp/sadptool.exe` |
| Wake-on-LAN | `BtnWol` | ActionButton | `BtnWol_Click` | ✅ Yan panel (UDP magic packet) |
| SNMP Sorgusu | `BtnSnmp` | ActionButton | `BtnSnmp_Click` | ✅ Yan panel (SharpSnmpLib GET) |
| Ekranı Temizle | `BtnTemizle` | ActionButton | `BtnTemizle_Click` | ✅ Tarama sırasında disabled |

### 5.5 Port Tara Paneli (yan panel, animasyonla açılır)

`PingCol` sütununu paylaşır. Ping ve Port panelleri aynı anda açık olamaz — biri açılırken diğeri kapatılır.

Elemanlar:
- `PortPanel` (Grid, ClipToBounds=True, Visibility=Collapsed başlangıçta)
- `PortIpBox` (TextBox, IP girişi) — `TextChanged`, `KeyDown` event'leri
- `PortIpPlaceholder` (TextBlock)
- `PortIpValidasyon` (TextBlock — ✓/~/✗ canlı doğrulama)
- `PortAralikBox` (TextBox, port aralığı — "1-1024", "80,443") — `TextChanged`
- `PortAralikPlaceholder` (TextBlock)
- 4 adet hızlı seçim `ChipButton` (Tag = port listesi, Click = `PortHizliBtn_Click`):
  - `Yaygın 20`, `Web`, `SSH+RDP`, `Kamera`
- `PortBaslatBtn` (Button, PrimaryButton stili, başlangıçta IsEnabled=False)
- `PortResultBorder` (Border, başlangıçta Collapsed)
  - `PortResultScroll` > `PortResultPanel` — açık portlar satır satır gösterilir
- Kapat butonu → `PortPanelKapat_Click`

Port tarama: `TcpClient.ConnectAsync` + `SemaphoreSlim(50)` + 1000ms timeout. Açık portlar yeşil `[AÇIK]` ile gerçek zamanlı listelenir. `BilindikPortlar` sözlüğü ile servis adı gösterilir.

### 5.6 Ping Paneli (yan panel, animasyonla açılır)

Elemanlar:
- `PingPanel` (Grid, ClipToBounds=True, `PingCol` 0→340px)
- `PingIpBox` (TextBox, IP girişi) — `TextChanged`, `KeyDown` event'leri
- `PingPlaceholder` (TextBlock, "Örn: 192.168.1.1")
- `PingValidasyonIkonu` (TextBlock — ✓/~/✗ canlı doğrulama)
- 6 adet `ChipButton` hızlı IP (Tag = IP, Click = `PingHizliBtn_Click`):
  - `192.168.1.1`, `192.168.1.254`, `8.8.8.8`, `1.1.1.1`, `8.8.4.4`, `google.com`
- `PingResultBorder` (Border, başlangıçta Collapsed) — ping başlayınca görünür olur
  - `PingResultScroll` (ScrollViewer, MaxHeight=180) içinde `PingResultPanel` (StackPanel)
  - Ping sonuçları (satır satır TextBlock) buraya eklenir; ana chat'e yazılmaz
- Kapat butonu → `PingPanelKapat_Click`

**Ping paneli iç grid satırları (6 satır):**
| Row | Height | İçerik |
|---|---|---|
| 0 | Auto | Başlık + açıklama |
| 1 | Auto | IP giriş kutusu |
| 2 | Auto | Hızlı seçim chip'leri |
| 3 | Auto | `PingBaslatBtn` (PrimaryButton, IsEnabled=False başlangıçta) |
| 4 | `*`  | `PingResultBorder` (sonuç kutusu) |
| 5 | Auto | Kapat butonu |

---

## 6. MainWindow.xaml.cs — Tam İçerik Haritası

### 6.1 Using İfadeleri

`System`, `System.Collections.Generic`, `System.Diagnostics`, `System.IO`, `System.Linq`, `System.Net.NetworkInformation`, `System.Net.Sockets`, `System.Text`, `System.Text.RegularExpressions`, `System.Threading`, `System.Threading.Tasks`, `System.Windows`, `System.Windows.Controls`, `System.Windows.Input`, `System.Windows.Media`, `Lextm.SharpSnmpLib`, `Lextm.SharpSnmpLib.Messaging`, `Microsoft.Win32`.

### 6.2 Alanlar (20-60)

| Alan | Tip | Amaç |
|---|---|---|
| `_taramaDevamEdiyor` | bool | Tarama durumu flag'i |
| `_taramaCts` | `CancellationTokenSource?` | Tarama iptali |
| `_tsharkProc` | `Process?` | Aktif tshark process |
| `_pingPanelAcik` | bool | Ping paneli durumu |
| `_pingCts` | `CancellationTokenSource?` | Ping iptali |
| `PingPanelGenisligi` | const double = 340 | Yan panel hedef genişliği |
| `_portPanelAcik` | bool | Port tara paneli durumu |
| `_portScanCts` | `CancellationTokenSource?` | Port tarama iptali |
| `BilindikPortlar` | `Dictionary<int,string>` | Port → servis adı eşlemesi (24 giriş) |
| `_tracePanelAcik` | bool | Traceroute paneli durumu |
| `_traceCts` | `CancellationTokenSource?` | Traceroute iptali |
| `_dnsPanelAcik` | bool | DNS paneli durumu |
| `_wolPanelAcik` | bool | Wake-on-LAN paneli durumu |
| `_snmpPanelAcik` | bool | SNMP sorgusu paneli durumu |
| `_snmpVersiyon` | string | Seçili SNMP versiyonu: `"v1"` veya `"v2c"` (varsayılan `"v2c"`) |
| `_macRegex` | `Regex` | MAC adresi doğrulama regex'i |
| `_otomatikGuncelleniyor` | bool | Otomatik nokta ekleme döngü koruması |
| `_oncekiUzunluk` | `Dictionary<TextBox,int>` | Silme/ekleme tespiti için önceki uzunluk |
| `AppBase` | static readonly string | exe konumu (single-file için `Environment.ProcessPath`, fallback `BaseDirectory`) |
| `NpcapInstaller` | static readonly string | `{AppBase}\Req\npcap-1.88.exe` |
| `TsharkExe` | static readonly string | `{AppBase}\tools\WiresharkPortable64\App\Wireshark\tshark.exe` |
| `WiresharkPortableExe` | static readonly string | `{AppBase}\tools\WiresharkPortable64\WiresharkPortable64.exe` |
| `LogKlasor` | static readonly string | `{AppBase}\logs` |
| `LogDosyasi` | static readonly string | `{AppBase}\logs\log.txt` |
| `TestSuresiSn` | const int = 2 | Arayüz aktiflik testi süresi |
| `HedefMB` | const int = 16 | Yakalama dosya boyutu sınırı |
| `HedefKB` | const int = 16384 | tshark `-a filesize:` argümanı |
| `_paketSayisi` | int | Atomik paket sayacı |
| `_sonPcap` | string | Son üretilen pcap yolu |

### 6.3 Yaşam Döngüsü ve Npcap

- **`MainWindow()`** (47-52): InitializeComponent, ilk mesaj, `BaslangicAsync()` fire-and-forget.
- **`BaslangicAsync()`** (54-58): `LogOturumBaslat()` + Npcap kontrol/kurulum + "Sistem hazır" mesajı.
- **`LogOturumBaslat()`**: `logs/` klasörünü oluşturur, `log.txt`'e `=== OTURUM: {datetime} ===` satırı yazar.
- **`LogKaydet(string kategori, string hedef, IEnumerable<string> satirlar)`**: Her işlem sonunda çağrılır. `[HH:mm:ss] [KATEGORİ] hedef` başlığı + satırları `log.txt`'e UTF-8 ekler. Kategoriler: `PING`, `PORT TARA`, `TRACEROUTE`, `DNS`, `ARP`, `AG BILGI`, `WAKE-ON-LAN`.
- **`NpcapKurulumu()` static** (61-66): `HKLM\SOFTWARE\Npcap` ve `WOW6432Node\Npcap` registry kontrolü.
- **`NpcapKontrolVeKur()`** (68-111): Npcap yüklü değilse `Req\npcap-1.88.exe /S` parametresiyle UAC ile sessiz kurulum (Verb="runas"). Hatalar `MesajEkle("hata", ...)` ile bildirilir.

### 6.4 Arayüz Tespiti

- **`record ArayuzBilgi(string No, string Ad)`** (114).
- **`TumArayuzlariGetirAsync()`** (116-153): `tshark -D` çıktısını parse eder. Format: `"1. \Device\NPF_{GUID} (Wi-Fi)"` → `No="1"`, `Ad="Wi-Fi"`. 32 karakterden uzun adlar `…` ile kısaltılır.
- **`ArayuzPaketSayisiAsync(string no)` static** (273-304): `tshark -i {no} -a duration:2 -q` ile 2 sn paket dinler. tshark istatistiği **stderr**'e yazar; "N packets captured" satırından sayı alınır.

### 6.5 Arayüz Seçim UI

- **`ArayuzSecimAsync(...)`** (156-252): ChatPanel'e dinamik kart ekler. Her arayüz için toggle Button (pasif gri ↔ aktif mavi). En az bir seçim olunca "Dinlemeyi Başlat" butonu enable olur. `TaskCompletionSource<List<string>>` ile bekleme.
- **`UygulaButonSablon(Button btn)` static** (255-270): Border (CornerRadius=6) içeren ControlTemplate'i programatik olarak butona uygular (XAML stilini taklit eder).

### 6.6 Yakalama Akışı

- **`YakalamaBaslat()`** (314-419): Akış:
  1. `BtnTaramaBaslat.IsEnabled = false`
  2. `TumArayuzlariGetirAsync()` → tüm arayüzler
  3. `ArayuzPaketSayisiAsync` paralel çağrı → aktif arayüzler
  4. `ArayuzSecimAsync` → kullanıcı seçimi
  5. `captures\analiz_ddMMyyyy_HH_mm.pcap` oluştur
  6. tshark başlat: `{iArgs} -w "{pcap}" -a filesize:{HedefKB} -P`
  7. stdout okuyan task → `Interlocked.Increment(ref _paketSayisi)`
  8. `DosyaIzleAsync` ile her 500ms kart güncelle
  9. Tamamlanma veya iptal sonrası kart durumu ayarla.

- **`DosyaIzleAsync(...)`** (422-437): 500ms döngüde dosya boyutu okur, `Guncelle(mb, paket, sure)` çağırır, `StatusText`'i günceller.

- **`YakalamaKartiOlustur(...)`** (440-613): Programatik olarak Border/StackPanel/Grid yapısı kurar. Döndürdüğü tuple:
  - `Kart` (Border)
  - `Guncelle(double mb, int paket, TimeSpan sure)` — progress bar, yüzde, BOYUT/PAKET/SÜRE
  - `Tamamla(double mb, int paket)` — yeşil renge dönüş, "YAKALAMA TAMAMLANDI"
  - `Durdur()` — kırmızı renk, "YAKALAMA DURDURULDU"

- **`YakalamaDurdur()`** (615-622): CTS cancel + `Process.Kill(entireProcessTree:true)`.

- **`WiresharkIleAc(string pcap)`**: `WiresharkPortable64.exe "{pcap}"` ile başlatır. Yakalama tamamlandığında karta eklenen "⬡ Wireshark'ta Aç" butonundan çağrılır.

### 6.7 Mesaj Sistemi (Chatbot)

**`MesajEkle(string tur, string metin)`** (645-717): Her mesajı stillenmiş Border + TextBlock + zaman damgası olarak ChatPanel'e ekler. ScrollToEnd otomatik.

| `tur` | Arka Plan | Kenarlık | Metin Rengi | Hizalama | Prefix |
|---|---|---|---|---|---|
| `"sistem"` | #161B22 | #21262D | #8B949E | Stretch | `◆ ` |
| `"kullanici"` | #161B22 | #30363D | #C9D1D9 | Right (max 500) | `› ` |
| `"sonuc"` | #0D3B66 | #1F6FEB | #58A6FF | Stretch | (yok) |
| `"hata"` | #3D1A1A | #8B1A1A | #F85149 | Stretch | `✖ ` |

**`TaramaDurumunuAyarla(bool devamEdiyor)`** (719-729): Buton durumlarını + `StatusText` ("● Hazır" yeşil / "● Yakalanıyor..." sarı) senkronize eder. `BtnTemizle` de bu metottan yönetilir: tarama başlayınca devre dışı, tamamlanınca/durdurulunca aktif olur.

### 6.8 Ping İşlevi

- **`BtnPing_Click`** (~737): Yan paneli toggle.
- **`PingPanelAcAnimasyon` / `PingPanelKapatAnimasyon`** (~746-793): `DispatcherTimer` ile 12ms periyotlu, exponential easing (kalanın %28'i + min 2px) ile `PingCol.Width` animasyonu.
- **`GecerliIpv4Mu(string)` static** (~796): 4 nokta-ayrılmış 0-255 oktet kontrolü.
- **`GecerliHostnameMu(string)` static** (~808): Regex `^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$`.
- **`PingIpBox_TextChanged`**: Canlı doğrulama: `✓` (yeşil IPv4), `~` (sarı hostname), `✗` (kırmızı geçersiz). Geçerli girişte `PingBaslatBtn.IsEnabled=true`.
- **`PingBaslatBtn_Click`**: Butona tıklanınca `PingBaslat(hedef)`.
- **`PingIpBox_KeyDown`**: Enter → `PingBaslat`.
- **`PingHizliBtn_Click`**: Chip butonun `Tag`'inden IP alıp `PingBaslat`.
- **`PingKutucugaYaz(string metin, string hex)`**: `PingResultPanel`'e stillenmiş `TextBlock` satırı ekler. Ping sonuçları ana chat'e **yazılmaz**.
- **`PingBaslat(string hedef)`**: 4× ping, log satırlarını toplar, tamamlanınca `LogKaydet("PING", ...)` çağırır.

### 6.9 Diğer Buton Handler'ları

- **`BtnPortTara_Click`**: `PortPanelAcAnimasyon` / `PortPanelKapatAnimasyon` toggle. Ping paneli açıksa önce kapatır.
- **`PortPanelAcAnimasyon` / `PortPanelKapatAnimasyon`**: `PingCol` animasyonu (PingPanel ile paylaşımlı sütun). Açılırken `PortPanel.Visibility=Visible`, kapanırken `Collapsed`.
- **`PortIpBox_TextChanged`**: Canlı IPv4/hostname doğrulama. `AktarButonDurumu()` çağırır.
- **`PortAralikBox_TextChanged`**: Placeholder yönetimi + `AktarButonDurumu()`.
- **`AktarButonDurumu()`**: Geçerli IP ve port varsa `PortBaslatBtn.IsEnabled=true`.
- **`PortHizliBtn_Click`**: Chip'in `Tag`'ini `PortAralikBox.Text`'e yazar.
- **`PortBaslatBtn_Click`**: `PortTaraBaslat(hedef, portlar)` fire-and-forget.
- **`PortKutucugaYaz(string, string)`**: `PortResultPanel`'e stillenmiş satır ekler.
- **`PortlariParse(string) → int[]`**: "1-1024", "80,443,22" formatını parse eder, sıralı dizi döner.
- **`PortTaraBaslat(string, int[])`**: `TcpClient.ConnectAsync` + `SemaphoreSlim(50)` + 1000ms timeout ile paralel port tarama. Açık portları `Dispatcher.InvokeAsync` ile UI'a yazar.
- **`BtnCihazlar_Click`**: `tools\Ip_Scanner\advanced_ip_scanner.exe` başlatır.
- **`BtnSadp_Click`**: `tools\sadp\sadptool.exe` başlatır.
- **`BtnTemizle_Click`**: `ChatPanel.Children.Clear()` + sistem mesajı. Tarama sırasında `IsEnabled=false`.
- **`PortTaraBaslat`**: Sonunda `LogKaydet("PORT TARA", ...)` çağırır (`ConcurrentBag` ile açık portları toplar).
- **`TracerouteBaslat`**: Her hop satırını `logSatirlari`'a ekler, tamamlanınca `LogKaydet("TRACEROUTE", ...)`.
- **`DnsLookupBaslat`**: Sonuç satırlarını toplar, `LogKaydet("DNS", ...)`.
- **`ArpTablosuGoster`**: Tablo metnini satırlara böler, `LogKaydet("ARP", ...)`.
- **`AgAdaptorleriniGoster`**: Her adaptör bilgisini toplar, `LogKaydet("AG BILGI", ...)`.
- **`WolGonder`**: Sonucu loglar, `LogKaydet("WAKE-ON-LAN", mac, ...)`.
- **`BtnSnmp_Click`**: SNMP panelini toggle eder.
- **`SnmpIpBox_TextChanged`**: Sadece IPv4 geçerli; `✓/✗` + `SnmpBaslatBtn.IsEnabled`.
- **`SnmpCommunityBox_TextChanged`**: Placeholder yönetimi.
- **`SnmpVersiyonBtn_Click`**: `_snmpVersiyon` günceller; `SnmpV2cBtn`/`SnmpV1Btn` stilini `SelectedChipButton` ↔ `ChipButton` olarak değiştirir.
- **`SnmpBaslatBtn_Click`** / **`SnmpIpBox_KeyDown`**: `SnmpSorguBaslat(ip)` fire-and-forget.
- **`SnmpPanelKapat_Click`**: Paneli kapatan animasyon + Collapsed.
- **`SnmpSorguBaslat(string ip)`**: `Messenger.Get` (SharpSnmpLib) ile MIB-II sysGroup OID'lerini (sysDescr/sysUpTime/sysContact/sysName/sysLocation) sorgular. `Task.Run` ile thread-pool'da çalışır, 3000ms timeout. `Lextm.SharpSnmpLib.Messaging.TimeoutException` (fully-qualified, `System.TimeoutException` ile çakışma nedeniyle) yakalanır. Sonuçlar `SnmpResultPanel`'e, `LogKaydet("SNMP", ...)` ile `log.txt`'e yazılır.
- **`SnmpDegerFormatla(ISnmpData, string oid)` static**: sysUpTime'ı `SnmpUptimeFormatla` ile biçimlendirir; diğer tipler için `.ToString()`.
- **`SnmpUptimeFormatla(uint ticks)` static**: TimeTicks'i `Xg XXs XXd XXsn` formatına dönüştürür.

---

## 7. Harici Bağımlılıklar (runtime)

| Dosya | Yer | Amaç | Otomatik mi? |
|---|---|---|---|
| `tshark.exe` | `tools\WiresharkPortable64\App\Wireshark\` | Paket yakalama | Manuel kurulmalı |
| `WiresharkPortable64.exe` | `tools\WiresharkPortable64\` | pcap görüntüleme | Manuel |
| `npcap-1.88.exe` | `Req\` | Sürücü installer | İlk açılışta otomatik UAC |
| `advanced_ip_scanner.exe` | `tools\Ip_Scanner\` | Cihaz listesi | Manuel |
| `Lextm.SharpSnmpLib` | NuGet (12.*) | SNMP v1/v2c GET sorgusu | `dotnet restore` otomatik |

---

## 8. Mesaj Türleri Hızlı Referans

```csharp
MesajEkle("sistem",    "...")  // gri, ◆ prefix, durum bildirimi
MesajEkle("kullanici", "...")  // sağda, › prefix, kullanıcı girdisi
MesajEkle("sonuc",     "...")  // mavi, başarı/sonuç
MesajEkle("hata",      "...")  // kırmızı, ✖ prefix
```

---

## 9. TODO / Yol Haritası

CLAUDE.md'de listelenen ve kodda hâlâ açık olanlar:

1. ✅ **Port Tara** — `PortTaraBaslat` + `PortPanel` yan paneli
2. ✅ **Otomatik Log** — `LogKaydet` → `logs/log.txt` (PING/PORT TARA/TRACEROUTE/DNS/ARP/AG BILGI/WAKE-ON-LAN bölümleri)
3. ✅ **Wireshark "Aç" butonu** — yakalama tamamlanınca karta dinamik ekleniyor
4. ✅ **Traceroute** — `TracerouteBaslat` (`tracert -d`), `TracePanel` yan paneli
5. ✅ **DNS Lookup** — `DnsLookupBaslat` (`Dns.GetHostEntryAsync`), `DnsPanel` yan paneli
6. ✅ **ARP Tablosu** — `ArpTablosuGoster` (`arp -a` parse), chat kart
7. ✅ **Ağ Adaptörü Bilgisi** — `AgAdaptorleriniGoster` (`NetworkInterface`), chat kart
8. ✅ **Wake-on-LAN** — `WolGonder` (UDP magic packet), `WolPanel` yan paneli
9. ✅ Ping Testi — `PingBaslat`
10. ✅ Cihazları Listele — Advanced IP Scanner
11. ✅ Taramayı Başlat / Durdur — tshark wrapper
12. ✅ **SNMP Sorgusu** — `SnmpSorguBaslat` (Lextm.SharpSnmpLib, MIB-II sysGroup), `SnmpPanel` yan paneli

---

## 10. Geliştirme Kuralları (CLAUDE.md'den)

- Tüm ağ işlemleri `async/await` + `CancellationToken` (UI thread'i bloke etme).
- Sonuçlar `MesajEkle("sonuc", ...)` ile chat'e basılır.
- Yeni buton sağ paneldeki StackPanel'e `ActionButton` stiliyle eklenir.
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`'da tanımlanır.
- .NET 10 / WPF — `LetterSpacing` gibi web CSS özellikleri yoktur.

---

## 11. Git Durumu (snapshot 2026-05-10)

- **Branch:** `duzenleme`
- **Main:** `main`
- **Modifiye edilenler:** `MainWindow.xaml`, `MainWindow.xaml.cs`
- **Son commitler:**
  - `88c2598` Merge pull request #1 from Crakkadmr/cihazlari_listeleme_kismi
  - `162bf75` refactor: remove integrated IP scanner background process logic and launch external executable instead
  - `001ffcc` feat: implement local network device discovery using Advanced IP Scanner
  - `5627c60` AppBase: ProcessPath kullan — single-file exe ile uyumlu
  - `3ed07f8` İlk commit — Ağ Tarama Programı v0.1
