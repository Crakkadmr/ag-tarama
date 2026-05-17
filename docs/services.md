# Services Katmanı Referansı

Lisans/güvenlik servisleri için: [docs/licensing.md](licensing.md)
NuGet bağımlılıkları: `QuestPDF 2024.12.*` (PDF), `ClosedXML 0.102.*` (XLSX)

---

## InterfaceDiscoveryService.cs (72 satır)

`tshark -D` çıktısını parse eder, aktif arayüzleri döner.

```csharp
Task<List<ArayuzBilgi>> TumunuGetirAsync()
Task<int> PaketSayisiAsync(ArayuzBilgi, CancellationToken)
```

**v0.3.0:** `Process.Start(psi)!` null-forgiveness kaldırıldı. tshark yoksa `TumunuGetirAsync` artık `InvalidOperationException("tshark başlatılamadı: ...")` fırlatır; `PaketSayisiAsync` null process durumunda sessizce 0 döner.

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

**v0.3.0:** Exception filtresi `catch (Exception ex) when (ex.GetBaseException() is not OperationCanceledException)`. `AggregateException` içine sarılı iptal artık yanlışlıkla loglanmıyor.

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
**Not:** `nbtstat`/`ping` çıktısı için `ProcessStartInfo.StandardOutputEncoding = Encoding.GetEncoding(OEMCodePage)` zorunlu (Türkçe karakter sorunu düzeltildi).

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

## HistoryService.cs (~95 satır)

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

**v0.3.0:**
- `Id` formatı: `yyyyMMdd_HHmmss_fff_{guid8}_{type}` — millisaniye collision imkânsız.
- `SonKayitlariYukle(limit)` artık önce dosyaları `LastWriteTimeUtc`'ye göre sıralayıp sadece ilk `limit` tanesini deserialize ediyor. 1000+ kayıtta bellek kazancı.

---

## SettingsService.cs (30 satır)

```csharp
static AppSettings Yukle()
static void Kaydet(AppSettings ayarlar)
// AppSettings: HedefMB (int), TestSuresiSn (int)
// Yol: %APPDATA%\AgTarama\settings.json
```

---

## FavoriService.cs (~60 satır)

```csharp
static bool Ekle(string ip)            // dönüş: false = zaten var
static void Sil(string ip)
static List<string> YukleHepsi()
// Yol: %APPDATA%\AgTarama\favorites.json
```

**v0.3.0:** IP normalizasyonu eklendi. `Normalize(s)` `IPAddress.TryParse` ile `"192.168.001.1"` → `"192.168.1.1"` çevirir; karşılaştırmalar `OrdinalIgnoreCase`. Aynı IP'nin farklı yazımları artık tek favori olarak değerlendiriliyor.

---

## UpdateService.cs (~310 satır)

GitHub Releases API kontrolü, ZIP indirme, PowerShell self-update.

```csharp
Task<UpdateBilgi?> GuncellemeyiKontrolEtAsync()
Task IndirVeKurAsync(string indirmeUrl, IProgress<double> progress, CancellationToken)
// Deterministic ZIP seçimi: AgTarama-v*-win-x64.zip + .sha256 zorunlu
// Opsiyonel: AGT_UPDATE_SIGNER_THUMBPRINT env var ile thumbprint pinning
```

**v0.3.0 — Güvenlik sertleştirmesi:** `ZipFile.ExtractToDirectory` yerine `SafeExtractZip`:
- Entry sayısı ≤ 5000, toplam açılmış boyut ≤ 500 MB, tek entry ≤ 200 MB.
- Mutlak yol, sürücü harfi, `..` içeren entry reddedilir.
- Her entry'nin canonical hedef yolu `extractTo` altında olduğu doğrulanır (Zip Slip).

---

## SecurityService.cs (~90 satır)

Debugger + analiz aracı tespiti. Release-only (DEBUG'da no-op).

```csharp
static void Dogrula()  // App_Startup'tan çağrılır; tespit edilirse uygulama kapanır
```

**v0.3.0:** `#if DEBUG return; #endif` deseni `#if DEBUG ... #else ... #endif` ile değiştirildi; CS0162 "unreachable code" uyarısı giderildi.

---

## AppSettings.cs

Model sınıfı:
```csharp
class AppSettings {
    int  HedefMB                { get; set; } = 16;
    int  TestSuresiSn           { get; set; } = 2;
    int  PingTimeoutMs          { get; set; } = 2000;
    int  PortTaramaConcurrency  { get; set; } = 50;
    int  PortTaramaTimeoutMs    { get; set; } = 1000;
    int  WlanAutoRefreshSeconds { get; set; } = 10;
    int  EvilTwinSinyalEsigi    { get; set; } = 75;  // v0.3.0 (50-90)
    bool SesAcik                { get; set; } = true;
    bool ToastAcik              { get; set; } = true;
}
```

**v0.3.0:** `EvilTwinSinyalEsigi` eklendi. `SupheliEvilTwinSinyalleriniGuncelle` hardcode 75 yerine bu ayarı `Math.Clamp(50, 90)` ile kullanıyor.

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

## BandwidthHistoryService.cs

In-memory dairesel buffer, bant genişliği zaman serisi.

```csharp
static void RecordTick(double totalRxBps, double totalTxBps)
static (double[] Rx, double[] Tx) GetAggregate(int seconds)
static (double PeakRx, double PeakTx, double AvgRx, double AvgTx,
        long TotalRxMB, long TotalTxMB) Stats(int seconds)
```

- Kapasite: 3600 örnek (1 saat), dairesel `_head` işaretçi ile
- `GetAggregate(sn)` → son `sn` saniyelik örnekleri kronolojik sırada döner

**v0.3.0 — Thread safety:** `_rxBuf`, `_txBuf`, `_head`, `_count` tüm erişim `lock (_sync)` altında. DispatcherTimer + background thread aynı anda RecordTick/GetSnapshot çağırsa bile data corruption riski yok.

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

## UbiquitiDiscoveryService.cs (v0.2.0; v0.3.0 TLV fix)

UDP 10001 üzerinden Ubiquiti UniFi AP / EdgeRouter / AirOS cihazlarını keşfeder.

```csharp
internal sealed record UbiquitiKaydi(string Ip, string? Mac, string? Hostname,
    string? Platform, string? Firmware, string? ModelKodu);
static Task<IReadOnlyList<UbiquitiKaydi>> TaraAsync(string subnet, CancellationToken, int dinlemeMs = 2500)
```

- v1 probe: `{0x01,0x00,0x00,0x00}` + v2 probe: `{0x02,0x08,0x00,0x00}` → subnet broadcast + global broadcast.
- TLV parser: `0x01`=MAC, `0x02`=MAC+IP, `0x03`=firmware, `0x0B`=hostname, `0x0C`=platform, `0x14`=modelCode.
- Sonuç: `Marka=Ubiquiti`, `Model=<platform>`, `Tur=Erişim Noktası` veya `Router/AP`.
- **v0.3.0:** Byte shift integer overflow düzeltildi: `((buf[index + 1] & 0xFF) << 8) | (buf[index + 2] & 0xFF)`. Sınır kontrolü `uzunluk > buf.Length - index` (taşmaya dayanıklı). Yüksek bit set TLV uzunluğu artık negatif int'e dönüşmüyor.

---

## MndpDiscoveryService.cs (v0.2.0; v0.3.0 TLV fix)

MikroTik Neighbor Discovery Protocol (UDP 5678) — aktif probe + pasif broadcast dinleme.

```csharp
internal sealed record MndpKaydi(string Ip, string? Mac, string? Identity,
    string? Version, string? Platform, string? Board, string? SoftwareId);
static Task<IReadOnlyList<MndpKaydi>> TaraAsync(string subnet, CancellationToken, int dinlemeMs = 3000)
```

- Probe: `{0x00,0x00,0x00,0x00}` → broadcast; UDP 5678'e bind ederek ~30s aralıklı broadcast'ları da yakalar.
- TLV: `0x01`=MAC, `0x05`=identity, `0x07`=version, `0x08`=platform, `0x0B`=softId, `0x0C`=board.
- Sonuç: `Marka=MikroTik`, `Model=<board>`, `Tur=Router/AP`.
- **v0.3.0:** TLV `tip` ve `uzunluk` alanları `& 0xFF` ile unsigned okunuyor; sınır kontrolü taşmaya dayanıklı. Malformed MNDP paketinde buffer overread riski giderildi.

---

## SnmpFingerprintService.cs (yeni — v0.2.0)

SNMP v1/v2c `sysDescr` / `sysName` probe (UDP 161, community `public`). Manuel ASN.1 DER kodlama (NuGet bağımlılığı yok).

```csharp
static Task<string?> SysDescrAsync(string ip, CancellationToken, int timeoutMs = 1500)
static Task<string?> SysNameAsync(string ip, CancellationToken, int timeoutMs = 1500)
```

- OID: `sysDescr = 1.3.6.1.2.1.1.1.0`, `sysName = 1.3.6.1.2.1.1.5.0`.
- Yalnızca port 161 açık tespit edildiğinde `ServisDetaylariniGuncelleAsync` içinden çağrılır.
- Yanıt örnekleri: `"Cisco IOS..."` → Switch; `"HP ETHERNET..."` → Yazıcı; `"RouterOS RB951..."` → MikroTik.

---

## OuiVendorLookup.cs (yeni — v0.2.0)

MAC OUI prefix → üretici eşlemesi (built-in fallback, Advanced IP Scanner DB yokken devreye girer).

```csharp
static string? Bul(string? mac)   // "AA:BB:CC:DD:EE:FF" → "Apple" veya null
```

- ~100 OUI girdisi: Apple, Samsung, Xiaomi, Huawei, Google, Hikvision, Dahua, Axis, Ubiquiti, MikroTik, TP-Link, Cisco, NETGEAR, ASUS, D-Link, Synology, QNAP, Espressif, Raspberry Pi, Sonos, Amazon, Reolink, EZVIZ, Tuya, Sony, LG, VMware.
- `ArpBilgileriniTopluGuncelleAsync` içinden `UreticiAra` sonucu `null` gelirse `OuiVendorLookup.Bul(mac)` denenir.

---

## HttpFingerprintService.cs (yeni — v0.2.0)

Açık HTTP/HTTPS portuna vendor-specific endpoint'leri paralel deneyerek marka/model belirler.

```csharp
internal sealed record HttpFingerprintSonuc(string? Marka, string? Tur, string? Model, string? Kaynak);
static Task<HttpFingerprintSonuc?> ProbeAsync(string ip, int port, CancellationToken, int timeoutMs = 1500)
```

- Paralel `Task.WhenAll`, ilk başarılı yanıt kazanır.
- Endpoint → vendor eşlemesi:

| Endpoint | Vendor |
|---|---|
| `/ISAPI/System/deviceInfo` | Hikvision |
| `/cgi-bin/magicBox.cgi?action=getSystemInfo` | Dahua |
| `/api.cgi?cmd=GetDevInfo` | Reolink |
| `/onvif/device_service` | ONVIF probe |
| `/api/v1/status` | Ubiquiti UniFi |

- Yalnızca "Derin tara" modu açıkken ve HTTP port açıkken çağrılır.

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
