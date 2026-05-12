# codex-eklenebilecekler.md - Guncel Ozellik Adaylari

> Hazirlayan: Codex
> Tarih: 2026-05-12
> Durum: Mevcut uygulama; paket yakalama, Ping, Port Tara, Traceroute, DNS, ARP+OUI, Ag Bilgisi, Wake-on-LAN, Cihaz Tara, ONVIF, SSDP, Ping Sweep, Favoriler, Bant Genisligi ve Ayarlar ozelliklerini iceriyor.

Bu dosya, mevcut `EKLENECEKLER.md` dosyasindaki tamamlanmis maddeleri tekrar etmeyen, uygulamanin bugunku haline gore en mantikli yeni gelistirme adaylarini listeler.

---

## 1. En Mantikli Siradaki Isler

### 1.1 HTTP Header Checker

**Oncelik:** Yuksek  
**Zorluk:** Kolay  
**Tahmini dokunulacak yerler:** `MainWindow.xaml`, `MainWindow.xaml.cs`, opsiyonel `Services/HttpHeaderService.cs`

URL veya IP girilince HTTP/HTTPS basliklarini gosteren bir yan panel.

Gosterilecek bilgiler:

- Status code
- Server
- Date
- Content-Type
- HSTS
- CSP
- X-Frame-Options
- X-Content-Type-Options
- Redirect zinciri
- Sertifika ozeti, HTTPS ise

**Neden iyi fikir:** Cihaz Tara zaten HTTP banner ve title okuyor. Bunu tek basina bir araca cevirmek kolay ve sahada cok is gorur.

---

### 1.2 Katmanli Cihaz Adi / Model Bulma - Tamamlandi

**Oncelik:** Yuksek  
**Zorluk:** Kolay-Orta  
**Uygulanan yerler:** `MainWindow.xaml.cs`, `Services/NetbiosService.cs`

Windows cihazlar icin UDP 137 NetBIOS Node Status, reverse DNS, `ping -a` ve `nbtstat -A <ip>` birlikte denenir. Kamera/router/NAS gibi cihazlarda ONVIF scope ve SSDP/UPnP cihaz aciklama XML'i de ad/model bulmaya katilir.
Advanced IP Scanner console, ARP/MAC/OUI ve servis banner bilgileri de Cihaz Tara kartlarini zenginlestirmek icin kullanilir.

Uygulanan kullanim yerleri:

- Cihaz Tara kartlarina "Cihaz Adi" satiri
- Cihaz Tara kartlarina "Grup" satiri
- Cihaz Tara kartlarina "Marka", "Model", "Konum" satirlari
- Ping yaniti veren veya 139/445/3389 portu acik gorunen IP'lerde otomatik sorgu
- Tum subnet icin UDP 137 NetBIOS sweep; ping kapali Windows cihazlari da adla yakalanabilir
- ONVIF `name/hardware/location` okuma
- SSDP `friendlyName/manufacturer/modelName/modelNumber` okuma
- Advanced IP Scanner console sonucu ile IP/ad/MAC/servis zenginlestirme
- ARP tablosundan MAC, `mac_interval_tree.txt` ile uretici bulma
- Acik portlar icin servis adi ve kisa banner gosterimi

**Not:** Favorilerde IP yanina isim gosterimi henuz eklenmedi; ayri bir UX isi olarak kalabilir.

---

### 1.3 Sonuclari JSON / CSV Disari Aktarma

**Oncelik:** Yuksek  
**Zorluk:** Kolay  
**Tahmini dokunulacak yerler:** `MainWindow.xaml`, `MainWindow.xaml.cs`, opsiyonel `Services/ExportService.cs`

Mevcut `Rapor Kaydet` duz metin uretiyor. Cihaz Tara, Port Tara ve ARP sonuclari yapilandirilmis olarak disari aktarilabilir.

Formatlar:

- CSV: Excel icin
- JSON: baska araclara aktarim icin
- TXT: mevcut davranis korunur

**Neden iyi fikir:** Uygulama sadece bakma araci olmaktan cikip rapor ureten saha aracina donusur.

---

### 1.4 Port Banner / Servis Surum Tespiti

**Oncelik:** Yuksek  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** `Services/PortScanService.cs`, `MainWindow.xaml.cs`

Acik porta gore kisa banner denemesi yapilir.

Ornekler:

- `22`: SSH greeting
- `21`: FTP greeting
- `25`: SMTP greeting
- `80/8080/443/8443`: HTTP header/title
- `554`: RTSP DESCRIBE
- `23`: Telnet greeting

**Neden iyi fikir:** "Port 22 acik" yerine "OpenSSH 8.9" gormek cok daha degerli.

---

### 1.5 mDNS / Bonjour Kesif

**Oncelik:** Orta-Yuksek  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** `MainWindow.xaml.cs`, opsiyonel `Services/MdnsDiscoveryService.cs`

`224.0.0.251:5353` uzerinden mDNS sorgulari ile Apple, yazici, Chromecast, akilli TV ve IoT cihazlari bulunabilir.

Sorgu adaylari:

- `_http._tcp.local`
- `_printer._tcp.local`
- `_googlecast._tcp.local`
- `_airplay._tcp.local`
- `_workstation._tcp.local`

**Neden iyi fikir:** Cihaz Tara'nin SSDP/ONVIF tarafini tamamlar. Ozellikle ev/ofis aglarinda cok cihaz mDNS ile kendini duyurur.

---

## 2. Guvenlik Odakli Eklemeler

### 2.1 ARP Spoof / Gateway MAC Degisimi Alarmi

**Oncelik:** Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** `MainWindow.xaml`, `MainWindow.xaml.cs`, opsiyonel `Services/ArpWatchService.cs`

Belirli aralikla ARP tablosu izlenir. Gateway IP'sinin MAC adresi degisirse kirmizi uyari ve log kaydi uretilir.

Ek fikirler:

- "Ag Izle" paneli
- Gateway MAC sabitleme/favoriye alma
- Ayni MAC'in birden fazla IP'de gorunmesi uyarisi

**Neden iyi fikir:** Basit ama gercek guvenlik degeri var. MITM belirtilerini erken yakalar.

---

### 2.2 Rogue DHCP Sunucu Tespiti

**Oncelik:** Orta  
**Zorluk:** Orta-Zor  
**Tahmini dokunulacak yerler:** `MainWindow.xaml.cs`, `CaptureService` veya yeni servis

Kisa sureli DHCP filtreli yakalama yaparak agda birden fazla DHCP Offer/Ack veren sunucu var mi bakilir.

Yaklasim:

- `tshark` ile `bootp` filtresi
- DHCP server IP/MAC listeleme
- Birden fazla server varsa uyari

**Neden iyi fikir:** Kurumsal aglarda yanlis takilmis modem/router gibi problemleri hizli yakalar.

---

### 2.3 HTTPS Sertifika Denetleyicisi

**Oncelik:** Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** yeni `Services/CertificateService.cs`, UI panel

IP veya hostname icin sertifika bilgilerini okur.

Gosterilecek bilgiler:

- Subject / CN
- Issuer
- Baslangic ve bitis tarihi
- Kalan gun
- SAN alanlari
- TLS protokol bilgisi

**Neden iyi fikir:** Kamera, NVR, router ve web sunucularinda suresi gecmis veya yanlis sertifikayi anlamak kolaylasir.

---

### 2.4 Acik Port Degisiklik Izleme

**Oncelik:** Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** `SettingsService`, yeni history modeli, UI panel

Secilen IP'ler periyodik taranir. Onceki sonuca gore yeni acilan veya kapanan portlar raporlanir.

**Neden iyi fikir:** "Dun kapali olan 3389 bugun acilmis" gibi kritik degisiklikler gorunur hale gelir.

---

## 3. Cihaz Tara'yi Guclendirecek Fikirler

### 3.1 Cihaz Kartlarina MAC ve Uretici Ekleme

**Oncelik:** Yuksek  
**Zorluk:** Kolay-Orta  
**Tahmini dokunulacak yerler:** `MainWindow.xaml.cs`

ARP tablosu zaten MAC+OUI biliyor. Cihaz Tara sonucundaki IP'ler icin ARP tablosundan MAC cekilip karta eklenebilir.

**Neden iyi fikir:** Cihaz Tara kartinda IP, port, marka tahmini ve MAC uretici tek yerde olur.

---

### 3.2 OS Tahmini

**Oncelik:** Orta  
**Zorluk:** Kolay  
**Tahmini dokunulacak yerler:** `PingService`, `MainWindow.xaml.cs`

TTL degerinden kaba OS tahmini:

- TTL 64 civari: Linux/Unix/Android
- TTL 128 civari: Windows
- TTL 255 civari: network cihazlari

**Neden iyi fikir:** Kesin bilgi degil ama cihaz turu tahmininde iyi ipucu verir.

---

### 3.3 Kamera Snapshot / RTSP Onizleme

**Oncelik:** Orta  
**Zorluk:** Zor  
**Tahmini dokunulacak yerler:** `tools/ffmpeg`, `MainWindow.xaml`, `MainWindow.xaml.cs`

RTSP URL'den tek kare yakalayip cihaz kartinda thumbnail gosterebilir.

Yaklasim:

- `ffmpeg` portable eklenir
- `rtsp://ip:554/...` adaylari denenir
- Basarili kare `captures/previews/` altina kaydedilir

**Neden iyi fikir:** Kamera var mi, goruntu geliyor mu sorusuna tek tikla cevap verir.

---

### 3.4 ONVIF Profil ve Stream URI Cekme

**Oncelik:** Orta  
**Zorluk:** Zor  
**Tahmini dokunulacak yerler:** yeni ONVIF servis katmani

WS-Discovery sonrasi ONVIF servis URL'sinden profil ve stream URI bilgisi alinabilir.

**Neden iyi fikir:** RTSP yolunu tahmin etmek yerine cihazdan dogrudan almak daha dogru olur.

---

## 4. Kullanici Deneyimi

### 4.1 Chat Icinde Arama ve Filtre

**Oncelik:** Orta  
**Zorluk:** Kolay-Orta  
**Tahmini dokunulacak yerler:** `MainWindow.xaml`, `MainWindow.xaml.cs`

Chat alaninda IP, port veya kelime aramasi. Eslesen kartlar vurgulanir, eslesmeyenler soluklastirilir.

**Neden iyi fikir:** Uzun tarama sonuclarinda belirli IP veya portu bulmak su an yorucu olabilir.

---

### 4.2 Son Taramalar / Gecmis Paneli

**Oncelik:** Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** `Services/HistoryService.cs`, yeni panel

Son ping, port, cihaz ve yakalama islemleri `%APPDATA%\AgTarama\history\` altinda JSON olarak saklanir.

**Neden iyi fikir:** Kullanici "gecen hafta hangi cihazlar vardi?" sorusuna uygulama icinden cevap alir.

---

### 4.3 Islem Kuyrugu ve Durum Merkezi

**Oncelik:** Dusuk-Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** UI ve ortak task modeli

Aktif ping, port tarama, cihaz tarama, yakalama gibi islemleri tek noktada listeleyen kucuk durum merkezi.

**Neden iyi fikir:** Uygulama buyudukce ayni anda ne calisiyor sorusuna netlik getirir.

---

### 4.4 Tray Bildirimi

**Oncelik:** Dusuk-Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** `App.xaml.cs`, `MainWindow.xaml.cs`

Tarama tamamlaninca veya kritik uyari olunca Windows bildirim/tray mesaji gosterilebilir.

**Neden iyi fikir:** Uzun taramalar arkada calisirken kullanici uygulamayi beklemek zorunda kalmaz.

---

## 5. Mimari ve Bakim

### 5.1 `MainWindow.xaml.cs` Dosyasini Partial Parcalara Bolme

**Oncelik:** Yuksek  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** yeni `.cs` dosyalari

Mevcut dosya buyumus durumda. Davranisi degistirmeden partial class dosyalarina bolmek bakimi rahatlatir.

Onerilen ayrim:

- `MainWindow.Capture.cs`
- `MainWindow.Panels.cs`
- `MainWindow.NetworkTools.cs`
- `MainWindow.DeviceScan.cs`
- `MainWindow.Rendering.cs`
- `MainWindow.Helpers.cs`

**Neden iyi fikir:** Yeni ozellik eklemek kolaylasir, merge conflict riski azalir.

---

### 5.2 Cihaz Tara Servis Katmanina Alinsin

**Oncelik:** Yuksek  
**Zorluk:** Orta-Zor  
**Tahmini dokunulacak yerler:** `Services/DeviceDiscoveryService.cs`, modeller

Port tarama, ONVIF, SSDP, Ping Sweep ve kimlik belirleme UI'dan ayrilabilir.

**Neden iyi fikir:** Test yazmak kolaylasir. UI sadece sonucu gosterir, tarama motoru bagimsiz calisir.

---

### 5.3 Ortak Kart/Buton Uretici Yardimcilari

**Oncelik:** Orta  
**Zorluk:** Kolay-Orta  
**Tahmini dokunulacak yerler:** `MainWindow.xaml.cs` veya yeni helper class

Benzer Border, TextBlock, kart satiri ve link uretimleri ortak yardimcilara alinabilir.

**Neden iyi fikir:** UI kartlari cogaldikca tekrar azalir, gorunum tutarliligi korunur.

---

### 5.4 Test Edilebilir Servisler

**Oncelik:** Orta  
**Zorluk:** Orta  
**Tahmini dokunulacak yerler:** yeni test projesi, servis refactorlari

Ozellikle parse fonksiyonlari icin test eklenebilir.

Test adaylari:

- Port araligi parse
- IPv4/hostname dogrulama
- ARP parse
- OUI lookup
- HTTP title parse
- NetBIOS parse

**Neden iyi fikir:** Ag araclarinda parse hatalari cok kolay kaciyor. Kucuk testler buyuk guven verir.

---

## 6. Harici Arac Entegrasyonlari

| Arac | Fikir | Oncelik | Zorluk |
|---|---|---:|---:|
| Nmap Portable | Varsa OS detection ve servis surum taramasi | Orta | Orta |
| FFmpeg Portable | RTSP snapshot / kamera onizleme | Orta | Zor |
| OpenSSL Portable | Sertifika detaylari ve TLS kontrolu | Orta | Orta |
| PsExec / PowerShell Remoting | Windows cihazlarda uzaktan komut | Dusuk | Zor |
| iperf3 | Bant genisligi performans testi | Dusuk-Orta | Orta |

---

## 7. Onerilen Uygulama Sirasi

1. HTTP Header Checker
2. Cihaz Tara kartlarina MAC + OUI ekleme
3. JSON / CSV Export
4. Port banner / servis surum tespiti
5. `MainWindow.xaml.cs` partial dosyalara bolme
6. mDNS kesif
7. ARP spoof / gateway MAC alarmi
8. HTTPS sertifika denetleyicisi
9. Son taramalar / gecmis paneli

Bu siralama, "hizli deger" ile "kod tabanini rahatlatma" arasinda dengeli gider. Ilk bes madde kullaniciya dogrudan gorunen yeni deger uretir; 6. madde sonraki buyumeyi daha rahat hale getirir.
