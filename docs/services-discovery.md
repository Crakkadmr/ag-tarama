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

### İki Fazlı Tarama (`StartScanAsync`)

- **Faz 1:** FastProbes + Listener'lar paralel. `taranan` sayacı subnet başına bir kez artırılır.
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
int[] Ports                 = DefaultPorts
// DefaultPorts: 22,23,53,80,135,139,443,445,554,1900,3389,5000,5357,7547,8000,8080,8443,9000,37777
int   ConcurrencyLimit      = 80
int   PingTimeoutMs         = 1000
int   PortTimeoutMs         = 800
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
| Kimlik | `Ip`, `MacAdresi`, `Uretici` |
| Durum | `Online`, `FirstSeen`, `LastSeen`, `PingYanit`, `PingMs`, `PingTtl` |
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

## ScanProgress

```csharp
sealed record ScanProgress(int Taranan, int Toplam, int BulunanCihaz, string AsamaMetni, int PaketSayisi = 0)
```

## Probes

### FastProbes (Faz 1 — paralel)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `ArpProbe` | ARP | MAC, IP, Online |
| `IcmpProbe` | ICMP Echo | PingYanit, PingMs, PingTtl |
| `TcpPortProbe` | TCP SYN | AcikPortlar, ServisDetaylari |
| `NetbiosProbe` | UDP 137 | NetbiosCihazAdi, NetbiosGrupAdi |
| `LlmnrProbe` | UDP 5355 | LlmnrHostname (PTR parse; `.arpa` reddedilir) |
| `NdpProbe` | IPv6 NDP | IPv6 komşu |

### DeepProbes (Faz 2 — yalnızca keşfedilmiş host'larda)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `SnmpProbe` | UDP 161 | SnmpSysDescr, SnmpSysName |
| `HttpFingerprintProbe` | HTTP/HTTPS | HttpFpMarka, HttpFpTur, HttpFpModel |
| `SmbProbe` | TCP 445 | SmbComputerName, SmbOs |
| `SshBannerProbe` | TCP 22 | SshBanner, Os |

**Phantom device guard:** DeepProbe'lar `store.TryGet(ip)` ile host kontrol eder; keşfedilmemişse `return` — `DeviceInfo` oluşturmaz.

## Listeners (broadcast/multicast dinleyiciler)

| Sınıf | Protokol | Keşfeder |
|---|---|---|
| `OnvifWsdListener` | UDP 3702 | ONVIF WS-Discovery + WSD |
| `SsdpListener` | UDP 1900 | SSDP/UPnP, SsdpFriendlyName |
| `MdnsListener` | UDP 5353 | MdnsMarka, MdnsTur (25+ servis) |
| `PassivePacketSniffer` | pcap | MAC lookup (Npcap varsa) |
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

Kanıt sırası (yüksek güven → düşük): Ubiquiti TLV → MikroTik identity → HTTP fingerprint → SNMP → ONVIF+WSD → mDNS tür → SSDP manufacturer → NetBIOS+SMB → OUI vendor → port pattern fallback.
