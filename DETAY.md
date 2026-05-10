# DETAY.md — Proje Tam Referans

> Bu dosya yeni Claude Code session'larında dosyaları taramadan tüm projeyi anlayabilmek için hazırlanmıştır.
> **Kaynak kodda her değişiklik yapıldığında bu dosya da güncellenmelidir.** (Bkz. CLAUDE.md)
> Son güncelleme: 2026-05-10 (SADP butonu eklendi)

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
    ├── MainWindow.xaml.cs        ← TÜM iş mantığı buradadır (~945 satır)
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
| Ping Testi | `BtnPing` | ActionButton | `BtnPing_Click` | ✅ Aktif (yan panel açar) |
| Port Tara | `BtnPortTara` | ActionButton | `BtnPortTara_Click` | ⏳ TODO |
| Cihazları Listele | `BtnCihazlar` | ActionButton | `BtnCihazlar_Click` | ✅ Aktif (Advanced IP Scanner başlatır) |
| SADP | `BtnSadp` | ActionButton | `BtnSadp_Click` | ✅ Aktif (`tools/sadp/sadptool.exe` başlatır) |
| Ekranı Temizle | `BtnTemizle` | ActionButton | `BtnTemizle_Click` | ✅ Aktif |

### 5.5 Ping Paneli (yan panel, animasyonla açılır)

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

**Ping paneli iç grid satırları (5 satır):**
| Row | Height | İçerik |
|---|---|---|
| 0 | Auto | Başlık + açıklama |
| 1 | Auto | IP giriş kutusu |
| 2 | Auto | Hızlı seçim chip'leri |
| 3 | `*`  | `PingResultBorder` (sonuç kutusu) |
| 4 | Auto | Kapat butonu |

---

## 6. MainWindow.xaml.cs — Tam İçerik Haritası

### 6.1 Using İfadeleri (1-14)

`System`, `System.Collections.Generic`, `System.Diagnostics`, `System.IO`, `System.Linq`, `System.Net.NetworkInformation`, `System.Text.RegularExpressions`, `System.Threading`, `System.Threading.Tasks`, `System.Windows`, `System.Windows.Controls`, `System.Windows.Input`, `System.Windows.Media`, `Microsoft.Win32`.

### 6.2 Alanlar (20-45)

| Alan | Tip | Amaç |
|---|---|---|
| `_taramaDevamEdiyor` | bool | Tarama durumu flag'i |
| `_taramaCts` | `CancellationTokenSource?` | Tarama iptali |
| `_tsharkProc` | `Process?` | Aktif tshark process |
| `_pingPanelAcik` | bool | Yan panel durumu |
| `_pingCts` | `CancellationTokenSource?` | Ping iptali |
| `PingPanelGenisligi` | const double = 340 | Yan panel hedef genişliği |
| `AppBase` | static readonly string | exe konumu (single-file için `Environment.ProcessPath`, fallback `BaseDirectory`) |
| `NpcapInstaller` | static readonly string | `{AppBase}\Req\npcap-1.88.exe` |
| `TsharkExe` | static readonly string | `{AppBase}\tools\WiresharkPortable64\App\Wireshark\tshark.exe` |
| `WiresharkPortableExe` | static readonly string | `{AppBase}\tools\WiresharkPortable64\WiresharkPortable64.exe` |
| `TestSuresiSn` | const int = 2 | Arayüz aktiflik testi süresi |
| `HedefMB` | const int = 16 | Yakalama dosya boyutu sınırı |
| `HedefKB` | const int = 16384 | tshark `-a filesize:` argümanı |
| `_paketSayisi` | int | Atomik paket sayacı |
| `_sonPcap` | string | Son üretilen pcap yolu |

### 6.3 Yaşam Döngüsü ve Npcap

- **`MainWindow()`** (47-52): InitializeComponent, ilk mesaj, `BaslangicAsync()` fire-and-forget.
- **`BaslangicAsync()`** (54-58): Npcap kontrol/kurulum + "Sistem hazır" mesajı.
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

- **`WiresharkIleAc(string pcap)`** (624-640): `WiresharkPortable64.exe "{pcap}"` ile başlatır. **NOT:** Şu an UI'dan çağrılmıyor (kod hazır, ama buton bağlantısı yok).

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
- **`PingIpBox_TextChanged`** (~812): Canlı doğrulama: `✓` (yeşil IPv4), `~` (sarı hostname), `✗` (kırmızı geçersiz).
- **`PingIpBox_KeyDown`** (~839): Enter → `PingBaslat`.
- **`PingHizliBtn_Click`** (~849): Chip butonun `Tag`'inden IP alıp `PingBaslat`.
- **`PingKutucugaYaz(string metin, string hex)`** (~860): `PingResultPanel`'e stillenmiş `TextBlock` satırı ekler ve scroll'u sona götürür. Ping sonuçları ana chat'e **yazılmaz**.
- **`PingBaslat(string hedef)`** (~875-935): `System.Net.NetworkInformation.Ping` ile 4× ping (timeout 2000ms, aralarda 700ms gecikme). Başlangıçta `PingResultPanel.Children.Clear()` + `PingResultBorder.Visibility = Visible`. Her sonuç `PingKutucugaYaz` ile renklendirilir (mavi=başarı, kırmızı=hata, yeşil=özet, gri=ayırıcı).

### 6.9 Diğer Buton Handler'ları

- **`BtnPortTara_Click`** (911-915): TODO — şu an yalnızca "yakında eklenecek" mesajı.
- **`BtnCihazlar_Click`** (917-938): `tools\Ip_Scanner\advanced_ip_scanner.exe` başlatır (UseShellExecute=true, WorkingDirectory ayarlanır).
- **`BtnTemizle_Click`** (940-944): `ChatPanel.Children.Clear()` + sistem mesajı.

---

## 7. Harici Bağımlılıklar (runtime)

| Dosya | Yer | Amaç | Otomatik mi? |
|---|---|---|---|
| `tshark.exe` | `tools\WiresharkPortable64\App\Wireshark\` | Paket yakalama | Manuel kurulmalı |
| `WiresharkPortable64.exe` | `tools\WiresharkPortable64\` | pcap görüntüleme | Manuel |
| `npcap-1.88.exe` | `Req\` | Sürücü installer | İlk açılışta otomatik UAC |
| `advanced_ip_scanner.exe` | `tools\Ip_Scanner\` | Cihaz listesi | Manuel |

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

1. ⏳ **Port Tara** — `TcpClient` ile async port taraması (`BtnPortTara_Click` boş)
2. ⏳ **Sonuçları Kaydet** — pcap kopyalama veya chat mesajlarını txt/csv olarak dışa aktarma
3. ⏳ **Wireshark "Aç" butonu** — `WiresharkIleAc` metodu hazır ama UI'dan çağrı yok (yakalama tamamlandığında karta buton ekleme)
4. ✅ Ping Testi — tamam (`PingBaslat`)
5. ✅ Cihazları Listele — harici Advanced IP Scanner ile çözüldü
6. ✅ Taramayı Başlat / Durdur — tshark wrapper olarak çalışıyor

---

## 10. Geliştirme Kuralları (CLAUDE.md'den)

- Tüm ağ işlemleri `async/await` + `CancellationToken` (UI thread'i bloke etme).
- Sonuçlar `MesajEkle("sonuc", ...)` ile chat'e basılır.
- Yeni buton sağ paneldeki StackPanel'e `ActionButton` stiliyle eklenir.
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`'da tanımlanır.
- .NET 10 / WPF — `LetterSpacing` gibi web CSS özellikleri yoktur.

---

## 11. Git Durumu (snapshot 2026-05-10)

- **Branch:** `ping_kismi`
- **Main:** `main`
- **Modifiye edilenler:** `MainWindow.xaml`, `MainWindow.xaml.cs`
- **Son commitler:**
  - `88c2598` Merge pull request #1 from Crakkadmr/cihazlari_listeleme_kismi
  - `162bf75` refactor: remove integrated IP scanner background process logic and launch external executable instead
  - `001ffcc` feat: implement local network device discovery using Advanced IP Scanner
  - `5627c60` AppBase: ProcessPath kullan — single-file exe ile uyumlu
  - `3ed07f8` İlk commit — Ağ Tarama Programı v0.1
