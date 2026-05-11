# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## ÖNCELİKLE DETAY.md'Yİ OKU

Yeni bir session başladığında **önce `DETAY.md` dosyasını oku.** Bu dosya projenin tüm dosya yapısı, mimarisi, alanları, metodları, UI bileşenleri ve TODO listesini içerir — kaynak dosyaları tek tek taramaya gerek kalmaz.

## DETAY.md OTOMATİK GÜNCELLEME (ZORUNLU)

Aşağıdaki tetikleyicilerden **herhangi biri** olduğunda `DETAY.md` dosyasını **aynı turda** güncellemen gerekir (kullanıcı ayrıca istemese bile):

- `MainWindow.xaml` veya `MainWindow.xaml.cs` üzerinde Edit/Write yapıldığında
- Yeni `.cs` / `.xaml` dosyası eklendiğinde veya silindiğinde
- `AgTarama.csproj` değiştirildiğinde (TargetFramework, paket vs.)
- Yeni klasör/araç eklendiğinde (`tools/`, `Req/` altı dahil)
- Yeni buton, stil veya UI bileşeni eklendiğinde
- Yeni metot/alan/state değişkeni eklendiğinde
- TODO maddesi tamamlandığında veya yeni TODO doğduğunda

Güncelleme yaparken:
- İlgili bölümü (Klasör Yapısı / Mimari / 6.x metot haritası / TODO / Git Durumu) bul ve **yerinde** düzenle.
- Üstteki `Son güncelleme:` tarihini bugünün tarihine çevir.
- Satır numaralarını mümkün olduğunca güncel tut (ana metotların yaklaşık satır aralığı).

## Proje Amacı

WPF tabanlı, **chatbot arayüzlü ağ paket yakalama uygulaması**. Aktif ağ arayüzlerini otomatik tespit eder, seçilen arayüzler üzerinde **tshark** ile paket yakalama gerçekleştirir, sonuçları **Wireshark Portable** ile analiz etmeye hazırlar. Ayrıca ping testi, port tarama, traceroute, DNS lookup, ARP tablosu, ağ adaptörü bilgisi, Wake-on-LAN, SNMP sorgusu ve harici Advanced IP Scanner entegrasyonu mevcut.

## Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build                   # Debug build
dotnet run                     # Çalıştır
dotnet build -c Release        # Release build
```

## Mimari

**Tek pencere — MVVM yok.** UI wiring `MainWindow.xaml` (~458 satır) + `MainWindow.xaml.cs` (~1683 satır) çiftinde. Ağ iş mantığı `Services/` katmanına ayrılmış.

**Katmanlar:**
- `Paths.cs` — tüm exe-relative yol sabitleri (`AppBase`, `TsharkExe`, `SadpExe` vb.)
- `LogService.cs` — `%APPDATA%\AgTarama\logs\YYYYMMDD.log`'a yazar; `OturumBaslat`, `Kaydet`, `Hata` metotları
- `Services/InterfaceDiscoveryService` — `tshark -D` parse + 2s paket sayısı testi
- `Services/CaptureService` — tshark process yönetimi, progress callback (`Action<double, int, TimeSpan>`)
- `Services/PingService` — `IAsyncEnumerable<PingSonuc>` akışı (4 ping, TTL, hata sarmalı)
- `Services/PortScanService` — `Parse(string)` + `TaraAsync(...)` (SemaphoreSlim 50, 1000ms timeout)
- `MainWindow` → `HataBildir(mesaj, ex?)` — chat kırmızı mesaj + `LogService.Hata` tek noktadan

**UI düzeni:**
- Sol (`*` genişlik): Başlık + Chat alanı (`ChatScrollViewer` → `ChatPanel` StackPanel) + animasyonla açılan yan panel (`PingCol` ColumnDefinition, 0 → 340px)
- Sağ (220px sabit): Kontrol butonları StackPanel + alt versiyon yazısı

**Mesaj türleri** (`MesajEkle` metodu):

| Tür | Arka Plan | Kullanım |
|---|---|---|
| `"sistem"` | Gri (#8B949E) | Durum bildirimleri, `◆` prefix |
| `"kullanici"` | #161B22, sağda | Kullanıcı girdisi, `›` prefix |
| `"sonuc"` | Mavi (#0D3B66) | Tarama/sonuç çıktıları |
| `"hata"` | Kırmızı (#3D1A1A) | Hatalar, `✖` prefix |

**Tarama durumu:** `_taramaDevamEdiyor` bool + `_taramaCts` ile izleniyor. `TaramaDurumunuAyarla(bool)` metodu butonları ve `StatusText`'i senkronize ediyor.

**Aktif buton stili:** `SetButonAktif(Button?)` — açık panelin sağ panel butonuna `ActiveActionButton` stili (yeşil çerçeve) atar, kapanınca `ActionButton`'a geri döndürür.

**Yan panel animasyonu:** `YanPanelAcAnimasyon()` / `YanPanelKapatAnimasyon(Action)` — her çağrıda önce `_yanPanelTimer?.Stop()` ile önceki timer durdurulur (çift tık race condition önlenir). Aynı anda yalnızca bir yan panel açık olabilir.

## Kontrol Butonları (sağ panel)

| Buton | Handler | Durum |
|---|---|---|
| Taramayı Başlat | `BtnTaramaBaslat_Click` | ✅ tshark yakalama |
| Taramayı Durdur | `BtnTaramaDurdur_Click` | ✅ CancellationToken iptal |
| Ping Testi | `BtnPing_Click` | ✅ Yan panel |
| Port Tara | `BtnPortTara_Click` | ✅ Yan panel |
| Traceroute | `BtnTrace_Click` | ✅ Yan panel (`tracert -d`) |
| DNS Lookup | `BtnDns_Click` | ✅ Yan panel |
| Cihazları Listele | `BtnCihazlar_Click` | ✅ Advanced IP Scanner |
| ARP Tablosu | `BtnArp_Click` | ✅ `arp -a` → chat kart |
| Ağ Bilgisi | `BtnAgBilgi_Click` | ✅ `NetworkInterface` → chat kartı |
| SADP | `BtnSadp_Click` | ✅ `tools/sadp/sadptool.exe` |
| Wake-on-LAN | `BtnWol_Click` | ✅ Yan panel (UDP magic packet) |
| SNMP Sorgusu | `BtnSnmp_Click` | ✅ Yan panel (SharpSnmpLib GET) |
| Ekranı Temizle | `BtnTemizle_Click` | ✅ Tarama sırasında disabled |

## Tamamlanan TODO Listesi

Tüm orijinal TODO maddeleri tamamlanmıştır:

- ✅ Taramayı Başlat / Durdur — tshark wrapper (`CaptureService`)
- ✅ Ping Testi — `PingService.PingleAsync` IAsyncEnumerable akışı
- ✅ Port Tara — `PortScanService.TaraAsync` + `PortPanel` yan paneli
- ✅ Cihazları Listele — Advanced IP Scanner entegrasyonu
- ✅ Traceroute — `TracerouteBaslat` (`tracert -d`), `TracePanel` yan paneli
- ✅ DNS Lookup — `DnsLookupBaslat` (`Dns.GetHostEntryAsync`), `DnsPanel` yan paneli
- ✅ ARP Tablosu — `ArpTablosuGoster` (`arp -a` parse), chat kart
- ✅ Ağ Adaptörü Bilgisi — `AgAdaptorleriniGoster` (`NetworkInterface`), chat kart
- ✅ Wake-on-LAN — `WolGonder` (UDP magic packet), `WolPanel` yan paneli
- ✅ SNMP Sorgusu — `SnmpSorguBaslat` (SharpSnmpLib, MIB-II sysGroup), `SnmpPanel` yan paneli
- ✅ Otomatik Log — `LogService.Kaydet` → `%APPDATA%\AgTarama\logs\YYYYMMDD.log`
- ✅ Wireshark "Aç" butonu — yakalama tamamlanınca karta dinamik ekleniyor

## Geliştirme Notları

- Tüm ağ işlemleri `async/await` + `CancellationToken` ile yapılmalı; UI thread'i bloke etme.
- Tarama sonuçları `MesajEkle("sonuc", ...)` ile ChatPanel'e yazılır; ping/port sonuçları kendi yan panel kutucuğuna yazılır (ana chat'e değil).
- Yeni buton sağ paneldeki `StackPanel`'e `ActionButton` stiliyle eklenir.
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`'da tanımlanır. `ActiveActionButton`, `ActionButton`'dan **SONRA**; `SelectedChipButton`, `ChipButton`'dan **SONRA** tanımlanmalı (StaticResource forward-ref hatası).
- .NET 10 / WPF — `LetterSpacing` gibi web'e özgü CSS özellikleri WPF XAML'de yok, kullanma.
- SNMP'de `Lextm.SharpSnmpLib.Messaging.TimeoutException` fully-qualified kullanılmalı (`System.TimeoutException` ile çakışma var).
- Harici araç başlatma için `HariciAracBaslat(exe, ad)` ortak metodu kullanılır.
