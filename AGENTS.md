# AGENTS.md — Proje Tam Referans ve Geliştirici Rehberi

> Bu dosya AI agent'larının projeyi tek yerden anlayabilmesi için hazırlanmıştır.
> **Kaynak kodda her değişiklik yapıldığında bu dosya da aynı turda güncellenmelidir.**
> Son güncelleme: 2026-05-13 (Cihaz Tara: sağ tık çökmesi düzeltildi, Excel/PDF/TXT/CSV dışa aktarma eklendi)

---

## OTOMATİK GÜNCELLEME KURALI (ZORUNLU)

Aşağıdaki değişikliklerden **herhangi birini** yaptığında `AGENTS.md` dosyasını **aynı turda** güncelle
(kullanıcı ayrıca istemese bile):

- `MainWindow.xaml` veya `MainWindow.xaml.cs` dosyası değiştirildiğinde
- Yeni `.cs` / `.xaml` dosyası eklendiğinde veya silindiğinde
- `AgTarama.csproj` değiştirildiğinde (TargetFramework, paket vb.)
- Yeni klasör/araç eklendiğinde (`tools/`, `Req/` altı dahil)
- Yeni buton, stil veya UI bileşeni eklendiğinde
- Yeni metot/alan/state değişkeni eklendiğinde
- TODO maddesi tamamlandığında veya yeni TODO doğduğunda

Güncelleme yaparken:
- İlgili bölümü bul ve **yerinde** düzenle — dosyayı baştan yazma.
- Üstteki `Son güncelleme:` satırını değişikliğin tarihine çevir.
- Yeni metotları §6'ya, yeni butonları §5.4'e, yeni alanları §6.2'ye ekle.

---

## 1. Proje Kimliği

| Alan | Değer |
|---|---|
| Ad | Network Sniffer (AgTarama) |
| Tip | WPF Desktop Uygulaması |
| Hedef | .NET 10 (`net10.0-windows`), `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable` |
| csproj ek | `tools\**\*` ve `Req\**\*` → `CopyToOutputDirectory=PreserveNewest` |
| Output | `WinExe` |
| Namespace | `AgTarama` |
| Sürüm | v0.1.0 |
| Branch | `VOL-2` (main: `main`) |
| Git user | Crakkadmr |
| Kök yol | `C:\Projects\AG TARAMA PROGRAMI\AgTarama` |

---

## 2. Proje Amacı

WPF tabanlı, **Network Sniffer markalı chatbot arayüzlü ağ tarama ve paket yakalama uygulaması**. Aktif ağ arayüzlerini
otomatik tespit eder, seçilen arayüzler üzerinde **tshark** ile paket yakalama gerçekleştirir,
sonuçları **Wireshark Portable** ile analiz etmeye hazırlar.

Ek özellikler: ping testi, port tarama, traceroute, DNS lookup, ARP tablosu (OUI üretici gösterimi),
ağ adaptörü bilgisi, bant genişliği monitörü, Wake-on-LAN, Cihaz Tara (port scan + ONVIF + SSDP + Ping
Sweep + DNS/ping-a/NetBIOS cihaz adı + UPnP/ONVIF ad-model + ARP/MAC/OUI + servis banner + Advanced IP Scanner console zenginleştirme + canlı arama/tür/protokol filtreleri), favori IP listesi, Advanced IP Scanner entegrasyonu, SADP entegrasyonu.

---

## 3. Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build                   # Debug build
dotnet run                     # Çalıştır
dotnet build -c Release        # Release build
```

---

## 4. Klasör Yapısı

```
AG TARAMA PROGRAMI/
└── AgTarama/
    ├── AgTarama.csproj               ← .NET 10 WPF proje dosyası (NuGet bağımlılığı yok)
    ├── App.xaml / App.xaml.cs        ← Application giriş noktası (boş)
    ├── AssemblyInfo.cs               ← ThemeInfo
    ├── MainWindow.xaml               ← Network Sniffer UI tasarımı + stiller (~1144 satır)
    ├── MainWindow.xaml.cs            ← UI wiring + event handler'lar (~2786 satır)
    ├── Paths.cs                      ← Tüm exe-relative yol sabitleri (static)
    ├── LogService.cs                 ← %APPDATA%\AgTarama\logs\YYYYMMDD.log
    ├── Services/
    │   ├── InterfaceDiscoveryService.cs  ← tshark -D + paket sayısı testi
    │   ├── CaptureService.cs             ← tshark yakalama + ilerleme callback
    │   ├── PingService.cs                ← IAsyncEnumerable ping akışı
    │   ├── PortScanService.cs            ← Parse + async port tarama
    │   ├── NetbiosService.cs             ← DNS + ping -a + nbtstat -A ile cihaz adı/workgroup parse
    │   ├── AdvancedIpScannerService.cs   ← advanced_ip_scanner_console.exe çıktısı parse/zenginleştirme
    │   ├── AppSettings.cs                ← Ayar modeli (HedefMB, TestSuresiSn vb.)
    │   ├── SettingsService.cs            ← JSON serileştirme (%APPDATA%)
    │   └── FavoriService.cs              ← Favori IP CRUD (%APPDATA%)
    ├── SettingsWindow.xaml / .cs     ← Ayarlar penceresi
    ├── AGENTS.md                     ← (bu dosya) tam referans + geliştirici rehberi
    ├── AGENT.md                      ← Claude Code için referans (Claude'a özgü kurallar içerir)
    ├── EKLENECEKLER.md               ← Geliştirme yol haritası (25+ özellik)
    ├── codex-eklenebilecekler.md     ← Codex güncel özellik adayları ve önerilen uygulama sırası
    ├── README.md                     ← Türkçe kullanıcı dokümantasyonu
    ├── Req/
    │   └── npcap-1.88.exe            ← Npcap installer
    ├── tools/
    │   ├── WiresharkPortable64/
    │   │   ├── WiresharkPortable64.exe
    │   │   └── App/Wireshark/tshark.exe
    │   ├── Ip_Scanner/
    │   │   └── advanced_ip_scanner.exe
    │   └── sadp/
    │       └── sadptool.exe
    ├── captures/                     ← Otomatik oluşur, .pcap dosyaları
    ├── bin/                          ← Build çıktısı (gitignore)
    └── obj/                          ← Build ara dosyaları (gitignore)
```

**Log klasörü:** `%APPDATA%\AgTarama\logs\YYYYMMDD.log`
**Ayarlar:** `%APPDATA%\AgTarama\settings.json`
**Favoriler:** `%APPDATA%\AgTarama\favoriler.json`

---

## 5. Mimari

**Tek pencere — MVVM yok.** UI wiring `MainWindow.xaml` + `MainWindow.xaml.cs` çiftinde.
Ağ iş mantığı `Services/` katmanına ayrılmış. ViewModel veya DI container yok.

**Mimari katmanlar:**
- `Paths.cs` — tüm exe-relative yol sabitleri tek yerde (`AppBase`, `TsharkExe`, `SadpExe`, `IpScannerConsoleExe`, `IpScannerMacDb` vb.)
- `LogService.cs` — `%APPDATA%\AgTarama\logs\YYYYMMDD.log`'a yazar; `OturumBaslat`, `Kaydet`, `Hata` metotları
- `Services/InterfaceDiscoveryService` — `tshark -D` parse + 2s paket sayısı testi
- `Services/CaptureService` — tshark process yönetimi, progress callback (`Action<double, int, TimeSpan>`)
- `Services/PingService` — `IAsyncEnumerable<PingSonuc>` akışı (4 ping, TTL, hata sarmalı)
- `Services/PortScanService` — `Parse(string)` + `TaraAsync(...)` (SemaphoreSlim 50, 1000ms timeout)
- `Services/NetbiosService` — UDP 137 NetBIOS Node Status, reverse DNS, `ping -a` ve `nbtstat -A <ip>` ile Windows cihaz adı + grup/workgroup parse eder
- `Services/AdvancedIpScannerService` — `tools\Ip_Scanner\advanced_ip_scanner_console.exe` ile subnet tarar, IP/ad/MAC/üretici/servis çıktısını parse eder
- `Services/SettingsService` — `Yukle()` / `Kaydet(AppSettings)` JSON serileştirme
- `Services/FavoriService` — `Ekle`, `Sil`, `YukleHepsi` CRUD
- `MainWindow` → `HataBildir(mesaj, ex?)` — chat kırmızı mesaj + `LogService.Hata` tek noktadan

### 5.1 UI Düzeni (MainWindow.xaml)

- `WindowState="Maximized"` — uygulama tam ekran açılır
- **2 satırlı kök Grid:** Satır 0 (Auto) = başlık kartı; Satır 1 (`*`) = `MainTabControl`
- **Başlık kartı** (`#161B22`, CornerRadius=12): sol — ikon + `NETWORK SNIFFER` + `StatusText`; orta — araç WrapPanel (BtnTaramaBaslat, BtnTaramaDurdur, BtnArp, BtnAgBilgi, BtnCihazlar, BtnSadp, BtnRapor, BtnAyarlar, BtnTemizle); sağ — `made by demircan` versiyon yazısı
- **TabControl** (`x:Name="MainTabControl"`, custom ControlTemplate — TabPanel ScrollViewer ile sarılmış): 9 sekme:

| # | Sekme Başlığı | İçerik |
|---|---|---|
| 0 | 💬 Chatbot | ChatScrollViewer → ChatPanel + FavoriChipleri; header'daki araç butonları Chatbot'u kontrol eder |
| 1 | ◎ Cihaz Tara | `KameraPanel` — tam genişlik envanter: subnet giriş, Tara/Durdur, filtreler, DataGrid |
| 2 | ◈ Ping Testi | `PingPanel` — IP giriş, chip'ler, PingResultPanel |
| 3 | ⊞ Port Tara | `PortPanel` — IP + port aralığı, chip'ler, PortResultPanel |
| 4 | ⇢ Traceroute | `TracePanel` — IP giriş, TraceResultPanel |
| 5 | ⊕ DNS Lookup | `DnsPanel` — hostname giriş, DnsResultPanel |
| 6 | ⏻ Wake-on-LAN | `WolPanel` — MAC giriş, magic packet gönder |
| 7 | ★ Favoriler | `FavorilerPanel` — favori IP listesi + sil chip'leri |
| 8 | ▶ Bant Genişliği | `BantPanel` — adaptör kartları (canlı ↓↑ hız, 1s timer) |

**TabItem stili (custom ControlTemplate):** Consolas 12pt, `#8B949E` fg, transparent border. Seçilince alt kenarlık `#2F81F7` (2px), bg `#0D1F2F`, metin `#58A6FF`. Hover (seçili değilken) bg `#161B22`, metin `#C9D1D9`. CornerRadius=6,6,0,0.

**Eski yan panel / sidebar mimarisi tamamen kaldırıldı.** `PingCol` GridColumnDefinition, sağ 250px sütun ve tüm animasyon timer'ları artık yok.

### 5.2 Stil Sistemi (Window.Resources)

| Kaynak Anahtar | Tip | Kullanım |
|---|---|---|
| `ActionButton` | Button | Standart sağ panel butonu (koyu mavi, 44px yükseklik) |
| `ActiveActionButton` | Button | Aktif panel butonu — yeşil çerçeve (#3FB950, 2px), yeşilimsi bg. **`ActionButton`'dan SONRA tanımlanmalı** |
| `PrimaryButton` | Button | Yeşil "Başlat" butonu (48px, BasedOn ActionButton) |
| `DangerButton` | Button | Kırmızı "Durdur" butonu (BasedOn ActionButton) |
| `PingInputBox` | TextBox | Yan panel IP/değer giriş kutusu |
| `ChipButton` | Button | Hızlı seçim chip'leri (yuvarlak, CornerRadius=12) |
| `DarkComboBox` | ComboBox | Cihaz Tara tür filtresi; koyu açılır liste ve ok template'i |
| `DarkComboBoxItem` | ComboBoxItem | Koyu dropdown satırları; hover/seçili durumları |
| `DarkDataGrid` | DataGrid | Cihaz Tara tablo görünümü; koyu tema, sıralanabilir sütunlar |
| `DarkDataGridColumnHeader` | DataGridColumnHeader | Koyu sütun başlıkları |
| `DarkDataGridCell` | DataGridCell | Koyu hücre template'i |
| `DarkDataGridRow` | DataGridRow | Koyu satır/hover/seçili durumları |
| `FlatContextMenu` | ContextMenu | Cihaz Tara sağ tık/dışa aktarma menüsü; güvenli `ItemsPresenter` template'iyle varsayılan ikon gutter'ı kaldırılmış koyu tema |
| `FlatContextMenuItem` | MenuItem | Sağ tık menü satırları; tek sütunlu sade template |
| `ToolTip` (default) | ToolTip | Koyu tema ToolTip — `#1C2128` bg, `#C9D1D9` fg, `#3D444D` border, CornerRadius=5, MaxWidth=280 |
| (default) | ScrollBar | 6px ince ScrollBar |

**Buton `ⓘ` badge:** Her sağ panel butonu `Grid` içerik kullanır — sol `StackPanel` (ikon + etiket), sağ-üst `TextBlock` (`ⓘ`, `#58A6FF88`). `HorizontalContentAlignment="Stretch"` zorunlu.

### 5.3 Renk Paleti (GitHub Dark teması)

```
Background:  #0D1117 (ana), #161B22 (yüzey), #21262D (ayırıcı)
Border:      #30363D (varsayılan), #21262D (silik)
Mavi:        #58A6FF (vurgu), #1F6FEB (seçim), #0D3B66 (basılı)
Yeşil:       #3FB950 (başarı), #1A4A2E (PrimaryButton bg), #238636
Kırmızı:     #F85149 (hata), #3D1A1A (DangerButton bg), #8B1A1A
Metin:       #E6EDF3 (parlak), #C9D1D9 (orta), #8B949E (silik), #484F58 (devre dışı)
```

### 5.4 Araç Butonları (başlık WrapPanel — soldan sağa)

Tüm butonlar artık **üst başlık kartındaki WrapPanel**'de bulunur (eski sağ kenar çubuğu kaldırıldı). Tıklama davranışları ya chat/tshark işlemi başlatır ya da ilgili sekmeye geçer.

| Adı | x:Name | Click Handler | Açıklama |
|---|---|---|---|
| Taramayı Başlat | `BtnTaramaBaslat` | `BtnTaramaBaslat_Click` | tshark yakalama başlatır |
| Taramayı Durdur | `BtnTaramaDurdur` | `BtnTaramaDurdur_Click` | CancellationToken iptal |
| ARP Tablosu | `BtnArp` | `BtnArp_Click` | `arp -a` → chat kart + OUI |
| Ağ Bilgisi | `BtnAgBilgi` | `BtnAgBilgi_Click` | `NetworkInterface` → chat kartı |
| Advanced IP Scanner | `BtnCihazlar` | `BtnCihazlar_Click` | Harici exe başlatır |
| SADP | `BtnSadp` | `BtnSadp_Click` | `tools/sadp/sadptool.exe` |
| Rapor Kaydet | `BtnRapor` | `BtnRapor_Click` | Chat → .txt dosyası |
| Ayarlar | `BtnAyarlar` | `BtnAyarlar_Click` | SettingsWindow açar |
| Ekranı Temizle | `BtnTemizle` | `BtnTemizle_Click` | Tarama sırasında disabled |

> Ping, Port, Trace, DNS, WoL, Favoriler, Cihaz Tara, Bant Genişliği artık doğrudan TabControl sekmeleri olarak erişilir — ayrı butonları yok.

### 5.5 Sekmeler (TabControl — x:Name içerikleri)

Her sekme `TabItem` içine konumlanmış bir `Border` (eskiden yan panel) barındırır. `x:Name` referansları kodda korunmuştur.

| Sekme # | Panel x:Name | CTS | İçerik |
|---|---|---|---|
| 2 | `PingPanel` | `_pingCts` | IP giriş, chip'ler, PingResultPanel |
| 3 | `PortPanel` | `_portScanCts` | IP + port aralığı, chip'ler, PortResultPanel |
| 4 | `TracePanel` | `_traceCts` | IP giriş, TraceResultPanel |
| 5 | `DnsPanel` | — | Hostname giriş, DnsResultPanel |
| 6 | `WolPanel` | — | MAC giriş, magic packet gönder |
| 7 | `FavorilerPanel` | — | Favori IP listesi + sil chip'leri |
| 1 | `KameraPanel` | `_kameraCts` | Tam genişlik: subnet giriş, Tara/Durdur, canlı arama, tür filtresi, DataGrid |
| 8 | `BantPanel` | — | Adaptör kartları (canlı hız, 1s timer) |

> `_xxxPanelAcik` boolean flag'leri ve `PingCol` / `KameraPanelGenisligi` sabitleri **kaldırıldı**. Sekme geçişi `MainTabControl.SelectedIndex = TabXxx` ile yapılır.

---

## 6. MainWindow.xaml.cs — Tam İçerik Haritası

### 6.1 Using İfadeleri

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AgTarama.Services;
```

> **Not:** `Lextm.SharpSnmpLib` **kaldırıldı** (SNMP özelliği silindi). NuGet bağımlılığı da yok.

### 6.2 Alanlar

| Alan | Tip | Amaç |
|---|---|---|
| `_ayarlar` | `AppSettings` | `SettingsService.Yukle()` ile başlatılır |
| `HedefMB` | `int` (prop) | `_ayarlar.HedefMB` |
| `HedefKB` | `int` (prop) | `_ayarlar.HedefMB * 1024` |
| `TestSuresiSn` | `int` (prop) | `_ayarlar.TestSuresiSn` |
| `_taramaDevamEdiyor` | bool | Yakalama durumu flag'i |
| `_taramaCts` | `CancellationTokenSource?` | Yakalama iptali |
| `TabChatbot` | const int = 0 | Chatbot sekme indeksi |
| `TabCihazTara` | const int = 1 | Cihaz Tara sekme indeksi |
| `TabPing` | const int = 2 | Ping Testi sekme indeksi |
| `TabPort` | const int = 3 | Port Tara sekme indeksi |
| `TabTrace` | const int = 4 | Traceroute sekme indeksi |
| `TabDns` | const int = 5 | DNS Lookup sekme indeksi |
| `TabWol` | const int = 6 | Wake-on-LAN sekme indeksi |
| `TabFavoriler` | const int = 7 | Favoriler sekme indeksi |
| `TabBant` | const int = 8 | Bant Genişliği sekme indeksi |
| `_pingCts` | `CancellationTokenSource?` | Ping iptali |
| `_portScanCts` | `CancellationTokenSource?` | Port tarama iptali |
| `_traceCts` | `CancellationTokenSource?` | Traceroute iptali |
| `_kameraCts` | `CancellationTokenSource?` | Cihaz tarama iptali |
| `_kameraBilgileri` | `Dictionary<string,KameraBilgi>` | Filtreleme için IP → son cihaz bilgisi cache'i |
| `_kameraSatirlari` | `ObservableCollection<KameraSatir>` | Cihaz Tara DataGrid satır kaynağı |
| `_kameraSatirlar` | `Dictionary<string,KameraSatir>` | IP → DataGrid satırı güncelleme cache'i |
| `_kameraSatirView` | `ICollectionView?` | Sütun filtreleri ve DataGrid görünümü |
| `_bantTimer` | `DispatcherTimer?` | 1s aralıklı bant hızı güncelleme timer'ı |
| `_bantOnceki` | `Dictionary<string,(long Rx, long Tx, long Ts)>` | Önceki snapshot (hız hesabı için) |
| `_toastTimer` | `DispatcherTimer?` | Toast bildirimi auto-hide |
| `_mesajGecmisi` | `List<(string Tur, string Metin, string Zaman)>` | HTML rapor için geçmiş |
| `_captureService` | `CaptureService` | tshark yönetimi |
| `_otomatikGuncelleniyor` | bool | Otomatik nokta ekleme döngü koruması |
| `_oncekiUzunluk` | `Dictionary<TextBox,int>` | Silme/ekleme tespiti |
| `BilindikPortlar` | `static Dictionary<int,string>` | Port → servis adı (24 giriş) |
| `OuiTablosu` | `static Dictionary<string,string>` | MAC OUI (6 hex) → üretici adı (~90 giriş) |

### 6.3 Statik Tablolar

**`BilindikPortlar`** (24 giriş): FTP, SSH, Telnet, SMTP, DNS, HTTP, HTTPS, SMB, RTSP, RDP, MySQL, MSSQL, VNC, Hikvision-SDK vb.

**`OuiTablosu`** (~90 giriş): IEEE OUI prefix (6 hex, büyük harf) → üretici adı. Kapsanan markalar: Hikvision, Dahua, Axis, Reolink, TP-Link, Cisco, MikroTik, Ubiquiti, ASUS, D-Link, NETGEAR, Huawei, Apple, Samsung, Intel, Realtek, ZyXEL, Synology, QNAP, Tenda, VMware, Raspberry Pi.

**`MarkaTablosu`** (40+ giriş): `(anahtar, marka, tur)` dizisi — HTTP banner/title anahtar kelimesi → marka + cihaz türü. Kapsanan: tüm IP kamera markaları, Ubiquiti, MikroTik, TP-Link, Cisco, D-Link, NETGEAR, ZyXEL, ASUS, Huawei, H3C, Ruijie, Tenda, Synology, QNAP, WD, Asustor, Windows/IIS, OpenWrt, DD-WRT, pfSense, Fortinet, SonicWall, Aruba, Juniper, HP ProCurve, HP/Epson/Canon/Brother/Xerox/Kyocera yazıcılar.

**`KameraPorts`** (14 port): `{ 554, 8000, 8080, 37777, 80, 8443, 22, 23, 139, 443, 445, 3389, 9000, 34567 }`

### 6.4 Yardımcı Metotlar

- **`OuiAra(string mac) static`**: MAC'ten ilk 6 hex karakteri alır, `OuiTablosu`'nda arar. Bulamazsa `""` döner.
- **`BantHizFormatla(long bytesPerSec) static`**: `≥1MB → "X.X MB/s"`, `≥1KB → "X.X KB/s"`, diğer → `"X B/s"`.
- **`GecerliIpv4Mu(string) static`**: 4×0-255 oktet kontrolü.
- **`GecerliHostnameMu(string) static`**: `^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$`
- **`HariciAracBaslat(exe, ad)`**: SADP ve IP Scanner için ortak başlatma + `HataBildir` hata yönetimi.
- **`YerelSubnetiBul() static`**: RFC 1918 bloğundaki ilk aktif IP'nin `/24` prefix'ini döner.

### 6.5 Yaşam Döngüsü ve Npcap

- **`MainWindow()`**: `InitializeComponent` → `MesajEkle("sistem",...)` → `BaslangicAsync()` fire-and-forget.
- **`BaslangicAsync()`**: `LogService.OturumBaslat()` + `FavoriChipleriniYenile()` + Npcap kontrol/kurulum + "Sistem hazır" mesajı.
- **`HataBildir(mesaj, ex?)`**: Tüm hata yollarının tek giriş noktası.
- **`NpcapKurulumu() static`**: `HKLM\SOFTWARE\Npcap` registry kontrolü.
- **`NpcapKontrolVeKur()`**: Kurulu değilse `Paths.NpcapInstaller`'ı `runas` ile başlatır (görsel kurulum, `/S` flag yok).

### 6.6 Sekme Geçiş Sistemi

Eski animasyon sistemi (`YanPanelAcAnimasyon`, `YanPanelKapatAnimasyon`, `TumYanPanelleriKapat`, `YanPanelAc`, `SetButonAktif`, `_yanPanelTimer`) **tamamen kaldırıldı**.

- **`MainTabControl_SelectionChanged(sender, e)`**: TabControl sekme değişimini dinler.
  - Sekme 8 (Bant) seçilince → `BantIzlemeBaslat()`, diğer sekmelerde → `_bantTimer?.Stop()`.
  - Sekme 7 (Favoriler) seçilince → `FavorilerPanelGuncelle()`.
  - Sekme 1 (Cihaz Tara) seçilince ve `KameraSubnetBox.Text` boşsa → `YerelSubnetiBul()` ile otomatik doldurulur.
- Tüm sekme geçişleri: `MainTabControl.SelectedIndex = TabXxx;`

### 6.7 Mesaj Sistemi

**`MesajEkle(string tur, string metin)`**: Border + TextBlock + zaman damgası → ChatPanel. ScrollToEnd otomatik.

| `tur` | Arka Plan | Kenarlık | Metin Rengi | Hizalama | Prefix |
|---|---|---|---|---|---|
| `"sistem"` | #161B22 | #21262D | #8B949E | Stretch | `◆ ` |
| `"kullanici"` | #161B22 | #30363D | #C9D1D9 | Right (max 500px) | `› ` |
| `"sonuc"` | #0D3B66 | #1F6FEB | #58A6FF | Stretch | — |
| `"hata"` | #3D1A1A | #8B1A1A | #F85149 | Stretch | `✖ ` |

**`TaramaDurumunuAyarla(bool devamEdiyor)`**: Buton durumları + `StatusText` ("● Hazır" yeşil / "● Yakalanıyor..." sarı) senkronize eder. `BtnTemizle` tarama sırasında `IsEnabled=false`.

### 6.8 Yakalama Akışı (tshark)

1. `BtnTaramaBaslat.IsEnabled = false`, `BtnTemizle.IsEnabled = false`
2. `InterfaceDiscoveryService.TumunuGetirAsync()` → tüm arayüzler (`tshark -D`)
3. `InterfaceDiscoveryService.PaketSayisiAsync` paralel → aktif arayüzler
4. `ArayuzSecimAsync(...)` → ChatPanel'e toggle butonlu kart, kullanıcı seçer
5. `Paths.CapturesKlasor\analiz_ddMMyyyy_HH_mm.pcap` oluştur
6. `_captureService.YakalaAsync(nolar, pcap, hedefKB, onProgress, token)`
7. Tamamlanınca `YakalamaKartiOlustur` karta "Aç" butonu eklenir

**`YakalamaKartiOlustur`** tuple döner: `Kart`, `Guncelle(mb,paket,sure)`, `Tamamla(mb,paket)`, `Durdur()`.

### 6.9 Ping Handler'ları

- **`BtnPing_Click`**: `MainTabControl.SelectedIndex = TabPing` + `PingIpBox.Focus()`.
- **`PingIpBox_TextChanged`**: Canlı doğrulama `✓/~/✗` + `PingBaslatBtn.IsEnabled`.
- **`PingBaslat(string hedef)`**: `PingService.PingleAsync` `IAsyncEnumerable` → `PingResultPanel` satırları. Ana chat'e **yazılmaz**. `LogService.Kaydet("PING", ...)`.
- **`PingKutucugaYaz(string metin, string hex)`**: `PingResultPanel`'e stillenmiş `TextBlock` ekler.
- **`PingFavoriEkle_Click`**: `FavoriService.Ekle(ip)` + `FavoriChipleriniYenile()` + toast.

### 6.10 Port Tarama Handler'ları

- **`BtnPortTara_Click`**: `MainTabControl.SelectedIndex = TabPort`.
- **`PortIpBox_TextChanged`** / **`PortAralikBox_TextChanged`**: Canlı doğrulama + `AktarButonDurumu()`.
- **`PortHizliBtn_Click`**: Chip `Tag`'ini `PortAralikBox.Text`'e yazar.
- **`PortTaraBaslat(string, int[])`**: `PortScanService.TaraAsync` + `SemaphoreSlim(50)` + 1000ms. Açık portlar `PortResultPanel`'e `[AÇIK]` yeşil ile. `LogService.Kaydet("PORT TARA", ...)`.
- **`PortlariParse(string) → int[]`**: "1-1024", "80,443,22" formatını parse eder.

### 6.11 Diğer Panel Handler'ları

- **`TracerouteBaslat`**: `tracert -d` çıktısını satır satır `TraceResultPanel`'e yazar. `LogService.Kaydet("TRACEROUTE", ...)`.
- **`DnsLookupBaslat`**: `Dns.GetHostEntryAsync` + `Dns.GetHostAddressesAsync`. `LogService.Kaydet("DNS", ...)`.
- **`ArpTablosuGoster`**: `arp -a` parse → chat kart. Sütunlar: `IP Adresi | MAC Adresi | Tür | Üretici` (OUI lookup ile). `LogService.Kaydet("ARP", ...)`.
- **`AgAdaptorleriniGoster`**: `NetworkInterface.GetAllNetworkInterfaces()` → chat kart. `LogService.Kaydet("AG BILGI", ...)`.
- **`WolGonder`**: UDP port 9, 255.255.255.255, 6×`FF` + 16×MAC = 102 byte magic packet. `LogService.Kaydet("WAKE-ON-LAN", ...)`.
- **`BtnCihazlar_Click`**: `advanced_ip_scanner.exe` başlatır.
- **`BtnSadp_Click`**: `sadptool.exe` başlatır.
- **`BtnRapor_Click`**: `_mesajGecmisi` → `SaveFileDialog` → `.txt` dosyası.
- **`BtnAyarlar_Click`**: `new SettingsWindow().ShowDialog()`.
- **`BtnTemizle_Click`**: `ChatPanel.Children.Clear()` + sistem mesajı.

### 6.12 Favori IP Sistemi

- **`BtnFavoriler_Click`**: `MainTabControl.SelectedIndex = TabFavoriler` (`FavorilerPanelGuncelle` `SelectionChanged`'dan tetiklenir).
- **`FavorilerPanelGuncelle()`**: `FavoriService.YukleHepsi()` → `FavorilerListePanel`'e IP chip'leri + sil butonu.
- **`FavoriChipleriniYenile()`**: Ping ve Port panellerindeki `PingFavorilerPanel` / `PortFavorilerPanel` chip'lerini günceller.

### 6.13 Bant Genişliği Paneli

- **`BtnBant_Click`**: `MainTabControl.SelectedIndex = TabBant` (timer `SelectionChanged` içinde başlar).
- **`BantIzlemeBaslat()`**: Tüm aktif (Loopback hariç) adaptörler için ilk snapshot alır → `DispatcherTimer` (1s) başlatır. `SelectionChanged`'dan tetiklenir.
- **`BantTimerTick(object?, EventArgs)`**: Her tick'te `NetworkInterface.GetIPv4Statistics()` okur, önceki snapshot ile fark alır → `BantAdaptorPanel`'e kart yeniler. Aktif adaptörler yeşil kenarlık.
- **`BantHizFormatla(long bytesPerSec) static`**: `≥1MB/s`, `≥1KB/s`, `B/s`.

### 6.14 Cihaz Tara (KameraPanel)

**`KameraBilgi` sealed class:**
```
Ip, AcikPortlar, OnvifBulundu, SsdpBulundu, OnvifServisUrl,
OnvifAdi, OnvifHardware, OnvifKonum, RtspDurum, SunucuBasligi, SayfaBasligi,
NetbiosCihazAdi, NetbiosGrupAdi, DnsAdi, PingAdi,
SsdpLocation, SsdpSunucu, SsdpFriendlyName, SsdpManufacturer, SsdpModelName, SsdpModelNumber,
MacAdresi, Uretici, AdvancedScannerAdi, AdvancedScannerServisler, ServisDetaylari,
PingYanit (bool), PingMs (int)
```

**`CihazKimlik` sealed class:** `Marka, Model?, Tur, TurIkon`

**Tür ikonları:**
| Tür | İkon |
|---|---|
| Kamera | ◎ |
| NVR/DVR | ▣ |
| Bilgisayar | ▢ |
| NAS | ▦ |
| Sunucu | ▤ |
| Güvenlik Duvarı | ⊞ |
| Router/AP / Switch | ⊛ |
| Erişim Noktası | ⊛ |
| Telefon | ⊡ |
| Tablet | ▭ |
| Cihaz (fallback) | ◈ |

**`KameraTaramaBaslat()`** — 4 görev `Task.WhenAll` ile paralel:

1. **Port taraması** — 1–254 tüm IP'lere `SemaphoreSlim(80)` + 800ms timeout. `KameraPorts` bağlantısı → 554 açıksa `RtspHizliKontrol` → HTTP port açıksa `HttpBannerOku`.
2. **ONVIF WS-Discovery** — `239.255.255.250:3702` Probe XML → 4sn `ProbeMatch` dinle. `XAddrs`, scope `name`/`hardware`/`location` okunur.
3. **SSDP/UPnP** — `239.255.255.250:1900` M-SEARCH → 3sn. Subnet filtresi (`ip.StartsWith(subnet+".")`) uygulanır. `LOCATION` varsa cihaz açıklama XML'i okunur; `friendlyName`, `manufacturer`, `modelName`, `modelNumber` karta işlenir.
4. **Ping Sweep** — 1–254 tüm IP'lere `SemaphoreSlim(64)` + `Ping.SendPingAsync` 1000ms. Yanıt verirse `bilgi.PingYanit=true`, `bilgi.PingMs=roundtrip`. Port açık olmadan ICMP'ye yanıt veren cihazlar da bulunur.
5. **Katmanlı cihaz adı** — ping yanıtı veren veya 139/445/3389 portlarından biri açık görünen IP'lerde `NetbiosService.SorgulaAsync` çağrılır. Servis sırayla UDP 137 NetBIOS Node Status, reverse DNS, `ping -a` ve `nbtstat -A` kaynaklarını paralel dener. Aynı IP için tek deneme yapılır, `SemaphoreSlim(16)` ile süreç sayısı sınırlanır. Kartta `Ad`, `Marka`, `Model`, `Grup`, `Konum` satırları gösterilir.
5b. **NetBIOS sweep** — tüm subnet'e hafif UDP 137 Node Status sorgusu yapılır (`NetbiosSweepAsync`, `SemaphoreSlim(64)`). Ping kapalı ama NetBIOS açık Windows cihazları da adlarıyla karta düşebilir.
6. **Advanced IP Scanner console zenginleştirme** — `AdvancedIpScannerService.TaraAsync` arka planda `advanced_ip_scanner_console.exe /r:<subnet>.1-<subnet>.254 /f:<temp> /v2` çalıştırır. Timeout dolarsa sessizce atlanır; sonuç gelirse IP/ad/MAC/üretici/servis bilgisi mevcut kartlara işlenir.
7. **ARP/MAC/OUI zenginleştirme** — tarama sonunda `arp -a` parse edilir. IP → MAC eşleşmesi kartlara eklenir. Üretici için önce `OuiAra`, sonra `tools\Ip_Scanner\mac_interval_tree.txt` prefix veritabanı kullanılır.

Her bulgu `ConcurrentDictionary<string, KameraBilgi>` ile dedup edilir → `Dispatcher.InvokeAsync` → `KameraKartEkleVeyaGuncelle`.

**`KimlikBelirle(KameraBilgi) static`**: Yazıcı ipuçlarını ve XVR/NVR/DVR ipuçlarını marka tablosundan önce yakalar; böylece HP/Epson yazıcılar router/switch, Hikvision/Dahua kayıt cihazları kamera olarak sınıflanmaz. Kayıt cihazı ipuçları: `xvr`, `nvr`, `dvr`, recorder başlıkları, `DS-`/`DH-` model desenleri, 34567 veya 9000+554 port kombinasyonu. Ardından `MarkaTablosu` → Server header + page title + ONVIF name/hardware + SSDP friendlyName/manufacturer/model/server → marka/tür. Port bazlı fallback: 34567/9000+554 → NVR/DVR, 445/3389 → Bilgisayar, NetBIOS/DNS/ping adı → Bilgisayar, 23 → Router/Switch. **Telefon tespiti** (üç katman): (1) MarkaTablosu'nda `android`, `miui`, `iphone`, `ipad`, `oneplus`, `oppo`, `vivo` anahtar kelimeleri → Telefon/Tablet; (2) DNS/ping/SSDP hostname'de `iphone`, `ipad`, `android-`, `galaxy`, `redmi`, `xiaomi`, `poco`, `pixel` → Telefon/Tablet; (3) OUI üreticisi mobil marka + sunucu portu yok (22/80/443/445/554/3389/8080/8443/8000) → Telefon.

**`KayitCihaziIpuclariVar(string, ICollection<int>) static`**: NVR/XVR/DVR metin, model ve port ipuçlarını tek yerde değerlendirir.

**`YaziciIpuclariVar(string, ICollection<int>) static`**: HP LaserJet, Epson, Canon, Brother, Xerox, Kyocera, `printer`/`MFP` metinleri ve 9100/515/631 portlarıyla yazıcı türünü belirler.

**Cihaz Tara tablo görünümü:** `KameraDataGrid` koyu temalı, sıralanabilir bir DataGrid'dir. Sütunlar: IP, Ad, Tür, Marka, Model, Ping, Portlar, Keşif, MAC, Üretici, Servis. Satıra çift tıklama web arayüzünü açar (`WebUrl` yoksa `http://IP/` denenir). Sağ tık menüsü: Web arayüzünü aç, Ping at, Port tara, Traceroute, DNS lookup, IP kopyala, Favorilere ekle, Excel/PDF/TXT/CSV dışa aktar. Filtre satırında `Dışa Aktar` chip butonu aynı format seçeneklerini açar.

**Cihaz Tara sütun filtreleri:** `KameraIpFiltreBox`, `KameraAdFiltreBox`, `KameraTurFiltreBox`, `KameraMarkaFiltreBox`, `KameraPortFiltreBox`, `KameraMacFiltreBox` DataGrid görünümünü sütun bazlı filtreler. Tür filtresi `Yazıcı` dahil cihaz türlerini içerir. `KameraFiltreSayacText` görünür/toplam cihaz sayısını gösterir.

**`KameraSatir` sealed class:** `INotifyPropertyChanged` uygular; DataGrid için IP/ad/tür/marka/model/ping/port/keşif/MAC/üretici/servis/web URL alanlarını taşır.

**`KameraSatirOlustur(KameraBilgi)`**: Tarama bulgusunu DataGrid satır modeline dönüştürür.

**`KameraFiltreleriUygula()`**: `_kameraSatirView.Refresh()` çağırır ve görünür/toplam sayaç metnini günceller.

**`KameraSatirFiltredenGecer(object)`**: IP, ad, tür, marka/üretici, port/servis/keşif ve MAC sütun filtrelerini uygular.

**`KameraKolonFiltre_TextChanged`**, **`KameraTurFiltreDegisti`**, **`KameraFiltreTemizle_Click`**, **`KameraDataGrid_MouseDoubleClick`**, **`KameraDataGrid_PreviewMouseRightButtonDown`**: DataGrid filtre ve satır etkileşim event handler'ları.

**`KameraMenuWeb_Click`**, **`KameraMenuPing_Click`**, **`KameraMenuPort_Click`**, **`KameraMenuTrace_Click`**, **`KameraMenuDns_Click`**, **`KameraMenuIpKopyala_Click`**, **`KameraMenuFavoriEkle_Click`**: Cihaz Tara sağ tık menü komutları; ilgili sekmeye geçer, IP'yi doldurur ve komutu başlatır.

**`SeciliKameraSatiri()`**, **`KameraWebArayuzunuAc(KameraSatir)`**, **`UstOgeBul<T>(DependencyObject?) static`**: Sağ tık/çift tık yardımcıları; satır seçimi ve web arayüzü açma davranışını ortaklaştırır.

**`KameraDisaAktarBtn_Click`**, **`KameraExportExcel_Click`**, **`KameraExportPdf_Click`**, **`KameraExportTxt_Click`**, **`KameraExportCsv_Click`**, **`KameraDisariAktar(KameraExportFormat)`**: Görünür/filtrelenmiş Cihaz Tara satırlarını `SaveFileDialog` ile Excel `.xls` (stilli HTML tablo), PDF `.pdf` (başlıklı sayfalı envanter), TXT `.txt` (hizalı metin raporu) veya CSV `.csv` (UTF-8, `;` ayracı) olarak dışa aktarır.

**`KameraGorunenSatirlariAl()`**, **`IpSiralamaAnahtari(string) static`**, **`KameraExportSatirlari(...) static`**, **`KameraCsvOlustur(...) static`**, **`KameraTxtOlustur(...) static`**, **`KameraExcelHtmlOlustur(...) static`**, **`KameraPdfOlustur(...) static`**, **`KameraPdfSayfaIcerigi(...) static`**, **`PdfMetin(...) static`**, **`PdfAscii(...) static`**, **`MetniKirp(...) static`**: Dışa aktarma veri hazırlama ve format üretim yardımcıları.

**`HttpBannerOku(ip, port, token) static`**: TCP HTTP GET `/` → `Server:` header + `<title>` (2.5sn timeout). `(Sunucu, Baslik)` tuple döner.

**`RtspHizliKontrol(ip, port, token) static`**: Anonim RTSP DESCRIBE → ilk satır durum kodu (2sn timeout).

**`CihazAdiSec(KameraBilgi) static`**: NetBIOS → kısa DNS → kısa `ping -a` → ONVIF name → SSDP friendlyName önceliğiyle kartta gösterilecek en iyi adı seçer.

**`IlkDolu(...)`, `KisaHostAdi(...)`, `AnlamliSayfaBasligi(...)`, `TemizKimlikMetni(...)`**: Kimlik/metin normalizasyonu ve gürültülü başlıkları eleme yardımcıları.

**`NetbiosBilgileriniGuncelleAsync(ip, bilgi, denenenler, logSatirlari, netbiosSem, token)`**: Aynı IP için tek katmanlı ad sorgusu yapar, NetBIOS/DNS/ping-a adlarını `KameraBilgi`'ye işler ve kartı yeniler.

**`NetbiosSweepAsync(subnet, bulunanlar, logSatirlari, token)`**: Tüm subnet'e UDP 137 Node Status sorgusu yapar; yanıt veren Windows cihazlarının ad/workgroup bilgisini karta ekler.

**`HttpBasliklariniParse(resp) static`**: SSDP/HTTP cevap başlıklarını dictionary olarak parse eder.

**`SsdpDetayOku(location, token) static`**: UPnP `LOCATION` XML'inden `friendlyName`, `manufacturer`, `modelName`, `modelNumber` okur.

**`XmlEtiketiOku(xml, etiket) static`**: Basit XML etiket değerlerini güvenli şekilde parse eder.

**`AdvancedScannerKayitlariniIsleAsync(subnet, bulunanlar, logSatirlari, token)`**: AIS console sonucunu mevcut `KameraBilgi` kartlarıyla birleştirir.

**`ArpBilgileriniTopluGuncelleAsync(...)`** / **`ArpTablosuOkuAsync(token)`**: Windows ARP tablosundan MAC adreslerini alır ve kartları yeniler.

**`UreticiAra(mac)`, `IpScannerMacDbYukle()`, `MacFormatla(mac)`**: MAC üretici çözümleme ve format yardımcıları.

**`ServisDetaylariniGuncelleAsync(...)`, `PortBannerOku(...)`, `BannerTemizle(...)`**: Açık portlar için servis adı ve mümkünse kısa banner üretir.

**`KartSatir(metin, hex) static`**: Kart içi düz metin `TextBlock`.

**`KartLink(etiket, url) static`**: `Hyperlink` → `Process.Start(UseShellExecute)`.

---

## 7. Harici Bağımlılıklar (runtime)

| Dosya | Yer | Amaç | Otomatik mi? |
|---|---|---|---|
| `tshark.exe` | `tools\WiresharkPortable64\App\Wireshark\` | Paket yakalama | Manuel |
| `WiresharkPortable64.exe` | `tools\WiresharkPortable64\` | pcap görüntüleme | Manuel |
| `npcap-1.88.exe` | `Req\` | Sürücü installer | İlk açılışta otomatik UAC |
| `advanced_ip_scanner.exe` | `tools\Ip_Scanner\` | Cihaz listesi | Manuel |
| `advanced_ip_scanner_console.exe` | `tools\Ip_Scanner\` | Cihaz Tara zenginleştirme (opsiyonel console veri kaynağı) | Cihaz Tara içinde otomatik, timeout'lu |
| `mac_interval_tree.txt` | `tools\Ip_Scanner\` | MAC prefix → üretici veritabanı | Cihaz Tara içinde otomatik |
| `sadptool.exe` | `tools\sadp\` | Hikvision SADP | Manuel |

> **NuGet paketi yok.** `Lextm.SharpSnmpLib` kaldırıldı (SNMP özelliği silindi).

---

## 8. Mesaj Türleri Hızlı Referans

```csharp
MesajEkle("sistem",    "...")  // gri, ◆ prefix
MesajEkle("kullanici", "...")  // sağda, › prefix
MesajEkle("sonuc",     "...")  // mavi, başarı/sonuç
MesajEkle("hata",      "...")  // kırmızı, ✖ prefix
```

---

## 9. Geliştirme Kuralları

- Tüm ağ işlemleri `async/await` + `CancellationToken` ile yapılmalı; UI thread bloke edilmemeli.
- Sonuçlar `MesajEkle("sonuc", ...)` ile chat'e; panel sonuçları kendi `XxxResultPanel`'e yazılır — ana chat'e **yazılmaz**.
- Yeni araç butonu başlık kartındaki `WrapPanel`'e eklenir. Sekme yönlendirme butonu için `MainTabControl.SelectedIndex = TabXxx` kullanılır; doğrudan işlem yapan butonlar (ARP, ağ bilgisi) için doğrudan metot çağırılır.
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`'da tanımlanır. `ActiveActionButton`, `ActionButton`'dan **SONRA** tanımlanmalı (StaticResource forward-ref hatası).
- .NET 10 / WPF — `LetterSpacing` gibi web CSS özellikleri yoktur.
- Harici araç başlatma için `HariciAracBaslat(exe, ad)` kullanılır.
- Toast bildirimi için `ToastGoster(mesaj, hata:bool)` kullanılır.

---

## 10. Tamamlanan Özellikler

| # | Özellik | Metot / Panel |
|---|---|---|
| ✅ | Taramayı Başlat / Durdur | `YakalamaBaslat` + `CaptureService` |
| ✅ | Ping Testi | `PingBaslat` + `PingPanel` |
| ✅ | Port Tara | `PortTaraBaslat` + `PortPanel` |
| ✅ | Traceroute | `TracerouteBaslat` + `TracePanel` |
| ✅ | DNS Lookup | `DnsLookupBaslat` + `DnsPanel` |
| ✅ | ARP Tablosu + OUI üretici | `ArpTablosuGoster` → chat kart |
| ✅ | Ağ Adaptörü Bilgisi | `AgAdaptorleriniGoster` → chat kart |
| ✅ | Wake-on-LAN | `WolGonder` + `WolPanel` |
| ✅ | Advanced IP Scanner | `HariciAracBaslat` |
| ✅ | Advanced IP Scanner console zenginleştirme | `AdvancedIpScannerService` + `AdvancedScannerKayitlariniIsleAsync` |
| ✅ | SADP | `HariciAracBaslat` |
| ✅ | Cihaz Tara (Kamera+Router+PC+NVR…) | `KameraTaramaBaslat` + `KameraPanel` |
| ✅ | Cihaz Tara DataGrid envanter UI + sütun filtreleri | `KameraDataGrid` + `KameraSatirFiltredenGecer` |
| ✅ | Ping Sweep (Cihaz Tara içinde) | 4. paralel görev, `PingYanit/PingMs` |
| ✅ | Katmanlı cihaz adı/model çözümleme (Cihaz Tara içinde) | `NetbiosService` + ONVIF/SSDP ad-model parse |
| ✅ | ARP/MAC/OUI + servis banner detayları (Cihaz Tara içinde) | `ArpBilgileriniTopluGuncelleAsync` + `ServisDetaylariniGuncelleAsync` |
| ✅ | ONVIF WS-Discovery | Cihaz Tara 2. görev |
| ✅ | SSDP/UPnP keşif | Cihaz Tara 3. görev |
| ✅ | MAC OUI üretici sorgusu | `OuiTablosu` + `OuiAra` |
| ✅ | Bant Genişliği Monitörü | `BantIzlemeBaslat` + `BantPanel` |
| ✅ | Favori IP Listesi | `FavoriService` + `FavorilerPanel` |
| ✅ | Otomatik Log | `LogService` → `%APPDATA%\AgTarama\logs\` |
| ✅ | Wireshark "Aç" butonu | Yakalama tamamlanınca dinamik eklenir |
| ✅ | Ayarlar (HedefMB, TestSuresiSn) | `SettingsService` + `SettingsWindow` |
| ✅ | Rapor Kaydet | `BtnRapor_Click` → .txt |
| ✅ | Koyu ToolTip stili | `Window.Resources` default ToolTip |
| ✅ | `ⓘ` bilgi badge (tüm butonlar) | Grid içerik + ToolTip |
| ✅ | Tam ekran başlatma | `WindowState="Maximized"` |
| ✅ | Sekme tabanlı tam ekran UI (TabControl mimarisi) | `MainTabControl` — 9 sekme, animasyonsuz, her araç tam genişlik |

---

## 11. Açık TODO / Sonraki Adaylar (EKLENECEKLER.md + codex-eklenebilecekler.md)

| Öncelik | Özellik | Zorluk |
|---|---|---|
| 🔴 | HTTP Header Checker | Kolay |
| 🔴 | Cihaz Tara kartlarına MAC + OUI ekleme | Kolay-Orta |
| 🟡 | mDNS Keşif | Orta |
| 🟡 | Sonuç JSON/CSV Export | Kolay |
| 🟡 | Port Banner / Servis Sürüm Tespiti | Orta |
| 🟡 | `MainWindow.xaml.cs` partial dosyalara bölme | Orta |
| 🟡 | HTTPS Sertifika Denetleyicisi | Orta |
| 🟢 | RTSP Önizleme (ffmpeg) | Zor |
| 🟢 | ARP Spoof Tespiti | Orta |
| 🟢 | Rogue DHCP Sunucu Tespiti | Orta-Zor |
| 🟢 | WiFi Ağ Tarayıcı | Kolay |
| 🔵 | CVE Kontrolü | Zor |
| 🔵 | SSH Komutu Çalıştırıcı | Zor |
| 🔵 | Geçmiş / Son Taramalar | Orta |
| 🔵 | Cihaz Tara servis katmanına alma | Orta-Zor |
| ✅ | Çoklu Sekme / Çalışma Alanı | TabControl mimarisi uygulandı |

---

## 12. Git Durumu (snapshot 2026-05-12)

- **Branch:** `VOL-2`
- **Main:** `main`
- **Son commitler:**
  - `6c6d8e9` refactor: remove SNMP and subnet modules, add bandwidth monitoring and OUI lookup database
  - `9a5d5b6` feat: add missing dependencies and documentation files for Wireshark, IP Scanner, and SADP tools
  - `016bdbb` Merge pull request #3 from Crakkadmr/yeni_ozelliklerpart2
  - `74a18df` feat: implement application settings system and favorite IP management with toast notifications
  - `c2b8c1c` refactor: extract network operations to service layer and centralize configuration into Paths and LogService
