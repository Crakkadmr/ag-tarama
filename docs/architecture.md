# Mimari ve Klasör Yapısı

## Klasör Yapısı

```
AG TARAMA PROGRAMI/
└── AgTarama/
    ├── AgTarama.csproj               ← .NET 10 WPF v0.2.0; NuGet: QuestPDF 2024.12.*, ClosedXML 0.102.*
    ├── App.xaml / App.xaml.cs        ← Application giriş noktası (boş)
    ├── AssemblyInfo.cs               ← ThemeInfo
    ├── MainWindow.xaml               ← Network Sniffer UI tasarımı + stiller (~1254 satır)
    ├── MainWindow.xaml.cs            ← Ana partial: alanlar, başlangıç, Npcap, ArayuzSecim, UygulaButonSablon (~354 satır)
    ├── Partials/                     ← C# partial class dosyaları (hepsi `public partial class MainWindow`)
    │   ├── MainWindow.Capture.cs     ← YakalamaBaslat/Durdur, YakalamaKartiOlustur, WiresharkIleAc (351 satır)
    │   ├── MainWindow.NetworkTools.cs← MesajEkle, Ping, PortTara, Traceroute, DNS, WoL, ARP, AgBilgi (835 satır)
    │   ├── MainWindow.Bandwidth.cs   ← Bant grafiği, BandwidthHistoryService entegrasyonu, per-app trafik (347 satır)
    │   ├── MainWindow.Console.cs     ← F12 komut konsolu, KonsoleToggle, ConsoleInput_KeyDown (yeni)
    │   ├── MainWindow.Favorites.cs   ← FavoriChipleriniYenile, FavorilerPanelGuncelle (133 satır)
    │   ├── MainWindow.History.cs     ← GecmisPanelGuncelle, Tekrar Çalıştır, Karşılaştır (235 satır)
    │   ├── MainWindow.UI.cs          ← BtnAyarlar, RaporKaydet, Toast, Bildirim (141 satır)
    │   ├── MainWindow.License.cs     ← LisansPanelGuncelle, sticky banner, MachineId, NTP, Kopyala (204 satır)
    │   ├── MainWindow.Wlan.cs        ← WlanPanelBaşlat, WlanTaramaBaslat, WlanSatir, Evil-Twin tespiti (~180 satır)
    │   └── MainWindow.DeviceScan.cs  ← KameraTaramaBaslat, 7 paralel keşif protokolü, subnet chip picker, export (2245 satır)
    ├── Paths.cs                      ← Tüm exe-relative yol sabitleri (static)
    ├── LogService.cs                 ← %APPDATA%\AgTarama\logs\YYYYMMDD.log
    ├── obfuscar.xml                  ← Obfuscar yapılandırması (Release post-build)
    ├── Services/                     ← Bkz. docs/services.md ve docs/licensing.md
    ├── LicenseWindow.xaml / .cs      ← Lisans aktivasyon ekranı
    ├── UpdateWindow.xaml / .cs       ← Güncelleme bildirimi + indirme
    ├── SettingsWindow.xaml / .cs     ← Ayarlar penceresi
    ├── docs/                         ← Agent referans dosyaları (bu dosya)
    ├── Req/npcap-1.88.exe            ← Npcap installer
    ├── tools/WiresharkPortable64/    ← tshark + Wireshark
    ├── tools/Ip_Scanner/             ← advanced_ip_scanner + mac_interval_tree.txt
    ├── tools/sadp/                   ← sadptool.exe
    └── captures/                     ← .pcap dosyaları (otomatik oluşur)
```

**Log:** `%APPDATA%\AgTarama\logs\YYYYMMDD.log`
**Ayarlar:** `%APPDATA%\AgTarama\settings.json`
**Favoriler:** `%APPDATA%\AgTarama\favoriler.json`
**Geçmiş:** `%APPDATA%\AgTarama\history\*.json`

---

## Mimari

**Tek pencere — MVVM yok.** UI wiring `MainWindow.xaml` + `MainWindow.xaml.cs` (+ `Partials/`) çiftinde.
`MainWindow.xaml.cs` 10 partial dosyaya bölünmüştür; derleyici bunları tek sınıfta birleştirir.
Ağ iş mantığı `Services/` katmanına ayrılmış. ViewModel veya DI container yok.

**Mimari katmanlar:**
- `Paths.cs` — tüm exe-relative yol sabitleri tek yerde
- `LogService.cs` — `%APPDATA%\AgTarama\logs\YYYYMMDD.log`'a yazar; `OturumBaslat`, `Kaydet`, `Hata`
- `MainWindow` → `HataBildir(mesaj, ex?)` — chat kırmızı mesaj + `LogService.Hata` tek noktadan
- `Services/` katmanı — ağ ve lisans iş mantığı (detay: `docs/services.md`, `docs/licensing.md`)

---

## Harici Bağımlılıklar (runtime)

| Dosya | Yer | Amaç | Otomatik mi? |
|---|---|---|---|
| `tshark.exe` | `tools\WiresharkPortable64\App\Wireshark\` | Paket yakalama | Manuel |
| `WiresharkPortable64.exe` | `tools\WiresharkPortable64\` | pcap görüntüleme | Manuel |
| `npcap-1.88.exe` | `Req\` | Sürücü installer | İlk açılışta otomatik UAC |
| `advanced_ip_scanner.exe` | `tools\Ip_Scanner\` | Cihaz listesi | Manuel |
| `advanced_ip_scanner_console.exe` | `tools\Ip_Scanner\` | Cihaz Tara zenginleştirme | Cihaz Tara içinde otomatik, timeout'lu |
| `mac_interval_tree.txt` | `tools\Ip_Scanner\` | MAC prefix → üretici veritabanı | Cihaz Tara içinde otomatik |
| `sadptool.exe` | `tools\sadp\` | Hikvision SADP | Manuel |

> **NuGet:** `QuestPDF 2024.12.*` (PDF raporu) + `ClosedXML 0.102.*` (XLSX). `Lextm.SharpSnmpLib` kaldırıldı — SNMPv1 artık `SnmpFingerprintService` + `CommandRouter` içinde manuel ASN.1 DER ile yapılıyor (NuGet bağımlılığı yok).

---

## Mesaj Türleri

```csharp
MesajEkle("sistem",    "...")  // gri, ◆ prefix
MesajEkle("kullanici", "...")  // sağda, › prefix
MesajEkle("sonuc",     "...")  // mavi, başarı/sonuç
MesajEkle("hata",      "...")  // kırmızı, ✖ prefix
```

---

## Geliştirme Kuralları

- Tüm ağ işlemleri `async/await` + `CancellationToken`; UI thread bloke edilmemeli.
- Sonuçlar `MesajEkle("sonuc", ...)` ile chat'e; panel sonuçları kendi `XxxResultPanel`'e — ana chat'e **yazılmaz**.
- Yeni araç butonu başlık kartındaki `WrapPanel`'e eklenir. Sekme yönlendirme: `MainTabControl.SelectedIndex = TabXxx`.
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`'da. `ActiveActionButton`, `ActionButton`'dan **SONRA** tanımlanmalı.
- .NET 10 / WPF — `LetterSpacing` gibi web CSS özellikleri yoktur.
- Harici araç başlatma: `HariciAracBaslat(exe, ad)`. Toast: `ToastGoster(mesaj, hata:bool)`.
