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
// Yol: %APPDATA%\AgTarama\settings.json
```

---

## AppSettings.cs — Tam Alan Listesi (v0.4.0)

```csharp
class AppSettings {
    // Genel
    int  HedefMB                { get; set; } = 16;
    int  TestSuresiSn           { get; set; } = 2;
    int  PingTimeoutMs          { get; set; } = 2000;
    int  PortTaramaConcurrency  { get; set; } = 50;
    int  PortTaramaTimeoutMs    { get; set; } = 1000;
    int  WlanAutoRefreshSeconds { get; set; } = 10;
    int  EvilTwinSinyalEsigi    { get; set; } = 75;  // 50-90
    bool SesAcik                { get; set; } = true;
    bool ToastAcik              { get; set; } = true;
    // AI (v0.4.0)
    bool   AiEnabled            { get; set; } = true;
    string AiSaglayici          { get; set; } = "OpenRouter"; // OpenRouter|Google|OpenAI|Custom
    string AiBaseUrl            { get; set; } = "https://openrouter.ai/api/v1";
    string AiModel              { get; set; } = "deepseek/deepseek-v4-flash";
    int    AiGunlukTokenLimiti  { get; set; } = 200_000;
    int    AiAylikTokenLimiti   { get; set; } = 5_000_000;
    bool   AiYerelIpMaskele     { get; set; } = false;
    // API anahtarı AppSettings'e ASLA yazılmaz — sadece AiKeyVault'ta.
}
```

---

## Services/Ai/ — AI Servisleri (v0.4.0)

### AiProvider.cs

Preset listesi:

| Id | Display | BaseUrl | DefaultModel |
|---|---|---|---|
| OpenRouter | OpenRouter | `https://openrouter.ai/api/v1` | `deepseek/deepseek-v4-flash` |
| Google | Google AI | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-2.0-flash` |
| OpenAI | OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| Custom | Özel | (kullanıcı girer) | (kullanıcı girer) |

```csharp
static AiProviderPreset GetById(string? id)  // bulunamazsa Presets[0] döner
```

---

### AiKeyVault.cs

API anahtarını DPAPI + AES-CBC/HMAC-SHA256 (machine-bound) ile şifreli saklar.

```csharp
static void Save(string apiKey)   // %APPDATA%\AgTarama\ai.vault (binary, opak)
static string? Load()             // null = anahtar yok / bozuk
static void Clear()
static bool HasKey()
```

---

### AiClient.cs

OpenAI-uyumlu `chat/completions` HTTP istemcisi.

```csharp
static Task<string> AskAsync(AppSettings settings, string systemPrompt, string userPrompt, CancellationToken)
static Task<string> ChatAsync(AppSettings settings, IReadOnlyList<AiChatMessage> messages, CancellationToken)
static Task<AiTestResult> TestConnectionAsync(AppSettings settings, string? explicitApiKey, CancellationToken)
// AiChatMessage: record(Role, Content)
// AiTestResult:  record(Success, Message, StatusCode, LatencyMs)
```

- `ChatAsync` öncesinde `AiEnabled`, günlük/aylık token limiti kontrolü yapar.
- Retry: max 2; 429 / 5xx için `700ms * (attempt+1)` bekleme.
- `Timeout = 60s`, `User-Agent = "AgTarama-AI/0.4.0"`.
- Hata mesajlarında API anahtarı `sk-or-***last4` formatıyla maskelenir.
- `AiUsageMeter.AddUsage(promptTokens, completionTokens)` her başarılı çağrıdan sonra çalışır.
- İptal semantiği: `cancellationToken` set edilmişse `OperationCanceledException` yutulmaz, çağırana propagate edilir.

---

### AiUsageMeter.cs

Günlük/aylık token sayacı. `%APPDATA%\AgTarama\ai.usage.json`.

```csharp
static AiUsageSnapshot Load()
static void AddUsage(int promptTokens, int completionTokens)
// Periyot rollover: yeni gün/ay başında sayaçlar sıfırlanır.
```

- `Load()` ve `AddUsage()` aynı `_lock` nesnesi altında çalışır; paralel AI isteklerinde race condition riski giderildi.

---

### AiPrompts.cs

```csharp
const string SohbetSystemPrompt  // Chatbot serbest sohbet — TR ağ asistanı
const string PcapSystemPrompt    // Pcap tshark istatistik analizi — top talker, anormallik, DNS/HTTP
const string CihazSystemPrompt   // Cihaz listesi analizi — KRITIK/ORTA/DUSUK sınıflandırma
```

---

### AiPcapAnalyzer.cs (Faz 3)

tshark istatistiklerini toplayıp AI'ya gönderir.

```csharp
static Task<string> AnalyzeAsync(string pcapPath, AppSettings settings, CancellationToken)
```

- tshark komutları: `-z conv,ip`, `-z io,stat,1`, `-z io,phs`, `-z endpoints,ip`, `-z http,tree`, `-z dns,tree`
- Her çıktı max 50 satıra kırpılır; toplam payload ≤ ~30KB.
- `AiYerelIpMaskele=true` ise private IP 3. oktet → `x` (`192.168.1.42` → `192.168.x.42`).
- Process cleanup: `WaitForExitAsync(ct)` sonrası finally bloğunda `Kill(entireProcessTree: true)` + `WaitForExitAsync(CancellationToken.None)` garantisi. stdout ve stderr paralel drainlenir (buffer dolmasından kaynaklanan blokaj önlendi).

---

### AiDeviceAnalyzer.cs (Faz 4)

Cihaz Tara sonuçlarını AI'ya gönderir.

```csharp
sealed record CihazDto(Ip, Ad, Tur, Marka, Model, Ping, Portlar, Kesif, Mac, Uretici, Servis, Guven)
static Task<string> AnalyzeAsync(IReadOnlyList<CihazDto> cihazlar, string talep, AppSettings settings, CancellationToken)
static readonly IReadOnlyList<Preset> Presetler  // 5 hazır preset
```

Hazır preset'ler: Güvenlik riski tespiti 🛡️, Kamera/NVR/DVR listesi 📷, AP/Router/Switch grubu 📡, Bilinmeyen cihaz sorguları ❓, Sonraki tarama önerisi 🔍.
Max 50 cihaz JSON olarak gönderilir; fazlası `"...ve N daha"` notu ile belirtilir.

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

- `AGT_UPDATE_SIGNER_THUMBPRINT` set edilmemişse imza doğrulaması atlanır ve log uyarısı yazılır.

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
static Task<bool> WifiAdaptorVarMiAsync()
// WlanSonuc: Ssid, Bssid, Auth, Encryption, Signal(%), Channel, RadioType, EvilTwin
```

- Evil-Twin tespiti: aynı SSID, birden fazla farklı BSSID → `EvilTwin = true`
- `WifiAdaptorVarMiAsync()`: async `WaitForExitAsync` kullanır; UI thread'ini bloke etmez. `WlanPanelBaslat()` sync çağrı yapmaz, adaptör kontrolü `BaslangicAsync()` → `WlanAdaptorKontrolAsync()` üzerinden yapılır.

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

## OuiVendorLookup.cs

MAC OUI prefix → üretici eşlemesi. Önce `Req/oui.csv` (IEEE MA-L, ~30K giriş) lazy yüklenir; başarısız olursa built-in fallback (~100 OUI) devreye girer.

```csharp
static string? Bul(string? mac)        // "AA:BB:CC:DD:EE:FF" → "Apple" veya null
static OuiBilgi? BulDetay(string? mac) // vendor + tür ipucu + mobil flag
// OuiBilgi: sealed record(Vendor, TurIpucu, Mobil)
```

**`BulDetay` tür ipuçları (`TurIpucu`):** `"Kamera"` (Hikvision, Dahua, Reolink, EZVIZ, Axis…), `"Yazıcı"` (HP Inc, Epson, Brother, Canon, Xerox…), `"Router/AP"` (Ubiquiti, MikroTik, TP-Link, Cisco…), `"NAS"` (Synology, QNAP), `"Hoparlör"` (Sonos), `"Akıllı Cihaz"` (Espressif, Tuya), `"Linux IoT"` (Raspberry Pi), `"Bilgisayar"` (VMware, Microsoft), `"Telefon"` (Mobil=true + tür yok).

**Phantom device guard:** `Bul(mac)` ve `BulDetay(mac)` başında `IsValidUnicast(mac)` kontrolü yapılır — geçersiz MAC'ler (all-zero, multicast) hiçbir zaman vendor eşlemesi almaz.

**`KisaltVendor`** — IEEE şirket adından kısa görünüm adı üretir. Kırpılan ekler: `, Ltd.` / ` Ltd` / ` Limited` / ` Foundation` / ` Innovation Limited` / ` Innovation` / `, Inc.` / ` LLC` / ` Corporation` / ` Corp.` / ` GmbH` / ` AG` / ` Technology` / ` Technologies` / ` Electronics` / ` Networks` / ` Communications` / ` Systems` / ` Solutions` / ` International` / `(Shenzhen)` / `(Shanghai)` vb.

**Normalizasyon:** `BulDetay` içinde "Routerboard.com" / "Mikrotikls" içeren vendor adları → "MikroTik" (IEEE `00:0C:42` "Routerboard.com" olarak kayıtlı).

**Fallback düzeltme (v0.4.0+):** `3C:46:D8` = "TP-Link" (daha önce yanlışlıkla "EZVIZ" yazılmıştı).

**Phantom device guard:** `BulDetay(null) = null` — null MAC'li cihazlar hiçbir zaman vendor sınıflandırması almaz.

---

## MacUtils.cs

MAC adresi normalizasyonu, OUI prefix çıkarma ve geçerlilik kontrolü.

```csharp
static string? Normalize(string? mac)     // her format → "XX:XX:XX:XX:XX:XX" (büyük harf, kolon)
static string? OuiPrefix(string? mac)     // → "XX:XX:XX" veya null
static bool    IsValidUnicast(string? mac)// geçerli unicast MAC mı? (null/all-zero/multicast → false)
```

- Desteklenen giriş formatları: `AA:BB:CC:DD:EE:FF`, `AA-BB-CC-DD-EE-FF`, `AABB.CCDD.EEFF` (Cisco dot), `AABBCCDDEEFF` (raw hex).
- 12 hex digit dışı karakterler kırpılır; 12 hex'e ulaşılamazsa giriş trim edilmiş haliyle döner.
- **`IsValidUnicast`:** `00:00:00:00:00:00`, multicast (bit0=1) ve broadcast MAC'leri reddeder. `ArpProbe` ve `OuiVendorLookup` bu guard'ı kullanarak hayalet cihaz (Xerox/00:00:00) oluşumunu önler.

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

## Services/Discovery/ — Cihaz Keşif Alt Sistemi (v0.4.0+)

`DeviceDiscoveryEngine` tarafından koordine edilen iki fazlı keşif motoru. Eski inline sweep kodu ve `AdvancedIpScannerService` çağrısı bu alt sistemle yerini aldı.

### IDeviceDiscoveryEngine / DeviceDiscoveryEngine

```csharp
interface IDeviceDiscoveryEngine {
    DeviceStore Store { get; }
    bool NpcapAvailable { get; }
    Task StartScanAsync(IReadOnlyList<(string Prefix, int Start, int End)> subnets,
                        ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken);
    Task StartLiveAsync(IReadOnlyList<(string Prefix, int Start, int End)> subnets,
                        ScanOptions options, CancellationToken);
}
```

**İki fazlı tarama (`StartScanAsync`):**
- **Faz 1:** FastProbes + Listener'lar paralel çalışır; `taranan` sayacı subnet başına bir kez artırılır.
- **Faz 2:** DeepProbes — yalnızca `TryGet` ile mevcut host'lar işlenir; phantom device oluşmaz.
- Tarama sonu: tüm cihazlar için `OuiVendorLookup.Bul(mac)` ile üretici tamamlama.

**Sürekli izleme (`StartLiveAsync`):** Listener'lar sürekli, ArpProbe periyodik (`LiveRefreshIntervalMs`). `LiveOfflineThresholdMs` geçen cihazlar `Online=false` olarak işaretlenir.

### DeviceStore

```csharp
DeviceInfo GetOrAdd(string ip)                   // yeni veya mevcut
bool TryGet(string ip, out DeviceInfo? dev)      // keşfedilmişse true
void NotifyChanged(DeviceInfo dev)               // LastSeen güncelle + event
void Touch(string ip)
void Upsert(DeviceInfo updated)
void Clear()
IReadOnlyList<DeviceInfo> All { get; }
int Count { get; }
event EventHandler<DeviceInfo>? DeviceChanged
```

`ConcurrentDictionary<string, DeviceInfo>` üzerine kurulu; `DeviceChanged` eventi UI katmanına anlık bildirim sağlar.

**IP normalizasyonu:** `GetOrAdd` / `TryGet` / `Touch` / `Upsert` çağrılarında `IPAddress.TryParse` ile normalize edilir → `"192.168.001.010"` ve `"192.168.1.10"` aynı anahtara eşlenir.

### ScanOptions

```csharp
bool  DeepScan              = false
bool  LiveMode              = false
int[] Ports                 = DefaultPorts  // 22,23,53,80,135,139,443,445,554,1900,3389,5000,5357,7547,8000,8080,8443,9000,37777
int   ConcurrencyLimit      = 80
int   PingTimeoutMs         = 1000
int   PortTimeoutMs         = 800
int   ArpTimeoutMs          = 3000
int   ListenerDurationMs    = 8000
int   LiveRefreshIntervalMs = 30_000
int   LiveOfflineThresholdMs = 90_000
```

### DeviceInfo (Models/DeviceInfo.cs)

Ana model sınıfı — tüm probe'lar bu nesneyi ortak günceller.

**`Online` başlangıç değeri `false`** — yalnızca gerçek kanıt (ARP yanıtı, ICMP, SNMP, LLMNR, vb.) probe'u `Online = true` set eder. Bu şekilde hayalet giriş "Online" görünmez.

| Alan grubu | Alanlar |
|---|---|
| Kimlik | `Ip`, `MacAdresi`, `Uretici` |
| Durum | `Online` (default false), `FirstSeen`, `LastSeen`, `PingYanit`, `PingMs`, `PingTtl` |
| Portlar | `AcikPortlar List<int>`, `ServisDetaylari Dictionary<int,string>` |
| ONVIF/WSD | `OnvifBulundu`, `OnvifAdi`, `OnvifHardware`, `OnvifServisUrl`, `WsdTipi` |
| SSDP | `SsdpBulundu`, `SsdpFriendlyName`, `SsdpManufacturer`, `SsdpModelName`, `SsdpSunucu` |
| DNS/NetBIOS | `DnsAdi`, `PingAdi`, `NetbiosCihazAdi`, `NetbiosGrupAdi` |
| SMB/SSH | `SmbComputerName`, `SmbOs`, `SshBanner` |
| LLMNR | `LlmnrHostname` |
| mDNS | `MdnsMarka`, `MdnsTur` |
| Ubiquiti | `UbntPlatform`, `UbntFirmware`, `UbntHostname` |
| MikroTik | `MikroTikBoard`, `MikroTikVersion`, `MikroTikIdentity` |
| SNMP | `SnmpSysDescr`, `SnmpSysName` |
| HTTP | `HttpFpMarka`, `HttpFpTur`, `HttpFpModel`, `SunucuBasligi`, `SayfaBasligi` |
| Diğer | `RtspDurum`, `Os`, `KesifKaynaklari HashSet<string>`, `KararIzi KimlikKararIzi?` |

### ScanProgress

```csharp
sealed record ScanProgress(int Taranan, int Toplam, int BulunanCihaz, string AsamaMetni, int PaketSayisi = 0)
```

### Probes

**FastProbes** (Faz 1 — paralel):

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `ArpProbe` | ARP | MAC, IP, Online |
| `IcmpProbe` | ICMP Echo | PingYanit, PingMs, PingTtl |
| `TcpPortProbe` | TCP SYN | AcikPortlar, ServisDetaylari |
| `NetbiosProbe` | UDP 137 | NetbiosCihazAdi, NetbiosGrupAdi |
| `LlmnrProbe` | UDP 5355 | LlmnrHostname (PTR parse yeniden yazıldı; `.arpa` hostname'ler reddedilir) |
| `NdpProbe` | IPv6 NDP | IPv6 komşu keşfi |

**DeepProbes** (Faz 2 — yalnızca FastProbe'un keşfettiği host'larda):

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `SnmpProbe` | UDP 161 | SnmpSysDescr, SnmpSysName |
| `HttpFingerprintProbe` | HTTP/HTTPS | HttpFpMarka, HttpFpTur, HttpFpModel |
| `SmbProbe` | TCP 445 | SmbComputerName, SmbOs |
| `SshBannerProbe` | TCP 22 | SshBanner, Os |

**Phantom device guard:** DeepProbe'lar `store.TryGet(ip)` ile host'u kontrol eder; keşfedilmemişse `return` — hiç `DeviceInfo` oluşturmaz.

### Listeners (broadcast/multicast dinleyiciler)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `OnvifWsdListener` | UDP 3702 | ONVIF WS-Discovery + WSD |
| `SsdpListener` | UDP 1900 | SSDP/UPnP, SsdpFriendlyName |
| `MdnsListener` | UDP 5353 | MdnsMarka, MdnsTur (25+ servis) |
| `PassivePacketSniffer` | pcap | MAC lookup (Npcap varsa) |
| `MndpListener` *(derin)* | UDP 5678 | MikroTikBoard, Identity |
| `UbiquitiListener` *(derin)* | UDP 10001 | UbntPlatform, Firmware |

Listener'lar `ListenerDurationMs` (varsayılan 8s) boyunca çalışır. `PassivePacketSniffer` için `PcapHelper.IsNpcapAvailable` kontrolü yapılır.

### PcapHelper

```csharp
static bool IsNpcapAvailable  // npcap dll varlık kontrolü
```

---

## AgTarama.Tests — Test Projesi (v0.4.0+)

`AgTarama.Tests.csproj` — net10.0-windows, xUnit 2.9.2, xunit.runner.visualstudio 2.8.2, coverlet.collector 6.0.2. Ana projeye `<ProjectReference>` bağlı; `InternalsVisibleTo` ile `internal` türlere erişir.

| Test sınıfı | Test sayısı | Kapsam |
|---|---|---|
| `OuiVendorLookupTests` | 18 | null/boş MAC → null (phantom device guard), vendor kısaltma, BulDetay tür ipuçları, routerboard normalizasyonu |
| `MacUtilsTests` | 12 | Kolon/dash/dot/raw format, null/boş, OuiPrefix |
| `DeviceStoreTests` | 8 | GetOrAdd, TryGet, Clear, DeviceChanged event, LastSeen güncelleme, All |
| `ProbeTests` | 10 | `SmbProbe_EmptyStore_CreatesNoPhantomDevices`, `SshBannerProbe_EmptyStore_CreatesNoPhantomDevices` (regresyon), port gate testi, TryGet contract |

Çalıştırma: `dotnet test AgTarama.Tests/AgTarama.Tests.csproj`

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

---

## AI Servisleri (2026-05-17)

- `Services/CryptoHelper.cs`:
  - AES-CBC + HMAC yardımcıları ortaklaştırıldı.
- `Services/Ai/AiClient.cs`:
  - OpenAI uyumlu `chat/completions` istemcisi.
  - Model/BaseUrl `AppSettings.AiModel` / `AppSettings.AiBaseUrl` üzerinden okunur (hardcoded değil); fallback `AiProvider.GetById(settings.AiSaglayici)`.
  - `TestConnectionAsync(settings, explicitApiKey?)` — UI'dan kaydetmeden test yapılabilir.
  - `ChatAsync` başında `AiEnabled`, günlük/aylık token limiti kontrolü; aşılırsa `InvalidOperationException`.
- `Services/Ai/AiKeyVault.cs`:
  - `%APPDATA%/AgTarama/ai.vault` içinde şifreli API anahtarı saklama (DPAPI + AES-HMAC).
- `Services/Ai/AiDefaultKey.cs`:
  - XOR-obfuscated varsayılan anahtar (yerinde, vault yoksa `EnsureDefaultKey()` otomatik yükler).
- `Services/Ai/AiUsageMeter.cs`:
  - `%APPDATA%/AgTarama/ai.usage.json` günlük/aylık token sayaçları; gün/ay değişiminde sıfırlanır.
- `Services/Ai/AiPrompts.cs`:
  - Sistem prompt sabitleri.
- `Services/Ai/AiProvider.cs`:
  - Sağlayıcı preset tanımları (OpenRouter/Google/OpenAI/Custom). Default model: `deepseek/deepseek-v4-flash`.
