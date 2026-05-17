# AGENTS.md — Proje Master Index

> Bu dosya AI agent'larinin projeye hizli giris noktasidir.
> Detayli referans bilgi `docs/` klasorunde konuya gore ayrilmistir.
> Son guncelleme: 2026-05-17 (v0.3.0 — Guvenlik sertlestirmesi, gercek CIDR /16-/30 destegi, concurrency duzeltmeleri, koyu tema chip + checkbox)

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
| Surum | v0.3.0 |
| Branch | `guvenlik-guncellestirmeleri-zirtpirt` (main: `main`) |
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

## 7. Son Degisiklik Notu (2026-05-17) — v0.3.0

**Guvenlik, dogruluk ve UI temasi sertlestirmesi:**

**P0 — Kritik:**
- **CIDR `/16-/30` gercekten taraniyor:** `TaramaSubneti` artik `HostStart`, `HostEnd`, `HostCount`, `OriginalCidr` aliyor. `SubnetGirdisiniCoz` /16-/23 maskeleri icin birden cok /24'e acilim yapar; /25-/30 icin sinirli host araligi hesaplar. Tum sweep'ler (`Enumerable.Range(1, 254)` yerine `Enumerable.Range(hostStart, sayi)`). `toplamHost = subnetler.Sum(s => s.HostCount)`.
- **`UpdateService.SafeExtractZip`:** Yeni `SafeExtractZip` ile Zip Slip / path traversal koruması, entry sayısı (max 5000), toplam boyut (500 MB), tek entry boyutu (200 MB), `..`/mutlak yol reddi.

**P1 — Concurrency / kaynak güvenliği:**
- `BandwidthHistoryService`: tüm static buffer erişimi `lock (_sync)` altında.
- `_wlanBilinenBssid`: `Dictionary<>` → `ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>`.
- `InterfaceDiscoveryService`: `Process.Start(psi)!` kaldırıldı; tshark yoksa açık `InvalidOperationException`.
- `MndpDiscoveryService` / `UbiquitiDiscoveryService`: TLV uzunlukları `& 0xFF` ile unsigned okunuyor, taşmaya dayanıklı sınır kontrolü.
- `CancellationTokenSource` disposal pattern: `_pingCts`, `_portScanCts`, `_traceCts`, `_konsoleCts`, `UpdateWindow._cts` yeniden atanmadan önce `Dispose()` ediliyor.
- `PingService`: `catch when (ex.GetBaseException() is not OperationCanceledException)` — AggregateException içine sarılı iptal artık yanlışlıkla loglanmıyor.

**P2 — Veri ve UX:**
- `HistoryService`:
  - `Id` formatı: `yyyyMMdd_HHmmss_fff_{guid8}_{type}` (ms collision imkansız).
  - `SonKayitlariYukle`: lazy load — önce `LastWriteTimeUtc`'ye göre sıralı listeden `Take(limit)` sonra deserialize.
- `FavoriService`: `IPAddress.TryParse` ile normalize + `OrdinalIgnoreCase` karşılaştırma (`192.168.001.1` = `192.168.1.1`).
- `MainWindow.History.NormalizeTip`: `İ→I, Ş→S, Ğ→G, Ü→U, Ö→O, Ç→C` — Türkçe locale `ToUpper` sorunu giderildi.
- `AppSettings.EvilTwinSinyalEsigi` (varsayılan 75, 50-90): Evil Twin "yüksek sinyal" eşiği artık ayarlanabilir.
- `SecurityService.Dogrula`: `#if DEBUG ... #else ... #endif` ile CS0162 uyarısı temizlendi.

**UI — Koyu tema tutarlılığı:**
- `DarkCheckBox` stili (`MainWindow.xaml`): 16×16 koyu kutucuk + mavi `Path` onay işareti + hover/checked/disabled trigger'ları. `<Style TargetType="CheckBox" BasedOn="{StaticResource DarkCheckBox}"/>` ile tüm CheckBox'lar otomatik koyu tema (Derin tara, Otomatik yenile vb.).
- `DarkChip` stili: `prim:ToggleButton` için yuvarlatılmış chip (CornerRadius=12), seçili durumda mavi vurgu, hover mavi kenar. `KameraChipOlustur` artık `Style = (Style)FindResource("DarkChip")` kullanıyor; inline renkler kaldırıldı.
- `prim:` namespace prefix'i Window root'una eklendi.

---

## 8. AI Faz 2 Notu (2026-05-17)

- Chatbot sekmesinde alt AI input barı aktif (`AiInputBox`, `AiGonderBtn`, `AiTemizleBtn`).
- AI sohbet akışı `Partials/MainWindow.Ai.cs` üzerinden çalışır.
- AI servisleri `Services/Ai/*` altında konumlanır.
- Model: OpenRouter üzerinden `minimax/minimax-m2.5`.
- Ayarlar penceresinde AI bölümü yoktur (kullanıcı isteğiyle devre dışı).
