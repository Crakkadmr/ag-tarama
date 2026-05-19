# Cihaz Tara — Komple Modüler Refactor Planı

## Bağlam (Context)

**Sorun:** Mevcut "Cihaz Tara" sekmesi (`Partials/MainWindow.DeviceScan.cs`, ~2340 satır) tarama sırasında external bir araca bağımlı (`tools\Ip_Scanner\advanced_ip_scanner_console.exe` üzerinden Advanced IP Scanner çağrılıp parse ediliyor) ve birkaç kritik kategoride zayıf:

> **Not:** `tools/Ip_Scanner/` klasörü ve `advanced_ip_scanner_console.exe` dosyası KALDIRILMAZ. Uygulamada başka bir yerde (Cihaz Tara dışı) external olarak Advanced IP Scanner'ı GUI olarak açan bir tuş var ve oraya dokunulmayacak. Refactor sadece Cihaz Tara'nın tarama motorundan Advanced IP Scanner bağımlılığını çıkarır.

- **Aktif ARP request gönderme yok** — sadece `arp -a` cache okuma. Cache'de olmayan sessiz/firewall'lı cihazlar görünmüyor.
- **IPv6 desteği sıfır** — NDP, ICMPv6, link-local yok.
- **Pcap/packet library yok** — passive sniff yapılamıyor, dolayısıyla broadcast/multicast yanıtı sızıntıları kaçırılıyor.
- **LLMNR / DHCP / SMB banner / SSH banner yok** — Windows hostname keşfi ve OS tanıma eksik.
- **Concurrency güvenliği bozuk** — `_kameraBilgileri` Dictionary + ObservableCollection üzerinde lock yok, race condition var.
- **CancellationToken zincirleme hatalı** — nested `CancelAfter()` çağrıları timeout override ediyor.
- **"Weak evidence drop"** — düşük skorlu kanıtlı cihazlar tablodan düşürülüyor → cihaz kaybı.
- **mDNS/SSDP timeout kısa** (3s) — paket kaybında cihaz drop.

**Amaç:** External araç tamamen kaldır. Tek başına, kendi içinde hata payı düşük, piyasadaki cihaz tiplerinin (ARP-only IoT, IPv6-only modern cihaz, mDNS-only Apple, SSDP UPnP, SMB-only PC, vb.) tamamını gören modüler bir keşif motoru yaz. UI'da mevcut 13 sütun + context menu + 5 export formatı korunur; sadece sütun ekleme (OS, Son Görülme, Yanıt ms, Durum) ve "Sürekli izle" (Live mode) toggle eklenir. Tüm Türkçe etiketler korunur.

**Kullanıcı kararları (onaylı):**
1. SharpPcap + PacketDotNet eklenecek; Npcap yoksa otomatik socket-only fallback.
2. Scope: Tam motor + sınırlı UI iyileştirme.
3. Live mode opsiyonel toggle olarak eklenecek.
4. Mevcut Türkçe etiketler aynen korunur; yeni eklenecek servisler İngilizce isimli ama UI metinleri Türkçe.

---

## Hedef Mimari

`Partials/MainWindow.DeviceScan.cs` → ince UI binding katmanına indirilir (~400 satır). Tüm keşif motoru `Services/Discovery/` altında modüler yapıya taşınır:

```
Services/Discovery/
├── IDeviceDiscoveryEngine.cs        # Orkestratör interface
├── DeviceDiscoveryEngine.cs         # Probe + Listener pipeline orkestratörü
├── DeviceStore.cs                   # ConcurrentDictionary<IPAddress, Device> + change event
├── PcapHelper.cs                    # Npcap availability + adapter selection
├── ScanOptions.cs                   # Hızlı/Derin mod, port set, concurrency, live flag
├── Models/
│   ├── Device.cs                    # Birleştirilmiş cihaz modeli (IPAddress tabanlı)
│   ├── DiscoverySource.cs           # enum + kaynak meta (skor, timestamp)
│   ├── ServiceInfo.cs               # Port, protocol, banner, detail
│   └── ScanProgress.cs              # IProgress<T> raporu
├── Probes/                          # Aktif keşif (gönder + bekle)
│   ├── IProbe.cs
│   ├── ArpProbe.cs                  # YENİ — SharpPcap ile ARP request broadcast
│   ├── NdpProbe.cs                  # YENİ — IPv6 ICMPv6 NS (ff02::1)
│   ├── IcmpProbe.cs                 # Mevcut PingService wrapper
│   ├── TcpPortProbe.cs              # Mevcut PortScanService wrapper, configurable
│   ├── HttpFingerprintProbe.cs      # Mevcut HttpFingerprintService wrapper
│   ├── SnmpProbe.cs                 # Mevcut SnmpFingerprintService wrapper
│   ├── NetbiosProbe.cs              # Mevcut NetbiosService wrapper
│   ├── LlmnrProbe.cs                # YENİ — UDP 5355 multicast
│   ├── SmbProbe.cs                  # YENİ — Port 445 negotiate, OS GUID
│   ├── SshBannerProbe.cs            # YENİ — Port 22 banner (SSH-2.0-...)
│   └── DhcpInformProbe.cs           # YENİ — DHCP INFORM (opsiyonel)
├── Listeners/                       # Pasif arka plan dinleyiciler
│   ├── IListener.cs
│   ├── MdnsListener.cs              # 5353 sürekli dinleme (timeout yok)
│   ├── SsdpListener.cs              # 1900 sürekli dinleme
│   ├── OnvifWsdListener.cs          # 3702
│   ├── MndpListener.cs              # 5678 (mevcut servis dönüştürülür)
│   ├── UbiquitiListener.cs          # 10001 (mevcut servis dönüştürülür)
│   └── PassivePacketSniffer.cs      # YENİ — SharpPcap NIC sniff (ARP/mDNS/NetBIOS/LLMNR)
└── Classification/
    ├── DeviceClassifier.cs          # Mevcut DeviceClassifier taşınır + iyileştirme
    └── ClassifierWeights.cs         # Ağırlık tablosu config'e ayrılır
```

---

## Faz Faz Uygulama

### Faz 0: Hazırlık
- `AgTarama.csproj`'a NuGet ekle:
  - `SharpPcap` (en güncel kararlı sürüm)
  - `PacketDotNet` (en güncel kararlı sürüm)
- `PcapHelper.IsNpcapAvailable()` runtime kontrolü — yoksa motor fallback moduna geçer ve UI'da "Pcap pasif (Npcap yok)" rozeti gösterir.
- **Sadece tarama motorundaki** Advanced IP Scanner kullanımı silinir:
  - `Services/AdvancedIpScannerService.cs` → SİLİNİR (sadece Cihaz Tara'da kullanılıyorsa). Eğer external launcher tuşu bu servisi de kullanıyorsa, servis korunur ama Cihaz Tara'daki çağrılar kaldırılır.
  - `Partials/MainWindow.DeviceScan.cs` içindeki `AdvancedIpScannerService` referansları ve CSV parse kodu silinir.
  - **`tools/Ip_Scanner/` klasörü ve `advanced_ip_scanner_console.exe` dosyası KORUNUR** — uygulamadaki external launcher tuşu hâlâ bunu kullanacak.

### Faz 1: Modeller & DeviceStore
- `Device`: `IPAddress`, `PhysicalAddress?`, `Hostname`, `Vendor`, `DeviceType`, `Brand`, `Model`, `Os`, `OpenPorts` (HashSet<int>), `Services` (List<ServiceInfo>), `DiscoverySources` (HashSet<DiscoverySource>), `FirstSeen`, `LastSeen`, `Online`, `Confidence`, `RttMs`.
- `DeviceStore`: `ConcurrentDictionary<IPAddress, Device>`. `Upsert(Device update)` merge mantığı: tüm setler birleşir, en yeni timestamp wins. `DeviceChanged` event'i UI'a batched update tetikler.
- Mevcut `KameraBilgi` ↔ `Device` arasında **mapper** yazılır; `KameraSatir` (UI binding) aynen kalır.

### Faz 2: Probes (Aktif Keşif)

| Probe | Yöntem | Yeni mi? |
|---|---|---|
| `ArpProbe` | SharpPcap ile her host için ARP request paketi gönder, yanıtları aktif dinle. Subnet sweep tek pass. | **YENİ** |
| `NdpProbe` | ICMPv6 Neighbor Solicitation (ff02::1 all-nodes multicast). Link-local + global IPv6 keşif. | **YENİ** |
| `IcmpProbe` | `PingService` wrapper, concurrency SemaphoreSlim(128). | Mevcut |
| `TcpPortProbe` | `PortScanService` wrapper, port set ayarlanabilir (`ScanOptions.Ports`). Default: 22, 23, 53, 80, 135, 139, 443, 445, 554, 1900, 3389, 5000, 5357, 7547, 8000, 8080, 8443, 9000, 37777. | Mevcut |
| `HttpFingerprintProbe` | `HttpFingerprintService.ProbeAsync()` wrapper | Mevcut |
| `SnmpProbe` | `SnmpFingerprintService.SysDescrAsync()` wrapper | Mevcut |
| `NetbiosProbe` | `NetbiosService.SorgulaAsync()` wrapper | Mevcut |
| `LlmnrProbe` | UDP 5355 multicast (`224.0.0.252` / `ff02::1:3`), Windows hostname sorgusu. | **YENİ** |
| `SmbProbe` | Port 445 SMB negotiate paketi, response'dan computer name + OS GUID parse. | **YENİ** |
| `SshBannerProbe` | Port 22 ilk satırı oku (`SSH-2.0-OpenSSH_8.4 Ubuntu`). | **YENİ** |
| `DhcpInformProbe` | DHCP INFORM gönder, mevcut lease yanıtlarını dinle. | **YENİ (opsiyonel)** |

Her probe `IProbe.RunAsync(IPAddress, CancellationToken)` veya `RunRangeAsync(IEnumerable<IPAddress>, ...)` döner; sonuçları `DeviceStore.Upsert()` ile yazar.

### Faz 3: Listeners (Pasif Keşif)
- Sürekli arka plan dinleyiciler (`BackgroundService` benzeri). Live mode'da hiç durmazlar; tek seferlik taramada belirli süre (örn. 8s) çalışır.
- `PassivePacketSniffer` — SharpPcap üzerinden NIC üzerindeki ARP, mDNS, NetBIOS, LLMNR, gratuitous ARP paketlerini yakalar. Yan trafik yakalama yerine BPF filter ile dar dinleme: `arp or udp port 5353 or udp port 137 or udp port 5355`.
- `MdnsListener`, `SsdpListener`, `OnvifWsdListener`, `MndpListener`, `UbiquitiListener` — mevcut bir-shot servisler `IListener.StartAsync()` / `StopAsync()` model'ine dönüştürülür.

### Faz 4: Orkestratör (DeviceDiscoveryEngine)
- `StartScanAsync(ScanOptions, IProgress<ScanProgress>, CancellationToken)`:
  1. NIC + subnet ön hazırlık (mevcut `SubnetGirdisiniCoz()` korunur).
  2. Listeners background'da başlat.
  3. Probes paralel başlat — `Channel<DeviceUpdate>` üzerinden event-driven. Tüm probe'lar tek master CTS'e bağlı, ayrı timeout zincirleme yok.
  4. İlerleme: `IProgress<ScanProgress>` her 250ms tetiklenir.
  5. Hızlı mod: ARP/NDP + Icmp + TcpPort (default set) + NetBIOS + LLMNR + passive sniff.
  6. Derin mod: + HttpFingerprint + SNMP + SMB + SSH banner + Ubiquiti/MNDP/DHCP.
- `StartLiveAsync(...)` — Live mode için Listeners sürekli çalışır, periyodik ARP/NDP refresh (30s aralıkla). Cihazların `LastSeen` güncellenir, threshold (90s) aşan cihazlar `Online=false` olur ama tablodan düşmez.

### Faz 5: Sınıflandırma İyileştirmesi
- `Partials/MainWindow.DeviceClassifier.cs` → `Services/Discovery/Classification/DeviceClassifier.cs`'e taşı.
- Ağırlık tablosu `ClassifierWeights.cs` static class'a ayrılır.
- **"Weak evidence drop" kaldırılır** — düşük skorlu cihazlar artık tablodan düşmez; sadece güven (`Guven`) sütununda düşük gösterilir ve renk skalası (kırmızı→sarı→yeşil) eklenir.
- Yeni kanıt türleri ağırlık tablosuna eklenir: `LLMNR_Hostname` (15), `SMB_ComputerName` (35), `SSH_Banner` (25), `DhcpHost` (20), `Arp_MacOui_Active` (15 — gerçekten ARP yanıtı geldiyse 5'den 15'e yükseltilir).

### Faz 6: UI Bağlama ve Yeni Sütunlar
- `MainWindow.xaml` (Cihaz Tara sekmesi, L944–L1198) içinde:
  - DataGrid sütunlarına ekle: **OS** (Windows/Linux/RouterOS/iOS/Android), **Son Görülme** (timestamp string), **Yanıt ms**, **Durum** (Online / Offline / Cevapsız) — Tüm Türkçe.
  - Toolbar'a "Sürekli İzle" (`KameraLiveCheck`) CheckBox ekle, "Derin Tara" yanına.
  - Durum şeridi: "Tarama: 87/254 host • 23 cihaz • 12s • 145 paket" şeklinde alt durum çubuğu (`KameraDurumStrip` TextBlock).
- `Partials/MainWindow.DeviceScan.cs` ince binding katmanına indirilir:
  - `KameraTaramaBaslat()` artık `DeviceDiscoveryEngine.StartScanAsync(...)` çağırır.
  - UI thread güvenliği: `Dispatcher.BeginInvoke` ile **batch update** (her 250ms toplu satır işleme).
- `KameraSatir` ve `KameraDataGrid` aynen kalır — sadece yeni alanlar eklenir.
- Tüm export formatları (CSV/JSON/TXT/XLSX/PDF) yeni sütunları da içerir.

### Faz 7: Concurrency, Cancellation, Doğruluk
- **Thread safety**: Tüm cihaz yazma `DeviceStore.Upsert()` üzerinden, ConcurrentDictionary kullanır.
- **CancellationToken**: Tek `_masterCts`. Probe-içi timeout için ayrı `CancellationTokenSource(timeout)` oluşturup `CreateLinkedTokenSource(_masterCts.Token, timeoutCts.Token)` ile birleştir, ASLA `CancelAfter()` zincirleme yapma.
- **MAC normalize**: `MacUtils.Normalize(string)` — `:` / `-` / `.` ayraçları, hex case-insensitive, "0000.0000.0000" Cisco formatı dahil.
- **Gateway/Loopback filtreleme**: Opsiyonel UI toggle ("Yerel makineyi gizle"). 0.0.0.0, 127.x, 169.254.x, multicast/broadcast MAC ignore.
- **OUI MA-M/MA-S**: `OuiVendorLookup` MA-M (28-bit) ve MA-S (36-bit) prefix desteği ekle — IEEE oui.csv'de iki ek kolon var, parse genişletilir.

### Faz 8: Test ve Doğrulama
1. **Build**: `dotnet build` temiz olmalı, warning yok.
2. **Karşılaştırma testi**: Aynı subnet'te eski commit ile yeni motor — yeni motor en az aynı sayıda + ARP-aktif/IPv6/SMB ek cihazlar bulmalı.
3. **Live mode**: Başka cihazı ağa al/çıkar, 30-90s içinde `Online` sütunu güncellenmeli.
4. **CancellationToken**: ⏹ Durdur tuşu 1s içinde tüm görevleri durdurmalı.
5. **Edge cases**: 0.0.0.0, 169.254.x.x, /30 küçük subnet, multicast subnet — crash olmamalı.
6. **Npcap yoksa**: Uygulama açılır, "Pcap pasif" rozeti görünür, fallback motoru çalışır.
7. **Export**: 5 formatın hepsi yeni sütunlarla başarılı çıktı vermeli.
8. **Performance hedefi**: /24 subnet (254 host) — hızlı mod < 30s, derin mod < 90s.
9. **Log**: `%APPDATA%\AgTarama\logs\` altında crash / unhandled exception yok.

---

## Kritik Dosyalar

### Değişecek
- `AgTarama.csproj` — NuGet (SharpPcap, PacketDotNet) ekle.
- `MainWindow.xaml` (L944–L1198) — 4 yeni sütun + Live checkbox + durum şeridi.
- `Partials/MainWindow.DeviceScan.cs` — UI binding katmanına indir (~2340 → ~400 satır).
- `Services/OuiVendorLookup.cs` — MA-M/MA-S desteği.

### Taşınacak
- `Partials/MainWindow.DeviceClassifier.cs` → `Services/Discovery/Classification/DeviceClassifier.cs`.
- `Services/MndpDiscoveryService.cs` → `Services/Discovery/Listeners/MndpListener.cs`.
- `Services/UbiquitiDiscoveryService.cs` → `Services/Discovery/Listeners/UbiquitiListener.cs`.

### Silinecek
- `Services/AdvancedIpScannerService.cs` — yalnızca Cihaz Tara tarafından kullanılıyorsa silinir. Önce bu servise yapılan tüm referanslar grep'lenir; eğer Cihaz Tara dışında bir kullanıcı varsa (örn. external launcher tuşu), servis korunur ve sadece DeviceScan referansları kaldırılır.
- `Partials/MainWindow.DeviceScan.cs` içindeki Advanced IP Scanner çağrısı + CSV parse kodu (tarama akışından çıkarılır).

### KORUNACAK (kullanıcı uyarısı)
- `tools/Ip_Scanner/` klasörü ve içindeki `advanced_ip_scanner_console.exe` + yardımcı dosyalar — Cihaz Tara dışında bir UI tuşundan external olarak açılıyor, bu davranış değişmez.

### Yeni
- `Services/Discovery/` namespace altı tüm dosyalar (yukarıdaki ağaç).

### Reuse (mevcut, aynen tutulacak)
- `Services/HttpFingerprintService.cs` — `HttpFingerprintProbe` içinden çağrılır.
- `Services/OuiVendorLookup.cs` — `Bul()` ve `BulDetay()` API'leri.
- `Services/PingService.cs` — `IcmpProbe` içinde.
- `Services/PortScanService.cs` — `TcpPortProbe` içinde.
- `Services/NetbiosService.cs` — `NetbiosProbe` içinde.
- `Services/SnmpFingerprintService.cs` — `SnmpProbe` içinde.
- `LogService.cs` — log yazma.

---

## Doğrulama Adımları (End-to-End)

```powershell
# 1. Build
dotnet build

# 2. Uygulamayı başlat
dotnet run

# 3. Manuel test akışı:
#    - Cihaz Tara sekmesine git
#    - NIC chip seç (otomatik gelmiyorsa "⟳ NIC Yenile")
#    - "Derin Tara" işaretle
#    - "Tara" tuşuna bas
#    - Durum şeridi ilerlemeli, cihazlar tabloya akmalı
#    - Tarama bitince > 5 cihaz görmeli (ev ağı için tipik)
#    - "Sürekli İzle" işaretle, başka bir cihazı ağa al
#    - 30-90s içinde yeni cihaz tabloya gelmeli, "Son Görülme" güncellenmeli
#    - Bir cihazı ağdan çıkar, 90s sonra "Durum" sütunu "Cevapsız" olmalı

# 4. Export testi: tüm 5 format
#    - sağ tık > Excel/PDF/TXT/CSV/JSON
#    - Çıktılarda yeni sütunlar (OS, Son Görülme, Yanıt ms, Durum) görünmeli

# 5. İptal testi:
#    - Tarama başlat, 5s içinde ⏹ Durdur
#    - 1s içinde durum şeridi "Durduruldu" olmalı, UI donmamalı

# 6. Npcap yoksa testi:
#    - Geçici olarak SharpPcap exception olacak şekilde test (mock veya disable)
#    - UI'da "Pcap pasif" rozeti görünmeli, motor socket-only modda çalışmalı

# 7. Log:
#    - %APPDATA%\AgTarama\logs\yyyymmdd.log
#    - Hata / unhandled exception olmamalı
```

---

## Risk ve Azaltma

| Risk | Azaltma |
|---|---|
| SharpPcap sürüm uyumsuzluğu | En güncel kararlı sürüm + Npcap 1.7+ test; csproj'da kesin sürüm pin'le |
| Pcap yokken motor crash | `PcapHelper.IsNpcapAvailable()` runtime kontrol + try/catch zarfı; fallback socket-only mod |
| Concurrency yüksek → soket limit | Probe başına SemaphoreSlim, toplam aktif soket < 512 |
| UI donma | `Dispatcher.BeginInvoke` batch (250ms), tek cihaz update etme |
| Mevcut tarama davranışlarının kullanıcı için "kaybolması" | UI sütun isimleri/sırası, context menu, export aynen korunur; sadece eklenti yapılır |
| Build kırılması | İlk adım: mevcut DeviceScan.cs'i bozmadan yeni `Services/Discovery/` paralel yaz. Tüm hazırken DeviceScan.cs ince binding katmanına indir |

---

## Uygulama Sırası (Tavsiye)

1. Faz 0 (NuGet + AdvancedIpScanner silme) — 30 dk
2. Faz 1 (Modeller + DeviceStore) — 1-2 saat
3. Faz 2 (Probes: önce mevcut servis wrapper'ları, sonra YENİ'ler: ARP → NDP → LLMNR → SMB → SSH → DHCP) — 4-6 saat
4. Faz 3 (Listeners) — 2-3 saat
5. Faz 4 (Orkestratör) — 2 saat
6. Faz 5 (Sınıflandırma taşı + iyileştir) — 1 saat
7. Faz 6 (UI bağlama + yeni sütunlar) — 2-3 saat
8. Faz 7 (Concurrency/Cancellation/MAC normalize) — 1 saat
9. Faz 8 (Test + doğrulama) — 1-2 saat

**Tahmini toplam: 15-20 saat efektif kod yazımı.**
