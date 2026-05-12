# AGENTS.md — Proje Tam Referans ve Geliştirici Rehberi

> Bu dosya AI agent'larının projeyi tek yerden anlayabilmesi için hazırlanmıştır.
> **Kaynak kodda her değişiklik yapıldığında bu dosya da aynı turda güncellenmelidir.**
> Son güncelleme: 2026-05-12 (Faz 12: Bant Genişliği paneli, OUI üretici sorgulama, Ping Sweep,
> rdp:// linkleri kaldırıldı, koyu ToolTip stili, WindowState=Maximized, SNMP+Subnet kaldırıldı)

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
| Ad | AG TARAMA PROGRAMI (AgTarama) |
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

WPF tabanlı, **chatbot arayüzlü ağ tarama ve paket yakalama uygulaması**. Aktif ağ arayüzlerini
otomatik tespit eder, seçilen arayüzler üzerinde **tshark** ile paket yakalama gerçekleştirir,
sonuçları **Wireshark Portable** ile analiz etmeye hazırlar.

Ek özellikler: ping testi, port tarama, traceroute, DNS lookup, ARP tablosu (OUI üretici gösterimi),
ağ adaptörü bilgisi, bant genişliği monitörü, Wake-on-LAN, Cihaz Tara (port scan + ONVIF + SSDP + Ping
Sweep), favori IP listesi, Advanced IP Scanner entegrasyonu, SADP entegrasyonu.

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
    ├── MainWindow.xaml               ← UI tasarımı + stiller (~520 satır)
    ├── MainWindow.xaml.cs            ← UI wiring + event handler'lar (~1950 satır)
    ├── Paths.cs                      ← Tüm exe-relative yol sabitleri (static)
    ├── LogService.cs                 ← %APPDATA%\AgTarama\logs\YYYYMMDD.log
    ├── Services/
    │   ├── InterfaceDiscoveryService.cs  ← tshark -D + paket sayısı testi
    │   ├── CaptureService.cs             ← tshark yakalama + ilerleme callback
    │   ├── PingService.cs                ← IAsyncEnumerable ping akışı
    │   ├── PortScanService.cs            ← Parse + async port tarama
    │   ├── AppSettings.cs                ← Ayar modeli (HedefMB, TestSuresiSn vb.)
    │   ├── SettingsService.cs            ← JSON serileştirme (%APPDATA%)
    │   └── FavoriService.cs              ← Favori IP CRUD (%APPDATA%)
    ├── SettingsWindow.xaml / .cs     ← Ayarlar penceresi
    ├── AGENTS.md                     ← (bu dosya) tam referans + geliştirici rehberi
    ├── AGENT.md                      ← Claude Code için referans (Claude'a özgü kurallar içerir)
    ├── EKLENECEKLER.md               ← Geliştirme yol haritası (25+ özellik)
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
- `Paths.cs` — tüm exe-relative yol sabitleri tek yerde (`AppBase`, `TsharkExe`, `SadpExe` vb.)
- `LogService.cs` — `%APPDATA%\AgTarama\logs\YYYYMMDD.log`'a yazar; `OturumBaslat`, `Kaydet`, `Hata` metotları
- `Services/InterfaceDiscoveryService` — `tshark -D` parse + 2s paket sayısı testi
- `Services/CaptureService` — tshark process yönetimi, progress callback (`Action<double, int, TimeSpan>`)
- `Services/PingService` — `IAsyncEnumerable<PingSonuc>` akışı (4 ping, TTL, hata sarmalı)
- `Services/PortScanService` — `Parse(string)` + `TaraAsync(...)` (SemaphoreSlim 50, 1000ms timeout)
- `Services/SettingsService` — `Yukle()` / `Kaydet(AppSettings)` JSON serileştirme
- `Services/FavoriService` — `Ekle`, `Sil`, `YukleHepsi` CRUD
- `MainWindow` → `HataBildir(mesaj, ex?)` — chat kırmızı mesaj + `LogService.Hata` tek noktadan

### 5.1 UI Düzeni (MainWindow.xaml)

- `WindowState="Maximized"` — uygulama tam ekran açılır
- **Sol** (`*` genişlik): Başlık (`AG TARAMA` + `StatusText`) + Chat alanı (`ChatScrollViewer` → `ChatPanel` StackPanel) + Animasyonla açılan yan panel (`PingCol` ColumnDefinition, 0 → 340px)
- **Sağ** (220px sabit): Kontrol butonları StackPanel'i + alt versiyon yazısı

Tüm yan paneller `PingCol` sütununu paylaşır — aynı anda yalnızca biri açık olabilir.

### 5.2 Stil Sistemi (Window.Resources)

| Kaynak Anahtar | Tip | Kullanım |
|---|---|---|
| `ActionButton` | Button | Standart sağ panel butonu (koyu mavi, 44px yükseklik) |
| `ActiveActionButton` | Button | Aktif panel butonu — yeşil çerçeve (#3FB950, 2px), yeşilimsi bg. **`ActionButton`'dan SONRA tanımlanmalı** |
| `PrimaryButton` | Button | Yeşil "Başlat" butonu (48px, BasedOn ActionButton) |
| `DangerButton` | Button | Kırmızı "Durdur" butonu (BasedOn ActionButton) |
| `PingInputBox` | TextBox | Yan panel IP/değer giriş kutusu |
| `ChipButton` | Button | Hızlı seçim chip'leri (yuvarlak, CornerRadius=12) |
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

### 5.4 Kontrol Butonları (sağ panel — sırayla)

| Adı | x:Name | Click Handler | Açıklama |
|---|---|---|---|
| Taramayı Başlat | `BtnTaramaBaslat` | `BtnTaramaBaslat_Click` | tshark yakalama başlatır |
| Taramayı Durdur | `BtnTaramaDurdur` | `BtnTaramaDurdur_Click` | CancellationToken iptal |
| ─ ayırıcı ─ | | | |
| Ping Testi | `BtnPing` | `BtnPing_Click` | Yan panel |
| Port Tara | `BtnPortTara` | `BtnPortTara_Click` | Yan panel |
| Traceroute | `BtnTrace` | `BtnTrace_Click` | Yan panel (`tracert -d`) |
| DNS Lookup | `BtnDns` | `BtnDns_Click` | Yan panel |
| ─ ayırıcı ─ | | | |
| Advanced IP Scanner | `BtnCihazlar` | `BtnCihazlar_Click` | Harici exe başlatır |
| ARP Tablosu | `BtnArp` | `BtnArp_Click` | `arp -a` → chat kart + OUI |
| Ağ Bilgisi | `BtnAgBilgi` | `BtnAgBilgi_Click` | `NetworkInterface` → chat kartı |
| SADP | `BtnSadp` | `BtnSadp_Click` | `tools/sadp/sadptool.exe` |
| Wake-on-LAN | `BtnWol` | `BtnWol_Click` | Yan panel (UDP magic packet) |
| Cihaz Tara | `BtnKamera` | `BtnKamera_Click` | Yan panel (port+ONVIF+SSDP+Ping Sweep) |
| ─ ayırıcı ─ | | | |
| Favoriler | `BtnFavoriler` | `BtnFavoriler_Click` | Yan panel |
| Bant Genişliği | `BtnBant` | `BtnBant_Click` | Yan panel (canlı ↓↑ hız) |
| Rapor Kaydet | `BtnRapor` | `BtnRapor_Click` | Chat → .txt dosyası |
| Ayarlar | `BtnAyarlar` | `BtnAyarlar_Click` | SettingsWindow açar |
| ─ ayırıcı ─ | | | |
| Ekranı Temizle | `BtnTemizle` | `BtnTemizle_Click` | Tarama sırasında disabled |

### 5.5 Yan Paneller (PingCol paylaşımlı, 340px)

| Panel x:Name | Flag | CTS | İçerik |
|---|---|---|---|
| `PingPanel` | `_pingPanelAcik` | `_pingCts` | IP giriş, chip'ler, sonuç kutusu |
| `PortPanel` | `_portPanelAcik` | `_portScanCts` | IP + port aralığı, chip'ler, sonuç |
| `TracePanel` | `_tracePanelAcik` | `_traceCts` | IP giriş, sonuç kutusu |
| `DnsPanel` | `_dnsPanelAcik` | — | Hostname giriş, sonuç kutusu |
| `WolPanel` | `_wolPanelAcik` | — | MAC giriş, magic packet gönder |
| `FavorilerPanel` | `_favorilerPanelAcik` | — | Favori IP listesi + sil chip'leri |
| `KameraPanel` | `_kameraPanelAcik` | `_kameraCts` | Subnet giriş, cihaz kart listesi |
| `BantPanel` | `_bantPanelAcik` | — | Adaptör kartları (canlı hız, 1s timer) |

---

## 6. MainWindow.xaml.cs — Tam İçerik Haritası

### 6.1 Using İfadeleri

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
| `_pingPanelAcik` | bool | Ping paneli durumu |
| `_pingCts` | `CancellationTokenSource?` | Ping iptali |
| `PingPanelGenisligi` | const double = 340 | Yan panel hedef genişliği |
| `_portPanelAcik` | bool | Port tara paneli durumu |
| `_portScanCts` | `CancellationTokenSource?` | Port tarama iptali |
| `_tracePanelAcik` | bool | Traceroute paneli durumu |
| `_traceCts` | `CancellationTokenSource?` | Traceroute iptali |
| `_dnsPanelAcik` | bool | DNS paneli durumu |
| `_wolPanelAcik` | bool | Wake-on-LAN paneli durumu |
| `_favorilerPanelAcik` | bool | Favoriler paneli durumu |
| `_kameraPanelAcik` | bool | Cihaz Tara paneli durumu |
| `_kameraCts` | `CancellationTokenSource?` | Cihaz tarama iptali |
| `_bantPanelAcik` | bool | Bant Genişliği paneli durumu |
| `_bantTimer` | `DispatcherTimer?` | 1s aralıklı bant hızı güncelleme timer'ı |
| `_bantOnceki` | `Dictionary<string,(long Rx, long Tx, long Ts)>` | Önceki snapshot (hız hesabı için) |
| `_yanPanelTimer` | `DispatcherTimer?` | Tek aktif panel animasyon timer'ı (çift tık race condition önler) |
| `_toastTimer` | `DispatcherTimer?` | Toast bildirimi auto-hide |
| `_aktifPanelBtn` | `Button?` | Şu an açık panelin sağ panel butonu |
| `_mesajGecmisi` | `List<(string Tur, string Metin, string Zaman)>` | HTML rapor için geçmiş |
| `_captureService` | `CaptureService` | tshark yönetimi |
| `_otomatikGuncelleniyor` | bool | Otomatik nokta ekleme döngü koruması |
| `_oncekiUzunluk` | `Dictionary<TextBox,int>` | Silme/ekleme tespiti |
| `BilindikPortlar` | `static Dictionary<int,string>` | Port → servis adı (24 giriş) |
| `OuiTablosu` | `static Dictionary<string,string>` | MAC OUI (6 hex) → üretici adı (~90 giriş) |

### 6.3 Statik Tablolar

**`BilindikPortlar`** (24 giriş): FTP, SSH, Telnet, SMTP, DNS, HTTP, HTTPS, SMB, RTSP, RDP, MySQL, MSSQL, VNC, Hikvision-SDK vb.

**`OuiTablosu`** (~90 giriş): IEEE OUI prefix (6 hex, büyük harf) → üretici adı. Kapsanan markalar: Hikvision, Dahua, Axis, Reolink, TP-Link, Cisco, MikroTik, Ubiquiti, ASUS, D-Link, NETGEAR, Huawei, Apple, Samsung, Intel, Realtek, ZyXEL, Synology, QNAP, Tenda, VMware, Raspberry Pi.

**`MarkaTablosu`** (40+ giriş): `(anahtar, marka, tur)` dizisi — HTTP banner/title anahtar kelimesi → marka + cihaz türü. Kapsanan: tüm IP kamera markaları, Ubiquiti, MikroTik, TP-Link, Cisco, D-Link, NETGEAR, ZyXEL, ASUS, Huawei, H3C, Ruijie, Tenda, Synology, QNAP, WD, Asustor, Windows/IIS, OpenWrt, DD-WRT, pfSense, Fortinet, SonicWall, Aruba, Juniper, HP ProCurve.

**`KameraPorts`** (13 port): `{ 554, 8000, 8080, 37777, 80, 8443, 22, 23, 443, 445, 3389, 9000, 34567 }`

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

### 6.6 Panel Animasyon Sistemi

- **`YanPanelAcAnimasyon()`** / **`YanPanelKapatAnimasyon(Action)`**: Her çağrıda önce `_yanPanelTimer?.Stop()` ile önceki timer durdurulur — çift tık race condition önlenir.
- **`TumYanPanelleriKapat()`**: Tüm panel flag + Visibility + CTS iptal + `_bantTimer?.Stop()` + `SetButonAktif(null)` + `PingCol.Width=0`.
- **`YanPanelAc(ref bool flag, UIElement panel)`**: `TumYanPanelleriKapat()` → flag=true → panel.Visible → animasyon. Aynı butona ikinci basışta kapatır (toggle).
- **`SetButonAktif(Button?)`**: Style switching — `ActionButton` ↔ `ActiveActionButton`. DataTrigger/Tag değil (IsMouseOver çakışması yok).

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

- **`BtnPing_Click`**: `YanPanelAc(ref _pingPanelAcik, PingPanel)` toggle + `SetButonAktif`.
- **`PingIpBox_TextChanged`**: Canlı doğrulama `✓/~/✗` + `PingBaslatBtn.IsEnabled`.
- **`PingBaslat(string hedef)`**: `PingService.PingleAsync` `IAsyncEnumerable` → `PingResultPanel` satırları. Ana chat'e **yazılmaz**. `LogService.Kaydet("PING", ...)`.
- **`PingKutucugaYaz(string metin, string hex)`**: `PingResultPanel`'e stillenmiş `TextBlock` ekler.
- **`PingFavoriEkle_Click`**: `FavoriService.Ekle(ip)` + `FavoriChipleriniYenile()` + toast.

### 6.10 Port Tarama Handler'ları

- **`BtnPortTara_Click`**: Panel toggle.
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

- **`BtnFavoriler_Click`**: `FavorilerPanelGuncelle()` + panel toggle.
- **`FavorilerPanelGuncelle()`**: `FavoriService.YukleHepsi()` → `FavorilerListePanel`'e IP chip'leri + sil butonu.
- **`FavoriChipleriniYenile()`**: Ping ve Port panellerindeki `PingFavorilerPanel` / `PortFavorilerPanel` chip'lerini günceller.

### 6.13 Bant Genişliği Paneli

- **`BtnBant_Click`**: Panel toggle. Açarken `BantIzlemeBaslat()` çağırır.
- **`BantPanelKapat_Click`**: `_bantTimer?.Stop()` + animasyonla kapat.
- **`BantIzlemeBaslat()`**: Tüm aktif (Loopback hariç) adaptörler için ilk snapshot alır → `DispatcherTimer` (1s) başlatır.
- **`BantTimerTick(object?, EventArgs)`**: Her tick'te `NetworkInterface.GetIPv4Statistics()` okur, önceki snapshot ile fark alır → `BantAdaptorPanel`'e kart yeniler. Aktif adaptörler yeşil kenarlık.
- **`BantHizFormatla(long bytesPerSec) static`**: `≥1MB/s`, `≥1KB/s`, `B/s`.

### 6.14 Cihaz Tara (KameraPanel)

**`KameraBilgi` sealed class:**
```
Ip, AcikPortlar, OnvifBulundu, SsdpBulundu, OnvifServisUrl,
OnvifHardware, RtspDurum, SunucuBasligi, SayfaBasligi,
PingYanit (bool), PingMs (int)
```

**`CihazKimlik` sealed class:** `Marka, Model?, Tur, TurIkon`

**Tür ikonları:**
| Tür | İkon |
|---|---|
| Kamera | ◉ |
| NVR/DVR | ▣ |
| Bilgisayar | ▢ |
| NAS | ▦ |
| Sunucu | ▤ |
| Güvenlik Duvarı | ⊞ |
| Router/Switch | ⊛ |
| Erişim Noktası | ◈ |
| Cihaz (fallback) | ◈ |

**`KameraTaramaBaslat()`** — 4 görev `Task.WhenAll` ile paralel:

1. **Port taraması** — 1–254 tüm IP'lere `SemaphoreSlim(80)` + 800ms timeout. `KameraPorts` bağlantısı → 554 açıksa `RtspHizliKontrol` → HTTP port açıksa `HttpBannerOku`.
2. **ONVIF WS-Discovery** — `239.255.255.250:3702` Probe XML → 4sn `ProbeMatch` dinle. `XAddrs`, scope `hardware`/`name` okunur.
3. **SSDP/UPnP** — `239.255.255.250:1900` M-SEARCH → 3sn. Subnet filtresi (`ip.StartsWith(subnet+".")`) uygulanır.
4. **Ping Sweep** — 1–254 tüm IP'lere `SemaphoreSlim(64)` + `Ping.SendPingAsync` 1000ms. Yanıt verirse `bilgi.PingYanit=true`, `bilgi.PingMs=roundtrip`. Port açık olmadan ICMP'ye yanıt veren cihazlar da bulunur.

Her bulgu `ConcurrentDictionary<string, KameraBilgi>` ile dedup edilir → `Dispatcher.InvokeAsync` → `KameraKartEkleVeyaGuncelle`.

**`KimlikBelirle(KameraBilgi) static`**: `MarkaTablosu` → Server header + page title + ONVIF hardware → marka/tür. Port bazlı fallback: 34567/9000+554 → NVR/DVR, 445/3389 → Bilgisayar, 23 → Router/Switch.

**`KameraKartIcDoldur`** gösterir: TürIkon+IP+Marka — Model başlık, Tür, **Ping (ms)**, Portlar, Sunucu, RTSP durumu, ONVIF/UPnP badge, linkler (`http/https/rtsp/ssh/onvif`).
> **rdp:// linki yoktur** — 3389 portu taranır ama kart üzerinde link gösterilmez.

**`HttpBannerOku(ip, port, token) static`**: TCP HTTP GET `/` → `Server:` header + `<title>` (2.5sn timeout). `(Sunucu, Baslik)` tuple döner.

**`RtspHizliKontrol(ip, port, token) static`**: Anonim RTSP DESCRIBE → ilk satır durum kodu (2sn timeout).

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
- Yeni buton sağ paneldeki `StackPanel`'e `ActionButton` stiliyle eklenir. `Grid` içerik + `ⓘ` badge standardı izlenir.
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
| ✅ | SADP | `HariciAracBaslat` |
| ✅ | Cihaz Tara (Kamera+Router+PC+NVR…) | `KameraTaramaBaslat` + `KameraPanel` |
| ✅ | Ping Sweep (Cihaz Tara içinde) | 4. paralel görev, `PingYanit/PingMs` |
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

---

## 11. Açık TODO / Sonraki Adaylar (EKLENECEKLER.md'den)

| Öncelik | Özellik | Zorluk |
|---|---|---|
| 🔴 | HTTP Header Checker | Kolay |
| 🟡 | mDNS Keşif | Orta |
| 🟡 | NetBIOS Cihaz Adı | Kolay |
| 🟡 | Sonuç JSON/CSV Export | Kolay |
| 🟢 | RTSP Önizleme (ffmpeg) | Zor |
| 🟢 | Port Sürüm/Banner Tespiti | Orta |
| 🟢 | ARP Spoof Tespiti | Orta |
| 🟢 | WiFi Ağ Tarayıcı | Kolay |
| 🔵 | CVE Kontrolü | Zor |
| 🔵 | SSH Komutu Çalıştırıcı | Zor |
| 🔵 | Geçmiş / Son Taramalar | Orta |
| 🔵 | Çoklu Sekme / Çalışma Alanı | Zor |

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
