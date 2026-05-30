# Cihaz Keşif Alt Sistemi (`Services/Discovery/`)

İki fazlı keşif motoru. Eski inline sweep kodu + `AdvancedIpScannerService` çağrısı bu mimaride yerini aldı.

## IDeviceDiscoveryEngine / DeviceDiscoveryEngine

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

### Üç Fazlı Tarama (`StartScanAsync`)

- **Faz 0:** `ArpProbe` tek başına çalışır, store'a online host'ları yazar. (`SkipDeadHosts=true` bu adımı tamamlandıktan sonra devreye girer.)
- **Faz 1:** `BuildFastProbesWithoutArp` + Listener'lar paralel. `taranan` sayacı subnet başına bir kez artırılır.
- **Faz 2:** DeepProbes — yalnızca `TryGet` ile mevcut host'lar işlenir; phantom device oluşmaz.
- **Sonu:** Tüm cihazlar için `OuiVendorLookup.Bul(mac)` ile üretici tamamlama.

### Sürekli İzleme (`StartLiveAsync`)

Listener'lar sürekli, `ArpProbe` periyodik (`LiveRefreshIntervalMs`). `LiveOfflineThresholdMs` geçen cihazlar `Online=false` işaretlenir.

## DeviceStore

```csharp
DeviceInfo GetOrAdd(string ip)
bool TryGet(string ip, out DeviceInfo? dev)
void NotifyChanged(DeviceInfo dev)
void Touch(string ip)
void Upsert(DeviceInfo updated)
void Clear()
IReadOnlyList<DeviceInfo> All { get; }
int Count { get; }
event EventHandler<DeviceInfo>? DeviceChanged
```

- `ConcurrentDictionary<string, DeviceInfo>` üzerine kurulu.
- `DeviceChanged` event UI'ye anlık bildirim.
- **IP normalizasyonu:** `GetOrAdd` / `TryGet` / `Touch` / `Upsert` çağrılarında `IPAddress.TryParse` → `"192.168.001.010"` ve `"192.168.1.10"` aynı anahtar.

## ScanOptions

```csharp
bool  DeepScan              = false
bool  LiveMode              = false
bool  SkipDeadHosts         = true   // ARP Faz 0'da görülmeyenleri TCP'de atla
int[] Ports                 = DefaultPorts
// DefaultPorts: 22,23,53,80,135,139,443,445,554,1900,3389,5000,5357,7547,8000,8080,8443,9000,37777
int   ConcurrencyLimit      = 80
int   PingTimeoutMs         = 600    // (eski: 1000)
int   PortTimeoutMs         = 450    // (eski: 800)
int   ArpTimeoutMs          = 3000
int   ListenerDurationMs    = 8000
int   LiveRefreshIntervalMs = 30_000
int   LiveOfflineThresholdMs = 90_000
```

## DeviceInfo (`Models/DeviceInfo.cs`)

Ana model sınıfı — tüm probe'lar bu nesneyi ortak günceller.

**`Online` default `false`** — yalnızca gerçek kanıt (ARP yanıtı, ICMP, SNMP, LLMNR…) probe'u `Online = true` set eder. Phantom giriş "Online" görünmez.

| Alan grubu | Alanlar |
|---|---|
| Kimlik | `Ip`, `MacAdresi`, `Uretici`, `IsGateway` |
| Durum | `Online`, `FirstSeen`, `LastSeen`, `PingYanit`, `PingMs`, `PingTtl` |
| DHCP | `DhcpHostname`, `DhcpVendorClass`, `DhcpFingerprint` |
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
| Telnet/RTSP/MQTT | `TelnetBanner string?`, `RtspServerHeader string?`, `MqttBulundu bool` |
| Diğer | `RtspDurum`, `Os`, `KesifKaynaklari HashSet<string>`, `KararIzi KimlikKararIzi?` |

## ScanProgress

```csharp
sealed record ScanProgress(int Taranan, int Toplam, int BulunanCihaz, string AsamaMetni, int PaketSayisi = 0, string? Detay = null)
```

**Toplam = `hostCount * 2`** — ICMP + TcpPortProbe iki kaynak host başına +1 atar. Yüzde = `Taranan/Toplam`. ICMP bittiğinde %50, TCP-Port da bittiğinde %100. Tek probe'la erken %100 görünmesi engellendi.

**`Detay`** opsiyonel — probe/listener başlangıç/bitiş bildirimi (UI overlay'ine son işlem satırı). Örn: `▶ Subnet 192.168.1.0/24 başlatıldı`, `📡 Listener'lar açıldı (5 adet, 8s)`, `⚡ Faz 1 — Hızlı probe'lar başladı (6 adet)`, `✓ ICMP tamamlandı`, `🔍 Faz 2 — Derin probe'lar başladı (4 adet)`.

## Gateway Tespit

`StartScanAsync` sonunda `NetworkInterface.GetAllNetworkInterfaces()`'den `GatewayAddresses` toplanır. Store içinde eşleşen IP'lerin `IsGateway=true` set edilir. `KanitTopla_Gateway` (DeviceClassifier) bu cihazlara `KanitAgirlik.GatewayTur=50` puan ile zorunlu `Router/AP` türü ekler — başka herhangi bir tür kanıtından ağır basar.

## Probes

### Faz 0 — ARP ön tarama

`ArpProbe` tek başına çalışır, store'a online host'ları yazar. `SkipDeadHosts=true` olduğunda Faz 1 `TcpPortProbe`, burada görülmeyen IP'lere TCP denemez (store boşsa kural dışı).

### FastProbes (Faz 1 — paralel, ARP hariç)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `IcmpProbe` | ICMP Echo | PingYanit, PingMs, PingTtl (opsiyonel `onHostDone` callback — progress sayacı için) |
| `TcpPortProbe` | TCP SYN | AcikPortlar, ServisDetaylari (`SkipDeadHosts`: store'da olmayan IP'yi atlar; `onHostDone` callback — yüzde için) |
| `NetbiosProbe` | UDP 137 | NetbiosCihazAdi, NetbiosGrupAdi |
| `LlmnrProbe` | UDP 5355 | LlmnrHostname (PTR parse; `.arpa` reddedilir; 2s timeout) |
| `NdpProbe` | IPv6 NDP | IPv6 komşu |

### DeepProbes (Faz 2 — yalnızca keşfedilmiş host'larda)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `SnmpProbe` | UDP 161 | SnmpSysDescr, SnmpSysName |
| `HttpFingerprintProbe` | HTTP/HTTPS | HttpFpMarka, HttpFpTur, HttpFpModel |
| `SmbProbe` | TCP 445 | SmbComputerName, SmbOs |
| `SshBannerProbe` | TCP 22 | SshBanner, Os |
| `TelnetBannerProbe` | TCP 23 | TelnetBanner; banner'dan router/switch OS + marka çıkarımı |
| `RtspProbe` | TCP 554 | RtspServerHeader; RTSP OPTIONS ile kamera Server header doğrulaması |
| `MqttProbe` | TCP 1883 | MqttBulundu; MQTT 3.1.1 CONNACK ile IoT broker onayı |

**Phantom device guard:** DeepProbe'lar `store.TryGet(ip)` ile host kontrol eder; keşfedilmemişse `return` — `DeviceInfo` oluşturmaz.

## Listeners (broadcast/multicast dinleyiciler)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `OnvifWsdListener` | UDP 3702 | ONVIF WS-Discovery + WSD |
| `SsdpListener` | UDP 1900 | SSDP/UPnP, SsdpFriendlyName |
| `MdnsListener` | UDP 5353 | MdnsMarka, MdnsTur (25+ servis) + DNS message parse → `<host>.local` → `DnsAdi` |
| `PassivePacketSniffer` | pcap | MAC lookup (Npcap varsa) |
| `DhcpListener` | UDP 67/68 (pcap) | DhcpHostname, DhcpVendorClass, DhcpFingerprint (Npcap varsa). BPF: `udp port 67 or 68`. BOOTP + Options 12/55/60 parse |
| `MndpListener` *(derin)* | UDP 5678 | MikroTikBoard, Identity |
| `UbiquitiListener` *(derin)* | UDP 10001 | UbntPlatform, Firmware |

Listener'lar `ListenerDurationMs` (default 8s) boyunca çalışır. `PassivePacketSniffer` için `PcapHelper.IsNpcapAvailable` kontrolü.

## PcapHelper

```csharp
static bool IsNpcapAvailable
```

## Classification (`Services/Discovery/Classification/`)

`KimlikKararIzi` — sınıflandırma gerekçesini saklar (kanıt sırası + ağırlık).

Kanıt tabanlı sınıflandırma `Partials/MainWindow.DeviceClassifier.cs`'de:
- `MarkaNormalize(string)` — vendor normalize (Hikvision, Dahua, MikroTik, TP-Link, Apple, vb.).
- `KimlikBelirleV2(DeviceInfo)` — `CihazKimlik { Marka, Model, Tur, TurIkon }` döner.

Kanıt sırası (yüksek güven → düşük): Gateway IP (50) → Ubiquiti TLV (50/60) → MikroTik identity (50/60) → HTTP fingerprint (35/55) → SNMP (45/50) → ONVIF+WSD (45) → DHCP vendor class (35) → mDNS tür (40) → RTSP server header (35) → SSDP manufacturer (30/35) → Telnet banner (30) → AdHostname / SMB (25-35) → NetBIOS (25) → MQTT CONNACK (20) → OUI vendor (40 marka / 18 tür) → port pattern fallback (10-25).

`KanitAgirlik` sabitleri: `Services/Discovery/Classification/ClassificationTypes.cs`. `MinKararEsigi=12` — eşiğin altındaki kanıtlar UI'a yansımaz.

**WIN- hostname notu:** Windows default hostname (`WIN-XXXX`) `KanitTopla_AdHostname`'de `AdHostnameTur=30` ağırlık alır (eski: -10 penaltı). LLMNR(15)+AdHostname(30)=45 → mDNS Amazon/IoT sinyali (40) üzerinde kalır.
