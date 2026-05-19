# Core Services (`Services/`)

> AI servisleri: [services-ai.md](services-ai.md)
> Cihaz keşif alt sistemi: [services-discovery.md](services-discovery.md)
> Lisans + güvenlik + update: [licensing.md](licensing.md)
> NuGet: `QuestPDF 2024.12.*` (PDF), `ClosedXML 0.102.*` (XLSX), `SharpPcap 6.3.0` + `PacketDotNet 1.4.7` (pasif sniff)

## InterfaceDiscoveryService.cs

`tshark -D` çıktısını parse eder, aktif arayüzleri döner.

```csharp
Task<List<ArayuzBilgi>> TumunuGetirAsync()
Task<int> PaketSayisiAsync(ArayuzBilgi, CancellationToken)
```

tshark yoksa `TumunuGetirAsync` → `InvalidOperationException("tshark başlatılamadı: ...")`. `PaketSayisiAsync` null process → 0.

## CaptureService.cs

tshark process yönetimi, progress callback.

```csharp
Task YakalaAsync(List<int> arayuzNolar, string pcapYolu,
    int hedefKB, Action<double,int,TimeSpan> onProgress, CancellationToken)
```

## PingService.cs

```csharp
IAsyncEnumerable<PingSonuc> PingleAsync(string hedef, CancellationToken)
// PingSonuc: Basarili, RoundtripMs, Ttl, Hata
```

4 ping, TTL, hata sarmalı. Exception filter `catch (Exception ex) when (ex.GetBaseException() is not OperationCanceledException)` — `AggregateException` içine sarılı iptal yanlışlıkla loglanmıyor.

## PortScanService.cs

```csharp
static int[] Parse(string aralik)            // "1-1024" veya "80,443,22"
Task TaraAsync(string ip, int[] portlar,
    Func<int,Task> acikCallback, CancellationToken)
// SemaphoreSlim(50), 1000ms timeout
```

## NetbiosService.cs

UDP 137 Node Status + reverse DNS + `ping -a` + `nbtstat -A`.

```csharp
Task<NetbiosSonuc> SorgulaAsync(string ip, CancellationToken)
// NetbiosSonuc: CihazAdi, GrupAdi, DnsAdi, PingAdi
```

**Not:** `nbtstat`/`ping` çıktısı için `ProcessStartInfo.StandardOutputEncoding = Encoding.GetEncoding(OEMCodePage)` zorunlu (Türkçe karakter).

## HistoryService.cs

`%APPDATA%\AgTarama\history\*.json` altında geçmiş.

```csharp
static void Kaydet(string tur, string hedef, string ozet,
    List<string> satirlar, Dictionary<string,string>? meta = null)
static List<HistoryRecord> YukleHepsi()
static List<HistoryRecord> SonKayitlariYukle(int limit)
static void Sil(string id)
static void TumunuSil()
// HistoryRecord: Id, CreatedAt, Type, Target, Summary, Lines, Metadata
```

- `Id` formatı: `yyyyMMdd_HHmmss_fff_{guid8}_{type}` — ms collision yok.
- `SonKayitlariYukle(limit)` dosyaları `LastWriteTimeUtc`'ye göre sıralayıp sadece ilk `limit` deserialize eder.

Kayıt üreten akışlar: Ping, Port Tara, ARP Tablosu, Cihaz Tara, Yakalama, AI Analiz.

## SettingsService.cs

```csharp
static AppSettings Yukle()
static void Kaydet(AppSettings ayarlar)
// Yol: %APPDATA%\AgTarama\settings.json
```

## AppSettings.cs (v0.4.0)

```csharp
class AppSettings {
    // Genel
    int  HedefMB                = 16;
    int  TestSuresiSn           = 2;
    int  PingTimeoutMs          = 2000;
    int  PortTaramaConcurrency  = 50;
    int  PortTaramaTimeoutMs    = 1000;
    int  WlanAutoRefreshSeconds = 10;
    int  EvilTwinSinyalEsigi    = 75;     // 50-90 clamp
    bool SesAcik                = true;
    bool ToastAcik              = true;

    // AI — detay services-ai.md
    bool   AiEnabled            = true;
    string AiSaglayici          = "OpenRouter";
    string AiBaseUrl            = "https://openrouter.ai/api/v1";
    string AiModel              = "deepseek/deepseek-v4-flash";
    int    AiGunlukTokenLimiti  = 200_000;
    int    AiAylikTokenLimiti   = 5_000_000;
    bool   AiYerelIpMaskele     = false;
    // API anahtarı ASLA AppSettings'e yazılmaz — sadece AiKeyVault.
}
```

## FavoriService.cs

```csharp
static bool Ekle(string ip)         // false = zaten var
static void Sil(string ip)
static List<string> YukleHepsi()
// Yol: %APPDATA%\AgTarama\favorites.json
```

IP normalize edilerek saklanır (`IPAddress.TryParse` → kanonik form). Karşılaştırma `OrdinalIgnoreCase`.

## UpdateService.cs

GitHub Releases API + ZIP indirme + PowerShell self-update.

```csharp
Task<UpdateBilgi?> GuncellemeyiKontrolEtAsync()
Task IndirVeKurAsync(string indirmeUrl, IProgress<double> progress, CancellationToken)
```

- Deterministic ZIP seçimi: `AgTarama-v*-win-x64.zip` + `.sha256` zorunlu.
- **`SafeExtractZip`:** entry ≤ 5000, toplam ≤ 500 MB, tek entry ≤ 200 MB. Mutlak yol/sürücü harfi/`..` reddedilir. Canonical hedef path `extractTo` altında doğrulanır (Zip Slip).
- `AGT_UPDATE_SIGNER_THUMBPRINT` env var set edilmemişse imza doğrulaması atlanır + log uyarısı.

Detay: [licensing.md](licensing.md).

## SecurityService.cs

Debugger + analiz aracı tespiti. Release-only (`#if DEBUG ... #else ... #endif`).

```csharp
static void Dogrula()  // App_Startup'tan; tespit edilirse uygulama kapanır
```

## CryptoHelper.cs

AES-CBC + HMAC yardımcıları (ortak).

## CommandRouter.cs

F12 konsol komut yönlendiricisi.

```csharp
static void Register(string name, Func<string[], CancellationToken, Task<string>> handler)
static Task<string> ExecuteAsync(string line, CancellationToken ct)
static void PushHistory(string cmd)
static List<string> GetHistory()
static List<string> GetCommandNames()
```

- `ExecuteAsync` `&&` zincirini `Regex.Split` ile destekler.
- Kayıtlı: `help`, `clear`, `history`, `ping`, `dns`, `port`, `traceroute`, `arp`, `wol`, `scan`, `ssl`, `banner`, `web`, `smb`, `snmp`.
- `snmp` — dahili ASN.1 DER kodlama (NuGet yok). OID alias: `sysName`, `sysDescr`, `sysUpTime`, `sysLocation`, `sysContact`.
- `"\x00CLEAR"` → console temizler.
- Geçmiş: son 50.

## BandwidthHistoryService.cs

In-memory dairesel buffer, bant genişliği zaman serisi.

```csharp
static void RecordTick(double totalRxBps, double totalTxBps)
static (double[] Rx, double[] Tx) GetAggregate(int seconds)
static (double PeakRx, double PeakTx, double AvgRx, double AvgTx,
        long TotalRxMB, long TotalTxMB) Stats(int seconds)
```

- Kapasite: 3600 örnek (1 saat), dairesel `_head`.
- `_rxBuf`, `_txBuf`, `_head`, `_count` — `lock (_sync)` altında (data race yok).

## WlanService.cs

`netsh wlan show networks mode=bssid` çıktısını parse eder.

```csharp
static Task<List<WlanSonuc>> ScanAsync(CancellationToken)
static Task<bool> WifiAdaptorVarMiAsync()
// WlanSonuc: Ssid, Bssid, Auth, Encryption, Signal(%), Channel, RadioType, EvilTwin
```

- Evil-Twin: aynı SSID, birden fazla BSSID → `EvilTwin = true`.
- `WifiAdaptorVarMiAsync()` async `WaitForExitAsync` — UI thread bloke etmez.

## AdvancedIpScannerService.cs

`tools\Ip_Scanner\advanced_ip_scanner_console.exe /r:<subnet>.1-254 /f:<temp> /v2` çalıştırır.

```csharp
Task<List<AisSonuc>> TaraAsync(string subnet, CancellationToken)
// AisSonuc: Ip, Ad, Mac, Uretici, Servisler
```

## HttpFingerprintService.cs

```csharp
sealed record HttpFingerprintSonuc(string? Marka, string? Tur, string? Model, string? Kaynak)
static Task<HttpFingerprintSonuc?> ProbeAsync(string ip, int port, CancellationToken, int timeoutMs = 1500)
```

Paralel `Task.WhenAll`, ilk başarılı yanıt kazanır.

| Endpoint | Vendor |
|---|---|
| `/ISAPI/System/deviceInfo` | Hikvision |
| `/cgi-bin/magicBox.cgi?action=getSystemInfo` | Dahua |
| `/api.cgi?cmd=GetDevInfo` | Reolink |
| `/onvif/device_service` | ONVIF |
| `/api/v1/status` | Ubiquiti UniFi |

Yalnızca "Derin tara" + HTTP port açıkken çağrılır.

## SnmpFingerprintService.cs

SNMP v1/v2c `sysDescr` / `sysName` (UDP 161, community `public`). Manuel ASN.1 DER.

```csharp
static Task<string?> SysDescrAsync(string ip, CancellationToken, int timeoutMs = 1500)
static Task<string?> SysNameAsync(string ip, CancellationToken, int timeoutMs = 1500)
```

OID: `sysDescr = 1.3.6.1.2.1.1.1.0`, `sysName = 1.3.6.1.2.1.1.5.0`.

## UbiquitiDiscoveryService.cs

UDP 10001 — UniFi AP / EdgeRouter / AirOS.

```csharp
internal sealed record UbiquitiKaydi(Ip, Mac?, Hostname?, Platform?, Firmware?, ModelKodu?)
static Task<IReadOnlyList<UbiquitiKaydi>> TaraAsync(string subnet, CancellationToken, int dinlemeMs = 2500)
```

- v1 probe `{0x01,0x00,0x00,0x00}` + v2 probe `{0x02,0x08,0x00,0x00}`.
- TLV parser unsigned-safe (byte shift integer overflow düzeltildi v0.3.0).

## MndpDiscoveryService.cs

MikroTik Neighbor Discovery (UDP 5678).

```csharp
internal sealed record MndpKaydi(Ip, Mac?, Identity?, Version?, Platform?, Board?, SoftwareId?)
static Task<IReadOnlyList<MndpKaydi>> TaraAsync(string subnet, CancellationToken, int dinlemeMs = 3000)
```

## OuiVendorLookup.cs

MAC OUI prefix → üretici. Önce `Req/oui.csv` (IEEE MA-L, ~30K), başarısız ise built-in fallback (~100).

```csharp
static string? Bul(string? mac)
static OuiBilgi? BulDetay(string? mac)   // sealed record(Vendor, TurIpucu, Mobil)
```

**Phantom guard:** `Bul`/`BulDetay` `IsValidUnicast(mac)` kontrolü — geçersiz MAC (all-zero, multicast) vendor eşlemesi almaz.

**`KisaltVendor`** — IEEE şirket adından kısa form. Kırpılan: `, Ltd.` ` Ltd` ` Limited` ` Foundation` ` Innovation Limited` ` Innovation` `, Inc.` ` LLC` ` Corporation` ` Corp.` ` GmbH` ` AG` ` Technology` ` Technologies` ` Electronics` ` Networks` ` Communications` ` Systems` ` Solutions` ` International` `(Shenzhen)` `(Shanghai)`.

**Normalize:** "Routerboard.com" / "Mikrotikls" → "MikroTik".

**v0.4.0+ fix:** `3C:46:D8` = "TP-Link" (önceden yanlış "EZVIZ").

## MacUtils.cs

```csharp
static string? Normalize(string? mac)        // her format → "XX:XX:XX:XX:XX:XX"
static string? OuiPrefix(string? mac)        // → "XX:XX:XX"
static bool    IsValidUnicast(string? mac)   // null/zero/multicast → false
```

Desteklenen format: `AA:BB:CC:DD:EE:FF`, `AA-BB-CC-DD-EE-FF`, `AABB.CCDD.EEFF`, `AABBCCDDEEFF`.

## PdfReportService.cs

QuestPDF ile PDF rapor.

```csharp
static byte[] GenerateDeviceScanReport(IEnumerable<DeviceScanRow> rows, ReportMetadata meta)
```

A4 yatay, kenar 18px/14px, 11 sütun, başlık `#0D3B66`, dönüşümlü satır `#0D1117`/`#101722`, altbilgi sayfa numarası. Statik ctor'da `QuestPDF.Settings.License = LicenseType.Community`.
