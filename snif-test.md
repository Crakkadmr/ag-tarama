# Cihaz Tara — Kapsamlı Bug Testi ve İyileştirme Planı

## Context

Ekran görüntüsünde Cihaz Tara sekmesinde `192.168.1.0/24` taraması sonrası **254 IP'nin tamamı** "Online" olarak listeleniyor; hepsi şu tutarsız ortak özelliğe sahip:

- MAC = `00:00:00:00:00:00`
- Marka = **Xerox**
- Tür = **Cihaz** (generic)
- Keşif kaynağı = **ARP**
- Güven = **13** (her satırda aynı)
- Son Görülme = aynı saat (`09:08:31`)

Sadece `192.168.1.2 (DESKTOP-KB5G4DN)` gerçek verilerle dolu (MAC `30:C5:99:B2:4A:AE`, mDNS/NetBIOS/Pin keşfi, güven 47). Bu, ARP probe'unun **subnet'in tamamını "ghost cihaz" olarak** envantere yazdığını ve OUI lookup'ın `00:00:00` prefix'i için Xerox dönmesini gösteriyor (IEEE OUI veritabanında `00-00-00` Xerox Corporation'a tahsisli).

Refactor çatısı `cihaz-tara-refactor.md` ile başlatılmış (`Services/Discovery/` yeni); bu dosya **bug envanteri + acil düzeltme planı**dır. Refactor planı mimari kapsamı, bu plan koddaki somut hataları ve hızlı kazanımları kapsar.

---

## 1. Kök Sebep: Ghost ARP Entry Kirliliği

### 1.1 Bug — Pcap yokken `arp -a` fallback'i tüm IP'leri yutuyor

**Dosya:** [Services/Discovery/Probes/ArpProbe.cs:131-162](Services/Discovery/Probes/ArpProbe.cs)

Windows `arp -a` çıktısı, başarısız bir ARP sweep'inden sonra cevap vermeyen her IP için aşağıdaki formatta **"invalid"** kayıt üretir:

```
  192.168.1.95         00-00-00-00-00-00     invalid
```

`RunWithArpCacheAsync` satır 149-159'daki regex:

```csharp
@"(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9A-Fa-f]{2}(?:[-:][0-9A-Fa-f]{2}){5})"
```

`00-00-00-00-00-00` MAC'i de yakalar; **"invalid" / "incomplete" / "type" sütunu kontrol edilmiyor**, geçersiz MAC filtresi yok. Bu yüzden `store.GetOrAdd(ip)` ile her IP envantere giriyor, MAC = `00:00:00:00:00:00` set ediliyor, `KesifKaynaklari.Add("ARP")` ekleniyor.

**Etki:** Subnet'teki her IP için "ARP ile bulundu" sahte kanıtı üretiliyor. Sonra OUI lookup `00:00:00` → "Xerox" döner ve `OnvifBulundu=false`, `AcikPortlar=[]`, hiçbir gerçek probe yanıtı olmadan satır "Online" görünüyor.

### 1.2 Bug — Pcap yolunda da koruma yok

[Services/Discovery/Probes/ArpProbe.cs:97-106](Services/Discovery/Probes/ArpProbe.cs#L97) (TryRunWithPcapAsync):

```csharp
foreach (var (ipStr, mac) in replies)
{
    var bilgi = store.GetOrAdd(ipStr);
    bilgi.MacAdresi = MacUtils.Normalize(mac.ToString());
    bilgi.KesifKaynaklari.Add("ARP");
    store.NotifyChanged(bilgi);
}
```

Replies dictionary teorik olarak sadece geçerli ARP **Response**'larını içermeli; ama promiscuous mode'da NIC'e gelen herhangi bir bozuk veya gratuitous ARP de `PacketDotNet.ArpOperation.Response` ile gelirse aynı yola düşer. **MAC validation katmanı yok.**

### 1.3 Bug — `MacUtils.Normalize` zero/broadcast/multicast MAC'leri filtrelemiyor

**Dosya:** [Services/MacUtils.cs:9-26](Services/MacUtils.cs)

12 hex karakter olan her şeyi geçerli sayıyor:
- `00:00:00:00:00:00` (all-zero) → geçer
- `FF:FF:FF:FF:FF:FF` (broadcast) → geçer
- `01:xx:...` (multicast bit = 1) → geçer
- `33:33:xx:xx:xx:xx` (IPv6 multicast) → geçer

### 1.4 Bug — `OuiVendorLookup` rezerve OUI'leri filtrelemiyor

**Dosya:** [Services/OuiVendorLookup.cs:142-149](Services/OuiVendorLookup.cs)

`Bul()` herhangi bir prefix için CSV veya Fallback dictionary'den vendor döner. `00:00:00`, `FF:FF:FF`, `01:00:5E` (IPv4 multicast), `33:33:xx` (IPv6 multicast), `01:80:C2` (LLDP/STP) gibi rezerve prefix'ler için **bilinçli "ignore" listesi yok**.

### 1.5 Bug — `DeviceInfo.Online = true` default

**Dosya:** [Services/Discovery/Models/DeviceInfo.cs:61](Services/Discovery/Models/DeviceInfo.cs)

```csharp
public bool Online { get; set; } = true;
```

Yeni eklenen her ghost cihaz "Online" doğar. KameraSatirOlustur (DeviceScan.cs:1183) sadece bu flag'e bakıyor:

```csharp
var durum = bilgi.Online ? "Online" : "Offline";
```

**Online**, somut kanıt (ping cevabı, ARP yanıtı, TCP port açık, listener paketi) olduğunda set edilmeli; default `false` olmalı.

---

## 2. Diğer Tespit Edilen Buglar

### 2.1 ScanProgress paket sayacı her zaman 0
[DeviceDiscoveryEngine.cs:57-62](Services/Discovery/DeviceDiscoveryEngine.cs#L57): `paket` değişkeni hiç güncellenmiyor; durum şeridindeki "X paket" sayacı ölü.

### 2.2 Yeni tarama eski cihazları temizlemiyor (UI tarafında)
[MainWindow.DeviceScan.cs:953](Partials/MainWindow.DeviceScan.cs#L953): `_kameraSatirlari.Clear()` çağrılıyor ama **`_engine.Store.Clear()`** yalnızca `StartScanAsync` içinde (engine'in metodunun ilk satırı). `StartLiveAsync` Store'u temizlemiyor — bir Live tarama ardından normal tarama yapılırsa eski "ghost" cihazlar Store'da kalır.

### 2.3 Listener CancellationToken zincirleme bug'ı
[DeviceDiscoveryEngine.cs:70-72](Services/Discovery/DeviceDiscoveryEngine.cs#L70):

```csharp
using var listenerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
listenerCts.CancelAfter(options.ListenerDurationMs);
```

`using` deklarasyonu **foreach scope'unun sonunda** dispose oluyor. Çoklu subnet'te ilk subnet'in listener'ları bitmeden ikinci subnet'e geçildiği için CTS dispose edilince listener'lar `ObjectDisposedException` patlatabilir. Çözüm: `await Task.WhenAll(listenerTasks)` blok dışına alınmalı veya listener'lar her subnet için kümülatif bir liste'de tutulup tarama sonunda iptal edilmeli.

### 2.4 Refactor planının "weak evidence drop kaldırılır" hedefi UI'da uygulanmamış
Şu an `KameraKartEkleVeyaGuncelle` her `DeviceChanged` event'inde satır ekliyor; düşük güvenli ghost'lar UI'a doğrudan yazılıyor. **Min güven eşiği uygulanmıyor** (`MinKararEsigi = 12` yalnızca Tur seçiminde kullanılıyor, satır filtrelemesinde değil).

### 2.5 Çift event handler kaydı riski
[MainWindow.DeviceScan.cs:979,1044](Partials/MainWindow.DeviceScan.cs#L979): Kullanıcı tarama bitmeden tekrar Tara'ya basarsa `_engine.Store.DeviceChanged += OnEngineDeviceChanged` iki kez kaydolur — UI satırları iki kez güncellenir. `KameraBaslatBtn.IsEnabled = false` koruma var ama Live mode'da garantili değil.

### 2.6 Engine static probe array'leri reuse ediyor
[DeviceDiscoveryEngine.cs:18-26](Services/Discovery/DeviceDiscoveryEngine.cs#L18): `FastProbes` ve `DeepProbes` `static readonly`. Probe'ların hepsi stateless gibi yazılmış ama herhangi bir probe instance state (örn. cache) tutmaya başlarsa bu pattern thread-safety sorunu doğurur. Güvenlik için **per-scan instantiate** edilmeli.

### 2.7 `bilgi.Online = true` ataması hiçbir probe'da set edilmiyor
Tüm probe'lar `KesifKaynaklari.Add(...)` yapıyor ama `bilgi.Online = true` yok. Bug çoktan kapsamda: default true olduğu için kimse fark etmiyor — ama 1.5 düzeltildikten sonra `Online` flag'i her probe'da somut yanıt üzerine set edilmeli.

### 2.8 `PassivePacketSniffer` BPF filtresi muhtemelen eksik
Refactor planı `arp or udp port 5353 or udp port 137 or udp port 5355` BPF filtresi diyor; dosyayı aç ve doğrula. Filtre yoksa promiscuous mode CPU yakacak ve `DeviceStore` her L2 broadcast için satır üretebilir.

### 2.9 NDP IPv6 keşfi sonuçları aynı `DeviceStore<string ip>` ile çelişiyor
[DeviceStore.cs:11](Services/Discovery/DeviceStore.cs#L11): Key `string`, IP normalize edilmiyor. `192.168.1.10` ve `192.168.001.010` veya IPv6 `fe80::1` aynı cihazın iki kaydı olabilir. Anahtar `IPAddress` olmalı veya normalize edilmiş string.

### 2.10 ARP probe sonrası OUI lookup geç yapılıyor
[DeviceDiscoveryEngine.cs:99-103](Services/Discovery/DeviceDiscoveryEngine.cs#L99): `Uretici` tarama sonunda toplu set ediliyor. Tarama sırasında UI'a düşen satırların `Uretici` boş — ama `KameraSatirOlustur` `OuiVendorLookup.BulDetay` çağrısı **DeviceClassifier** içinde yapılıyor (line 389), yani sınıflandırma sırasında zaten çekiliyor. **Çift OUI lookup** (DeviceClassifier + DeviceDiscoveryEngine tarama sonu) — performans değil, tutarsızlık riski (biri Bul, diğeri BulDetay).

### 2.11 `arp -a` tek bir aile için çalışır
Windows'ta `arp -a` yalnızca **aktif default interface** üzerindekileri döner; tarama çoklu NIC'de yapılıyorsa yanlış arayüzdeki cache okunabilir. `arp -a -N <localIp>` ile NIC pin'lenebilir.

### 2.12 `OuiVendorLookup.Normalize` MacUtils ile tutarsız
İki ayrı `Normalize` var: `Services/MacUtils.cs` (public) ve `Services/OuiVendorLookup.cs:209` (private). İkincisi sadece `:` prefix döner; `MacUtils.Normalize` 12-hex normalize yapar. **DRY ihlali + farklı behavior**: `MacUtils.OuiPrefix()` zaten var ve doğru olan budur; `OuiVendorLookup.Normalize` silinip MacUtils kullanılmalı.

### 2.13 Concurrency: `bilgi.MacAdresi = ...` atomik değil
`DeviceInfo` POCO, hiçbir property lock altında değil. İki probe (ARP + Pcap sniffer) aynı IP'ye eş zamanlı yazarsa **race condition** olur; refactor planı bunu `DeviceStore.Upsert(merge)` ile çözmeyi planlıyor ama mevcut kodda yok.

### 2.14 `KameraKartEkleVeyaGuncelle` her `DeviceChanged`'de filter refresh ediyor
[MainWindow.DeviceScan.cs:1163](Partials/MainWindow.DeviceScan.cs#L1163): `KameraFiltreleriUygula()` her satır için çağrılıyor. 254 cihazda **254 × CollectionView refresh** = UI thread blocked. Refactor planı 250ms batch update öneriyor — uygulanmamış.

### 2.15 `OnEngineDeviceChanged` Dispatcher.BeginInvoke fire-and-forget
[MainWindow.DeviceScan.cs:1051-1052](Partials/MainWindow.DeviceScan.cs#L1051): Yüksek frekanslı `DeviceChanged` event'leri için Dispatcher kuyruğunu boğabilir. Throttle/coalesce yok.

### 2.16 `Online` "false" olunca tablodan düşmüyor — beklenen davranış mı?
Refactor planı "düşürme" demiyor; sadece "Durum=Cevapsız" set ediyor. Şu an Durum yalnızca "Online"/"Offline". `Cevapsız` (LastSeen > 90s) ayrı bir durum olmalı.

### 2.17 Tarama "Tamamlandı" mesajından sonra Store hâlâ canlı
[MainWindow.DeviceScan.cs:1044](Partials/MainWindow.DeviceScan.cs#L1044): `DeviceChanged -= ...` yapılıyor ama Store içeriği siliniyor değil — tekrar Tara'ya basınca `Clear()` çağrılıyor; arada bir filtreleme/export sırasında race olmaz çünkü `Store.All` snapshot list döner. ✓ (sorun yok, doğrulama)

### 2.18 Cancellation: ⏹ Durdur 1s içinde yanıt vermiyor olabilir
Probe'ların hiçbirinde `token` argümanı tek tip alt-timeout ile birleştirilmiyor: `IcmpProbe` sadece `options.PingTimeoutMs` Ping'e veriyor, master token semaphore beklerken iptal olur. Ama `TryRunWithPcapAsync` (ArpProbe.cs:82) `Task.Delay(wait, token)` doğru. `RunWithArpCacheAsync` `proc.WaitForExitAsync(token)` doğru. Genel olarak OK, ama **listener'lar** uzun delay'ler kullanıyor olabilir; ayrı bug testi gerekir.

---

## 3. Geliştirilebilecek Alanlar (Refactor Planını Tamamlayan)

| Alan | Mevcut Durum | Öneri |
|---|---|---|
| **MAC sanity layer** | Yok | `MacUtils.IsValidUnicast(mac)` ekle; zero/broadcast/multicast/locally-administered first-octet reddet. Tüm probe'lar Upsert öncesi bu kontrolden geçirsin. |
| **OUI reserved blacklist** | Yok | `OuiVendorLookup.Bul` `00:00:00`, `FF:FF:FF`, `01:00:5E`, `33:33:xx`, `01:80:C2` için null dönsün. |
| **Online derivation** | `bool Online` flag, default true | Online = (PingYanit ∨ AcikPortlar.Any ∨ ARPactiveReply ∨ ListenerEvidence). Default false. |
| **Confidence threshold UI gizleme** | Yok | Toolbar'a "Düşük güven göster" toggle ekle, default OFF. `MinKararEsigi=12` altında satırlar gizli kalsın ama sayaç görsün. |
| **Renk kodlu güven sütunu** | Yok | DataGrid sütununda 0-39 kırmızı, 40-69 sarı, 70+ yeşil. Refactor planında zaten var, henüz uygulanmamış. |
| **Decision trace tooltip** | `KararIzi` var ama UI'da yok | Güven sütunu hover → `KararIziOzetle` çıktısı tooltip olarak. |
| **Subnet boyut uyarısı** | Yok | `/16` (65k host) seçimi yapılırsa onay sor; default 254 host'tan büyük subnet'lerde "Bu büyük bir subnet, devam edelim mi?" diyaloğu. |
| **NIC seçimi explicit** | Otomatik default route | Toolbar NIC chip picker (refactor planında var — eklenmemiş). |
| **Live mode CTS guard** | `_kameraCts` paylaşılıyor | Live mode kapanırken Store korunsun, normal taramaya geçince sadece UI temizlensin (Store değil). Veya tam tersi: Live mode kapanışı Store'u da temizlesin. Tutarsızlık var. |
| **Probe paralellik tuning** | Sabit `SemaphoreSlim(128)` | ScanOptions.ConcurrencyLimit zaten var; UI'da görünür değil. Ayarlar > Cihaz Tara altına eklenebilir. |
| **PassiveSniffer auto-fallback uyarısı** | Yok | Npcap yoksa toolbar'a "Pcap pasif" rozeti — refactor planında var, eklenmemiş. |
| **OUI veritabanı tazeliği** | Çift kaynak (fallback dict + oui.csv) | Tek truth-source; oui.csv yüklendiyse fallback dict ignore. Çakışmaları log'a yaz. |
| **Test coverage** | 0 | En az `MacUtils.Normalize`, `OuiVendorLookup.Bul` ve `DeviceClassifier.KimlikBelirleV2` için unit test ekle (xUnit). |
| **Export sütunları** | OS/SonGörülen/Durum/PingMs UI'da var | CSV/JSON/TXT/XLSX/PDF export bunları içeriyor mu doğrula. |

---

## 4. Acil Düzeltme Sırası (Bug Fix Pipeline)

### P0 — Ghost cihazları durdur
1. **`Services/MacUtils.cs`** → `IsValidUnicast(string mac)` ekle. Zero/broadcast/multicast/`00:00:5E` (VRRP) reddet.
2. **`Services/Discovery/Probes/ArpProbe.cs`** → Hem `TryRunWithPcapAsync` (97-105) hem `RunWithArpCacheAsync` (149-159) içinde `IsValidUnicast` filtresi.
3. **`Services/OuiVendorLookup.cs`** → `Bul()` başında reserved prefix blacklist; null döndür.
4. **`Services/Discovery/Models/DeviceInfo.cs`** → `Online` default `false`.
5. **Tüm Probe'lar** → Gerçek kanıt geldiğinde `bilgi.Online = true` set et (Icmp ✓, Tcp ✓, Arp ✓, listener'lar).

### P1 — Doğruluk ve UX
6. **`DeviceDiscoveryEngine.cs`** → Listener CTS scope düzelt; Store.Clear `StartLiveAsync` başında da çağır.
7. **`MainWindow.DeviceScan.cs`** → `KameraKartEkleVeyaGuncelle` içinde `KameraFiltreleriUygula()` 250ms throttle (Timer + dirty flag).
8. **`MainWindow.xaml`** → Toolbar'a "Düşük güveni göster" CheckBox; default OFF. `KameraSatirFiltredenGecer` içinde `satir.Guven >= 12` koşulu (toggle açıkken atla).
9. **`MainWindow.xaml`** → Güven sütunu cell template'i renk kodlu (DataTrigger).
10. **`MainWindow.xaml`** → Tooltip'te `KararIzi`.

### P2 — Mimari
11. **`OuiVendorLookup.Normalize`** sil → `MacUtils.OuiPrefix` kullan.
12. **`DeviceStore`** key normalize (`IPAddress` veya `IPAddress.Parse(ip).ToString()`).
13. **Probe array'leri** static'ten instance'a çevir (per-scan).
14. **Unit tests** — `Tests/AgTarama.Tests.csproj` projesi oluştur (xUnit), kritik 3 service'i kapsa.

---

## 5. Kritik Dosyalar

| Dosya | Rol |
|---|---|
| [Services/Discovery/Probes/ArpProbe.cs](Services/Discovery/Probes/ArpProbe.cs) | Ghost ARP entry kaynağı (P0) |
| [Services/MacUtils.cs](Services/MacUtils.cs) | MAC validation eklenecek (P0) |
| [Services/OuiVendorLookup.cs](Services/OuiVendorLookup.cs) | Reserved OUI blacklist (P0) |
| [Services/Discovery/Models/DeviceInfo.cs](Services/Discovery/Models/DeviceInfo.cs) | `Online = false` default (P0) |
| [Services/Discovery/DeviceDiscoveryEngine.cs](Services/Discovery/DeviceDiscoveryEngine.cs) | CTS scope + Store.Clear + per-scan probe instance (P1-P2) |
| [Services/Discovery/Probes/IcmpProbe.cs](Services/Discovery/Probes/IcmpProbe.cs) | `Online=true` set (P0) |
| [Services/Discovery/Probes/TcpPortProbe.cs](Services/Discovery/Probes/TcpPortProbe.cs) | `Online=true` set (P0) |
| [Partials/MainWindow.DeviceScan.cs](Partials/MainWindow.DeviceScan.cs) | UI throttle + filter (P1) |
| [Partials/MainWindow.DeviceClassifier.cs](Partials/MainWindow.DeviceClassifier.cs) | (değişiklik yok, sınıflandırma sağlam) |
| [MainWindow.xaml](MainWindow.xaml) | "Düşük güveni göster" toggle + renk kodlu Güven sütunu + tooltip (P1) |

---

## 6. Doğrulama (End-to-End)

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build
dotnet run
```

**P0 fix sonrası beklenen davranış:**

1. `192.168.1.0/24` taraması başlat → tabloya **yalnız gerçekten yanıt veren** cihazlar düşer (tipik ev ağı: 5-20 cihaz).
2. Hiçbir satırda MAC = `00:00:00:00:00:00` görünmemeli; bu MAC'ler Store'a girmemeli.
3. Hiçbir satırda Marka = "Xerox" görünmemeli (gerçek bir Xerox yazıcı varsa istisna).
4. "Online" sütunu yalnızca somut kanıtı olan cihazlarda "Online"; diğerleri "Offline" veya tablo dışı.
5. Güven sütunu 12'nin altındaki satırlar gizli (toggle OFF iken); toggle ON ise gri renkte gözüksün.
6. ⏹ Durdur 1 saniye içinde "Durduruldu" mesajı vermeli.
7. Live mode'a geç → cihaz ağa al/çıkar → "Son Görülme" + "Durum" değişmeli.
8. `arp -a` cache'i kirliyse (manuel olarak `arp -d *` yapıp tekrar dene) tablo yine temiz olmalı.
9. Npcap'i kapatıp (geçici test) tarama → fallback path da P0 düzeltmesinden sonra ghost üretmemeli.
10. Export (CSV/JSON/XLSX/PDF/TXT) tüm yeni sütunları (OS, Son Görülme, Durum, Yanıt ms) içermeli.

**Performans:**
- `/24` hızlı mod < 30s, derin mod < 90s
- UI thread tarama sırasında donmamalı (DataGrid scroll akıcı)
- 250ms batch update sonrası `KameraFiltreleriUygula` saniyede 4 kez tetiklenmeli, 254 kez değil

**Log:**
- `%APPDATA%\AgTarama\logs\<tarih>.log` içinde unhandled exception olmamalı.

---

## 7. Refactor Planıyla İlişki

[cihaz-tara-refactor.md](cihaz-tara-refactor.md) mimari yeniden yazımdır (15-20 saat efor, Faz 0-8). Bu plan onun **acil bug fix kompleman**ıdır: P0 düzeltmeleri refactor'dan **bağımsız** uygulanabilir (her birinin scope'u küçük, 2-4 saatlik iş), kullanıcıya hemen çalışan bir Cihaz Tara verir. P1-P2 düzeltmeleri refactor ile çakışıyor; refactor başlarsa o adımlar refactor faz 5-7 içinde yapılır.

**Öneri:** Önce P0 (1-2 saat), sonra refactor başlasın. P1-P2 refactor içinde absorb edilsin.
