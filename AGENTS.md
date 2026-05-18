# AGENTS.md — Proje Master Index

> Bu dosya AI agent'larinin projeye hizli giris noktasidir.
> Detayli referans bilgi `docs/` klasorunde konuya gore ayrilmistir.
> Son guncelleme: 2026-05-18 (v0.4.0 — AI Faz 1-4: sohbet, pcap analizi, cihaz analizi + AiDeviceReportWindow; model: deepseek/deepseek-v4-flash)

---

## 1. Proje Kimligi

| Alan | Deger |
|---|---|
| Ad | Network Sniffer (AgTarama) |
| Tip | WPF Desktop Uygulamasi |
| Hedef | .NET 10 (`net10.0-windows`), `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable` |
| csproj ek | `tools\**\*` ve `Req\**\*` -> `CopyToOutputDirectory=PreserveNewest` |
| Output | `WinExe` |
| Namespace | `AgTarama` |
| Surum | v0.4.0 |
| Branch | `bugveyeniozellikler` (main: `main`) |
| Git user | Crakkadmr |
| Kok yol | `C:\Projects\AG TARAMA PROGRAMI\AgTarama` |

---

## 2. Proje Amaci

WPF tabanli **Network Sniffer** markali chatbot arayuzlu ag tarama ve paket yakalama uygulamasi.

Ana ozellikler:
- Paket yakalama (tshark) ve Wireshark Portable ile analiz
- Ping, Port Tara (banner), Traceroute, DNS, ARP, Wake-on-LAN
- Bant genisligi monitoru (gecmis grafik + istatistik)
- Cihaz Tara (ONVIF+WSD, SSDP, mDNS/25-servis, Ping Sweep, NetBIOS, Advanced IP Scanner, Ubiquiti Discovery UDP-10001, MikroTik MNDP UDP-5678, SNMP sysDescr UDP-161, HTTP Fingerprint vendor-specific endpoint'ler; subnet chip picker; confidence score; QuestPDF/ClosedXML export)
- Wi-Fi Tarama (SSID/BSSID/sinyal/kanal, Evil-Twin tespiti)
- F12 komut konsolu (CommandRouter, `&&` zincirleme)
- Favori IP, gecmis, lisanslama (Supabase)
- **AI Modu** (OpenRouter / Google / OpenAI / Custom; deepseek/deepseek-v4-flash varsayilan): serbest sohbet, pcap analizi, cihaz analizi + `AiDeviceReportWindow` modal

---

## 3. Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build                   # Debug build
dotnet run                     # Calistir
dotnet build -c Release        # Release build
```

---

## 4. Dokumantasyon Haritasi

| Ne yapiyorsun? | Oku |
|---|---|
| XAML, stil, renk, buton, sekme degisikligi | [docs/ui.md](docs/ui.md) |
| Service ekle veya degistir | [docs/services.md](docs/services.md) |
| MainWindow partial icinde metot bul / degistir | [docs/partials.md](docs/partials.md) |
| Lisans, Supabase, guvenlik, guncelleme | [docs/licensing.md](docs/licensing.md) |
| Mimari, klasor yapisi, harici bagimliliklar, kurallar | [docs/architecture.md](docs/architecture.md) |

---

## 5. GitHub Release Prosedürü

Kullanici **"github release yap"** (veya benzeri: "release et", "yayınla") dediginde asagidaki adimlari **sirayla ve eksiksiz** uy­gu­la:

### 5.1 Versiyon Arttirma

1. `AgTarama.csproj` dosyasindaki `<Version>`, `<AssemblyVersion>`, `<FileVersion>` degerlerini **minor basamagi 0.1 arttirarak** guncelle.
   - Ornek: `0.2.0` → `0.3.0`
2. `AGENTS.md` §1 tablosundaki `Surum` satirini ve §7 basligini guncelle.
3. Degisikligi commit et:
   ```
   chore: bump version to vX.Y.Z
   ```

### 5.2 Release Build

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build -c Release
```

Build basarili olmazsa duraksayip kullaniciya hata mesajini ilet; devam etme.

### 5.3 ZIP Olustur

```powershell
$ver = "X.Y.Z"   # yeni versiyon
$src = "bin\Release\net10.0-windows"
$zip = "bin\AgTarama-v$ver.zip"
Compress-Archive -Path "$src\*" -DestinationPath $zip -CompressionLevel Optimal
```

### 5.4 SHA256 Dosyasi Olustur

```powershell
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$shaFile = "bin\AgTarama-v$ver.zip.sha256"
[System.IO.File]::WriteAllText($shaFile, "$hash  AgTarama-v$ver.zip", [System.Text.UTF8Encoding]::new($false))
```

> **Zorunlu:** UpdateService, SHA dosyasi olmayan release'lerde guncelleme bulamaz (`return null`).

### 5.5 GitHub Release Olustur

```bash
gh release create "vX.Y.Z" "bin/AgTarama-vX.Y.Z.zip" "bin/AgTarama-vX.Y.Z.zip.sha256" \
  --repo Crakkadmr/ag-tarama \
  --title "vX.Y.Z — <kisa ozet>" \
  --notes "<release notlari>" \
  --latest
```

- `--latest` mutlaka ekle (UpdateService `/releases/latest` endpoint'ini kullanir).
- Release notlarina en az "Kurulum" adimi ekle.

### 5.6 Dogrulama

```bash
gh release view vX.Y.Z --repo Crakkadmr/ag-tarama --json assets --jq '.assets[].name'
```

Ciktida hem `.zip` hem `.zip.sha256` gozukmeliydi. Ikisi de varsa islemi kullaniciya bildir.

---

## 6. Dokumantasyon Guncelleme Politikasi

**Markdown dosyalari (AGENTS.md, docs/*.md) her degisiklikte otomatik guncellenmez.**
Sadece kullanici acikca `"md guncelle"` veya `"AGENTS.md'yi guncelle"` dediginde guncellenir.
Kod degisiklikleri yapilirken bu dosyalara dokunulmaz.

Kullanici `"md guncelle"` dediginde:
- Hangi alt dosyalarin guncel olmadigini analiz et
- Sadece etkilenen dosyalari duzenle

---

## 6. Gelistirme Kurallari (ozet)

- `async/await` + `CancellationToken` zorunlu; UI thread bloke edilmemeli.
- Panel sonuclari kendi `XxxResultPanel`'e; ana chat'e yazilmaz.
- Sekme gecisi: `MainTabControl.SelectedIndex = TabXxx`.
- Stil kaynaklari yalnizca `MainWindow.xaml > Window.Resources`.
- Harici arac baslatma: `HariciAracBaslat(exe, ad)`.
- Toast: `ToastGoster(mesaj, hata:bool)`.

---

## 7. Son Degisiklik Notu (2026-05-18) — v0.4.0

**Bug düzeltmeleri (bugtest.md kapsamı):**

- **P0 korundu:** `AiDefaultKey` XOR-obfuscated key yerinde; vault yoksa otomatik yükleniyor.
- **HTTPS zorunluluğu:** `SettingsWindow` AI base URL `Uri.TryCreate` + `https` scheme zorunlu; HTTP girişi reddediliyor.
- **Update imza log:** `AGT_UPDATE_SIGNER_THUMBPRINT` set edilmemişse `LogService.Kaydet` uyarısı yazılıyor.
- **AiUsageMeter thread safety:** `_lock` nesnesi; `Load()` ve `AddUsage()` lock altında, race condition giderildi.
- **AI iptal semantiği:** `AiClient` catch bloğu `OperationCanceledException` propagate ediyor (artık hata mesajına çevrilmiyor).
- **tshark process cleanup:** `AiPcapAnalyzer.RunTsharkStatAsync` → finally + `Kill(entireProcessTree)` + stdout/stderr paralel drain.
- **Wi-Fi UI thread fix:** `WlanService.WifiAdaptorVarMiAsync()` (async); `WlanPanelBaslat()` sync check kaldırıldı; `BaslangicAsync()` → `WlanAdaptorKontrolAsync()` ile açılışta donma önlendi.
- **Cihaz AI modal CTS:** `AiDeviceReportWindow` `_cts` alanı + `Closed` handler; pencere kapanınca istek iptal ediliyor.
- **F12 AI önerisi iptal:** `_aiOneriCts`; Ctrl+Tab önceki isteği iptal edip yenisini başlatıyor; Esc AI önerisini de iptal ediyor.
- **CIDR /31-/32:** Parser sınırı `> 30` → `> 32`; tek host ve point-to-point subnet taranabiliyor.
- **User-Agent:** `AgTarama-AI/0.3.0` → `0.4.0`.

---

**AI Modu tam entegrasyonu (Faz 1-4):**

### Faz 1 — Altyapı
- `Services/Ai/`: `AiKeyVault` (DPAPI+AES machine-bound), `AiClient` (OpenAI-uyumlu, retry, rate-limit, key masking), `AiProvider` (OpenRouter/Google/OpenAI/Custom preset'ler), `AiUsageMeter` (günlük/aylık token sayacı), `AiPrompts`, `AiDefaultKey` (XOR-obfuscated).
- `AppSettings` yeni alanlar: `AiEnabled`, `AiSaglayici`, `AiBaseUrl`, `AiModel` (default: `deepseek/deepseek-v4-flash`), `AiGunlukTokenLimiti` (200K), `AiAylikTokenLimiti` (5M), `AiYerelIpMaskele`.
- Ayarlar > AI bölümü: sağlayıcı dropdown, API anahtarı PasswordBox (vault), Test Et butonu, token limitleri, IP maskele checkbox.

### Faz 2 — Serbest sohbet (Chatbot sekmesi)
- Chatbot sekmesi **DockPanel** düzeniyle: araç çubuğu üstte, AI input barı altta (`AiInputBorder` + `AiInputBox` + `AiGonderBtn` + `AiTemizleBtn`), `ChatScrollViewer` ortada.
- `AiInputBox`: `AiInputStyle` (minimal template — sadece PART_ContentHost ScrollViewer; görsel çerçeve `AiInputBorder`'dan gelir; GotFocus/LostFocus ile border rengi code-behind'dan değişir).
- `Partials/MainWindow.Ai.cs`: `_aiSohbetGecmisi`, `AiSoruGonderAsync`, bekleme satırı, `_aiSohbetCts`.
- `Dispatcher.InvokeAsync(..., DispatcherPriority.Loaded)` ile layout sonrası scroll.
- **Kritik layout notu:** Kök pencere Grid'inin Row 2 mutlaka `Height="*"` olmalı.

### Faz 3 — Pcap AI Analizi
- `Services/Ai/AiPcapAnalyzer.cs`: tshark 6 istatistik komutu (conv/io/phs/endpoints/http/dns), 50 satır kırpma, özel IP maskeleme (3. oktet → x), `AiClient.AskAsync`.
- `AiPrompts.PcapSystemPrompt`: tshark istatistiklerini yorumlayan TR sistem promptu.
- `Partials/MainWindow.Capture.cs`: yakalama tamamlama kartına "✨ AI ile analiz et" butonu → ChatPanel + HistoryService.

### Faz 4 — Cihaz Tara AI Analizi
- `Services/Ai/AiDeviceAnalyzer.cs`: `CihazDto` record, 5 hazır preset (güvenlik riski, kamera listesi, AP/router grubu, bilinmeyen sorgu, sonraki tarama), max 50 cihaz JSON.
- `AiPrompts.CihazSystemPrompt`: cihaz listesi analiz promptu (KRITIK/ORTA/DUSUK sınıflandırma).
- `AiDeviceReportWindow.xaml/.cs`: koyu temalı modal pencere; preset chip'ler + serbest metin girişi; [Kopyala] [TXT Kaydet] [Yeniden Sor] [Kapat]; AI yanıtında IP tespit edilirse "Bu IP'leri yeniden tara" butonu.
- `Partials/MainWindow.DeviceScan.cs`: Cihaz Tara satırına `KameraAiBtn` ("✨ AI") eklendi; tarama bitmeden disabled.

### UI düzeltmeleri (v0.3.0 → v0.4.0 arası)
- Chatbot sekmesi yeniden yapılandırıldı: Grid → DockPanel LastChildFill.
- `AiInputStyle` minimal TextBox stili (`OverridesDefaultStyle` + şeffaf background + PART_ContentHost only); metin görünürlük sorunu çözüldü.
- Ayarlar: AI ComboBox `DarkComboBox` + CheckBox `DarkCheckBox` temasıyla uyumlu.

---

## 8. v0.3.0 Gecmis Notu (2026-05-17)

**Guvenlik, dogruluk ve UI temasi sertlestirmesi:**

- **CIDR `/16-/30`** gercekten taraniyor. `SubnetGirdisiniCoz` /16-/23 → birden cok /24; /25-/30 → sinirli host araligi.
- **`UpdateService.SafeExtractZip`**: Zip Slip / path traversal korumasi, entry/boyut sinirlari.
- **BandwidthHistoryService** `lock (_sync)`, **`_wlanBilinenBssid`** ConcurrentDictionary, CTS disposal pattern, PingService AggregateException filtresi.
- **HistoryService** ms-hassas Id, lazy load. **FavoriService** IP normalizasyonu. Turkce locale ToUpper duzeltmesi. EvilTwinSinyalEsigi ayarlanabilir.
- **DarkCheckBox** + **DarkChip** stillleri; `prim:` namespace prefix.
