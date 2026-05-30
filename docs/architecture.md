# Mimari ve Klasör Yapısı

## Klasör Yapısı

```
AG TARAMA PROGRAMI/                   ← repo kökü
├── AgTarama.slnx                     ← .NET 10 solution (slnx format) — AgTarama + AgTarama.Tests
├── .gitignore                        ← **/bin/, **/obj/, *.user, .vs/, captures/, TestResults/
├── master-refactor.md                ← refactor plan özeti
├── AgTarama/                         ← ana WPF projesi
│   ├── AgTarama.csproj               ← .NET 10 WPF v0.4.0; bkz. project.md
│   ├── App.xaml / App.xaml.cs        ← Application giriş noktası
│   ├── AssemblyInfo.cs               ← ThemeInfo
│   ├── MainWindow.xaml               ← Network Sniffer UI + stiller (~1986 satır)
│   ├── MainWindow.xaml.cs            ← Ana partial (~386 satır)
│   ├── Partials/                     ← 12 partial — bkz. partials.md
│   ├── Paths.cs                      ← exe-relative yol sabitleri (static)
│   ├── LogService.cs                 ← %APPDATA%\AgTarama\logs\YYYYMMDD.log
│   ├── obfuscar.xml                  ← Release post-build obfuscator config
│   ├── Services/                     ← Core servisler — bkz. services.md
│   │   ├── Ai/                       ← AI altyapısı — bkz. services-ai.md
│   │   └── Discovery/                ← Cihaz keşif motoru — bkz. services-discovery.md
│   ├── LicenseWindow.xaml / .cs      ← Lisans aktivasyon
│   ├── UpdateWindow.xaml / .cs       ← Güncelleme bildirimi + indirme
│   ├── SettingsWindow.xaml / .cs     ← Ayarlar penceresi
│   ├── AiDeviceReportWindow.xaml/.cs ← Cihaz AI rapor modalı
│   ├── docs/                         ← Doc dosyaları (bu klasör)
│   ├── Req/npcap-1.88.exe            ← Npcap installer + oui.csv
│   ├── tools/WiresharkPortable64/    ← tshark + Wireshark
│   ├── tools/Ip_Scanner/             ← advanced_ip_scanner
│   ├── tools/sadp/                   ← sadptool.exe
│   ├── tools/security/               ← hashes.allowlist.sha256 + verify script
│   └── supabase/                     ← RLS migration SQL'leri
└── AgTarama.Tests/                   ← xUnit test projesi (net10.0-windows)
    ├── AgTarama.Tests.csproj         ← xunit 2.9.2 + xunit.runner.visualstudio + coverlet
    ├── OuiVendorLookupTests.cs
    ├── MacUtilsTests.cs
    ├── DeviceStoreTests.cs
    └── ProbeTests.cs
```

**Log:** `%APPDATA%\AgTarama\logs\YYYYMMDD.log`
**Ayarlar:** `%APPDATA%\AgTarama\settings.json`
**Favoriler:** `%APPDATA%\AgTarama\favorites.json` (v0.3.0: IP normalize edilerek saklanır)
**Geçmiş:** `%APPDATA%\AgTarama\history\*.json` (v0.3.0: ID `{tarih}_{guid8}_{type}`; `SonKayitlariYukle` lazy load)

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

> **NuGet (ana proje):** `QuestPDF 2024.12.*` (PDF raporu) + `ClosedXML 0.102.*` (XLSX). `Lextm.SharpSnmpLib` kaldırıldı — SNMPv1 artık `SnmpFingerprintService` + `CommandRouter` içinde manuel ASN.1 DER ile yapılıyor (NuGet bağımlılığı yok).
> **NuGet (AgTarama.Tests):** `xunit 2.9.2` + `xunit.runner.visualstudio 2.8.2` + `coverlet.collector 6.0.2`.

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

---

## AI Faz 2 (2026-05-17)

- Yeni partial: `Partials/MainWindow.Ai.cs`.
- Yeni servis klasörü: `Services/Ai/`.
- Chatbot alt satırında AI input barı bulunur; istekler `AiClient` ile OpenRouter'a gider.
- Varsayılan model: `deepseek/deepseek-v4-flash` (`AppSettings.AiModel` üzerinden değiştirilebilir).
- AI ayar UI'si `SettingsWindow > AI` bölümünde sunulur.
