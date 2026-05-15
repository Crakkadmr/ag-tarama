# Services Katmanı Referansı

Lisans/güvenlik servisleri için: [docs/licensing.md](licensing.md)
NuGet bağımlılıkları: `QuestPDF 2024.12.*` (PDF), `ClosedXML 0.102.*` (XLSX)

---

## InterfaceDiscoveryService.cs (71 satır)

`tshark -D` çıktısını parse eder, aktif arayüzleri döner.

```csharp
Task<List<ArayuzBilgi>> TumunuGetirAsync()
Task<int> PaketSayisiAsync(ArayuzBilgi, CancellationToken)
```

---

## CaptureService.cs (80 satır)

tshark process yönetimi, progress callback.

```csharp
Task YakalaAsync(List<int> arayuzNolar, string pcapYolu,
    int hedefKB, Action<double,int,TimeSpan> onProgress, CancellationToken)
```

---

## PingService.cs (48 satır)

```csharp
IAsyncEnumerable<PingSonuc> PingleAsync(string hedef, CancellationToken)
// PingSonuc: Basarili, RoundtripMs, Ttl, Hata
```

4 ping, TTL, hata sarmalı akış.

---

## PortScanService.cs (73 satır)

```csharp
static int[] Parse(string aralik)          // "1-1024" veya "80,443,22"
Task TaraAsync(string ip, int[] portlar,
    Func<int,Task> acikCallback, CancellationToken)
// SemaphoreSlim(50), 1000ms timeout
```

---

## NetbiosService.cs (241 satır)

UDP 137 NetBIOS Node Status + reverse DNS + `ping -a` + `nbtstat -A`.

```csharp
Task<NetbiosSonuc> SorgulaAsync(string ip, CancellationToken)
// NetbiosSonuc: CihazAdi, GrupAdi, DnsAdi, PingAdi
```

---

## AdvancedIpScannerService.cs (137 satır)

`tools\Ip_Scanner\advanced_ip_scanner_console.exe /r:<subnet>.1-254 /f:<temp> /v2` çalıştırır, çıktıyı parse eder.

```csharp
Task<List<AisSonuc>> TaraAsync(string subnet, CancellationToken)
// AisSonuc: Ip, Ad, Mac, Uretici, Servisler
```

---

## HistoryService.cs (88 satır)

`%APPDATA%\AgTarama\history\*.json` altında geçmiş kayıtları.

```csharp
static void Kaydet(string tur, string hedef, string ozet,
    List<string> satirlar, Dictionary<string,string>? meta = null)
static List<HistoryRecord> YukleHepsi()
static void Sil(string id)
static void TumunuSil()
// HistoryRecord: Id, CreatedAt, Type, Target, Summary, Lines, Metadata
```

Kayıt üreten akışlar: Ping, Port Tara, ARP Tablosu, Cihaz Tara, Yakalama.

---

## SettingsService.cs (30 satır)

```csharp
static AppSettings Yukle()
static void Kaydet(AppSettings ayarlar)
// AppSettings: HedefMB (int), TestSuresiSn (int)
// Yol: %APPDATA%\AgTarama\settings.json
```

---

## FavoriService.cs (47 satır)

```csharp
static void Ekle(string ip)
static void Sil(string ip)
static List<string> YukleHepsi()
// Yol: %APPDATA%\AgTarama\favoriler.json
```

---

## UpdateService.cs (254 satır)

GitHub Releases API kontrolü, ZIP indirme, PowerShell self-update.

```csharp
Task<UpdateBilgi?> GuncellemeyiKontrolEtAsync()
Task IndirVeKurAsync(string indirmeUrl, IProgress<double> progress, CancellationToken)
// Deterministic ZIP seçimi: AgTarama-v*-win-x64.zip + .sha256 zorunlu
// Opsiyonel: AGT_UPDATE_SIGNER_THUMBPRINT env var ile thumbprint pinning
```

---

## SecurityService.cs (90 satır)

Debugger + analiz aracı tespiti. Release-only (DEBUG'da no-op).

```csharp
static void Dogrula()  // App_Startup'tan çağrılır; tespit edilirse uygulama kapanır
```

---

## AppSettings.cs

Model sınıfı:
```csharp
class AppSettings {
    int HedefMB      { get; set; } = 100;
    int TestSuresiSn { get; set; } = 2;
}
```

---

## WlanService.cs

`netsh wlan show networks mode=bssid` çıktısını parse eder.

```csharp
static Task<List<WlanSonuc>> ScanAsync(CancellationToken ct)
static bool WifiAdaptorVarMi()
// WlanSonuc: Ssid, Bssid, Auth, Encryption, Signal(%), Channel, RadioType, EvilTwin
```

- Evil-Twin tespiti: aynı SSID, birden fazla farklı BSSID → `EvilTwin = true`
- `WifiAdaptorVarMi()`: `netsh wlan show interfaces` çıktısında "Name" satırı arar

---

## BandwidthHistoryService.cs (yeni — #10)

In-memory dairesel buffer, bant genişliği zaman serisi.

```csharp
static void RecordTick(double totalRxBps, double totalTxBps)
static (double[] Rx, double[] Tx) GetAggregate(int seconds)
static (double PeakRx, double PeakTx, double AvgRx, double AvgTx,
        long TotalRxMB, long TotalTxMB) Stats(int seconds)
```

- Kapasite: 3600 örnek (1 saat), dairesel `_head` işaretçi ile
- `GetAggregate(sn)` → son `sn` saniyelik örnekleri kronolojik sırada döner

---

## CommandRouter.cs (yeni — #13)

F12 konsol için komut yönlendirici; tüm servisleri tek API yüzeyi üzerinden çağırır.

```csharp
static void Register(string name, Func<string[], CancellationToken, Task<string>> handler)
static Task<string> ExecuteAsync(string line, CancellationToken ct)
static void PushHistory(string cmd)
static List<string> GetHistory()
static List<string> GetCommandNames()
```

- `ExecuteAsync` `&&` zincirini `Regex.Split` ile destekler
- Kayıtlı komutlar: `help`, `clear`, `history`, `ping`, `dns`, `port`, `traceroute`, `arp`, `wol`, `scan`, `ssl`, `banner`, `web`, `smb`, `snmp`
- `snmp` komutu dahili ASN.1 DER kodlama/çözümleme kullanır (NuGet yok); OID takma adları: sysName, sysDescr, sysUpTime, sysLocation, sysContact
- `"\x00CLEAR"` dönüş değeri konsol çıktısını temizler
- Geçmiş: son 50 komut, yineleme önlemeli

---

## PdfReportService.cs (yeni — #12)

QuestPDF ile gerçek PDF raporu üretimi.

```csharp
static byte[] GenerateDeviceScanReport(IEnumerable<DeviceScanRow> rows, ReportMetadata meta)
// DeviceScanRow: Ip, Ad, Tur, Marka, Model, Ping, Portlar, Kesif, Mac, Uretici, Servis
// ReportMetadata: Operator, Project
```

- Statik constructor'da `QuestPDF.Settings.License = LicenseType.Community`
- A4 Yatay, kenar boşlukları 18px (yatay) / 14px (dikey)
- 11 sütunlu tablo, başlık arka planı `#0D3B66`, dönüşümlü satır renkleri `#0D1117`/`#101722`
- Risk alanları PDF modelinden çıkarılmıştır.
- Altbilgi: sayfa X/Y numarası
