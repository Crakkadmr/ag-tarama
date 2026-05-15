# Services Katmanı Referansı

Lisans/güvenlik servisleri için: [docs/licensing.md](licensing.md)

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
