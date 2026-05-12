# EKLENECEKLER.md — Geliştirme Yol Haritası

> Analiz tarihi: 2026-05-12
> Mevcut durum: v0.1.0, .NET 10 WPF, ~1750 satır MainWindow.xaml.cs

---

## 1. Yüksek Öncelik — Hemen Yapılabilir

### 1.1 Bant Genişliği / Trafik Monitörü
- Ağ adaptörlerinden gerçek zamanlı indirme/yükleme hızı okuma
- `NetworkInterface.GetIPv4Statistics()` ile poll döngüsü
- Chat'te canlı güncellenen sparkline grafik (WPF Path ile çizim)
- **Neden:** En sık sorulan ağ sorusu — "hangisi bant yiyor?"

### 1.2 HTTP/HTTPS Başlık Denetleyicisi (Header Checker)
- URL gir → tam HTTP yanıt başlıklarını göster
- Status code, Server, X-Frame-Options, HSTS, Content-Security-Policy vb.
- Web sunucusu/kamera güvenlik denetimi için doğrudan kullanım
- **Neden:** Cihaz Tara'daki HTTP banner okuma zaten var; bağımsız araç olarak çok değerli

### 1.3 MAC Adresinden Üretici Sorgulama (OUI Lookup)
- IEEE OUI veritabanını yerel dosyaya gömme (oui.txt ~6 MB)
- ARP Tablosu ve Cihaz Tara kartlarında MAC'ın yanında üretici göster
- **Neden:** "AA:BB:CC..." yazmak yerine "Hikvision / TP-Link" görmek çok daha kullanışlı

### 1.4 Ping Sweep (Subnet Ping)
- Tüm subnet'i (örn. 192.168.1.1–254) paralel ping ile tara
- Yanıt verenleri listele, yanıt vermeyenleri atla
- Cihaz Tara'nın hızlı ön taraması olarak da kullanılabilir
- **Neden:** Cihaz Tara port taraması yapıyor ama bazı cihazlar port kapalıyken ICMP'ye yanıt verir

---

## 2. Orta Öncelik — Güçlü Özellikler

### 2.1 Gelişmiş Port Tarama — Servis/Sürüm Tespiti
- Açık porta banner/versiyon isteği gönder (HTTP, FTP, SSH, Telnet)
- `Services/PortScanService`'e `BannerAsync(ip, port)` metodu ekle
- Çıktıda `[AÇIK] 22/ssh — OpenSSH 8.9` formatında servis adı göster
- **Neden:** Şu an sadece "açık/kapalı" görünüyor; hangi yazılımın çalıştığı daha değerli

### 2.2 CVE / Güvenlik Açığı Kontrolü
- Tespit edilen servis + sürüm bilgisiyle yerel CVE JSON veritabanını sorgula
- NVD (NIST) CVE veritabanının küçük bir özetini gömülü tut
- Kamera/router firmware versiyonlarında kritik CVE uyarısı göster
- **Neden:** Güvenlik odaklı kullanım senaryosu; SADP + Cihaz Tara ile sinerji

### 2.3 RTSP Stream Önizleme
- RTSP URL'den ilk frame'i VLC/FFmpeg ile yakala, küçük thumbnail göster
- Kamera kartına "Önizle" butonu ekle
- `ffmpeg -i rtsp://... -vframes 1 preview.jpg`
- **Neden:** Kameranın gerçekten görüntü verip vermediğini görmek için kritik

### 2.4 DHCP Lease Tablosu Okuyucu
- Windows: `netsh dhcp server ... show clients` veya router web arayüzünü scrape et
- Alternatif: `ipconfig /all` + ARP birleştirmesi ile aktif DHCP cihazlarını göster
- **Neden:** Kim hangi IP'yi almış? Router login olmadan kısmi cevap verilebilir

### 2.5 SSH Komutu Çalıştırıcı (minimal)
- IP + kullanıcı + şifre/key gir, tek komut çalıştır, çıktıyı göster
- `SSH.NET` NuGet paketi (Renci.SshNet) kullanılabilir
- **Neden:** Router/switch/Linux sunucu yönetimi için; SNMP'nin tamamlayıcısı

---

## 3. UX / Arayüz İyileştirmeleri

### 3.1 Arama / Filtre Çubuğu (Chat'te)
- Chat panelinin üstüne küçük arama kutusu
- Yazılınca chat mesajlarını filtrele, eşleşenleri vurgula
- **Neden:** Uzun tarama çıktılarında belirli IP veya porta bakmak zorlaşıyor

### 3.2 Sonuç Dışa Aktarma — JSON / CSV
- Cihaz Tara, Port Tara, ARP sonuçlarını yapılandırılmış formatta kaydet
- Mevcut `BtnRapor` yalnızca düz metin; JSON/CSV seçeneği ekle
- **Neden:** Excel entegrasyonu, başka araçlara aktarım için

### 3.3 Geçmiş / Son Taramalar
- Son 10 tarama sonucunu `%APPDATA%\AgTarama\history\` altında JSON olarak sakla
- "Geçmiş" panelinden önceki sonuçlara bakma
- **Neden:** "Dün ne bulmuştum?" sorusuna cevap; log dosyası var ama arayüzden bakılamıyor

### 3.4 Karanlık/Açık Tema Geçişi
- Mevcut GitHub Dark teması korunarak bir de açık tema ekle
- `App.Resources` merkezinden `ResourceDictionary` ile değiştir
- Ayarlar panelinde toggle butonu

### 3.5 Çoklu Sekme / Çalışma Alanı
- Aynı anda iki ayrı port taraması veya iki ayrı ping seansı yürütme
- Tab kontrolü ile her sekme bağımsız panel
- **Neden:** Birden fazla hedefi aynı anda izleme ihtiyacı var

### 3.6 Bildirim Sistemi (Tray)
- Sistem tepsisine küçült
- Tarama tamamlanınca veya belirli port açılınca tray bildirimi
- `System.Windows.Forms.NotifyIcon` veya Windows App SDK Toast

---

## 4. Ağ Protokolü Genişletmeleri

### 4.1 NetBIOS / SMB Cihaz Adı Çözümleme — TAMAMLANDI
- `Services/NetbiosService.cs` ile `nbtstat -A <ip>` çıktısı parse ediliyor
- Cihaz Tara içinde ping yanıtı veren veya 139/445/3389 portu açık görünen IP'lerde çalışıyor
- Bilgisayar adı ve çalışma grubu bilgisi Cihaz Tara kartına `Ad` / `Grup` satırı olarak ekleniyor
- **Not:** Favorilerde IP yanına isim gösterimi ayrı UX işi olarak hâlâ eklenebilir

### 4.2 mDNS / Bonjour Keşif
- `224.0.0.251:5353`'e mDNS sorgusu gönder
- Apple, Chromecast, akıllı TV, yazıcı gibi cihazları tespit et
- Servis tipi: `_http._tcp`, `_printer._tcp`, `_googlecast._tcp` vb.
- **Neden:** Cihaz Tara'daki SSDP'nin tamamlayıcısı; IoT cihazlar çoğunlukla mDNS kullanır

### 4.3 LLDP / CDP Paket Okuyucu
- tshark ile `lldp` veya `cdp` protokolü filtreli kısa yakalama
- Komşu switch/router bilgilerini (port, VLAN, cihaz adı) göster
- **Neden:** Kurumsal ağlarda fiziksel topoloji haritalaması için temel

### 4.4 ICMP Timestamp / Type Haritalama
- Ping'e ek olarak ICMP timestamp, echo tiplerini raporla
- Cihazın OS fingerprint ipucu (TTL 64 = Linux, 128 = Windows, 255 = Cisco)
- **Neden:** Ping sonuçlarına TTL bilgisi zaten ekleniyor; OS tahmini adım küçük

### 4.5 WiFi Ağ Tarayıcı
- `netsh wlan show networks mode=bssid` çıktısını parse et
- Yakındaki SSID'ler, kanal, sinyal gücü, şifreleme türünü listele
- **Neden:** Kanalın kalabalık olup olmadığını, yabancı AP'ları tespit etmek için

---

## 5. Performans & Mimari

### 5.1 Cihaz Tara Paralel Yoğunluk Ayarı
- Şu an `SemaphoreSlim(80)` sabit; kullanıcıya slider ile 20–200 arası ayar
- Yavaş ağlarda timeout süresini de ayarlanabilir yap (800ms → kullanıcı seçimi)

### 5.2 `MainWindow.xaml.cs` Parçalama
- ~1750 satırlık dosya çok büyüdü; `partial class` ile böl:
  - `MainWindow.Capture.cs` — tshark yakalama
  - `MainWindow.Panels.cs` — yan panel animasyonları
  - `MainWindow.Devices.cs` — Cihaz Tara mantığı
  - `MainWindow.Network.cs` — ping/port/trace/dns
- **Neden:** Derleme süresi ve okunabilirlik; şu an tek dosyada 10+ bağımsız özellik var

### 5.3 Ayarlar Kalıcılığı
- `Services/AppSettings.cs` → `System.Text.Json` ile `%APPDATA%\AgTarama\settings.json`
- Timeout, tema, favori subnet, varsayılan community string gibi değerleri sakla
- Ayarlar paneli bu servisi okuyup yazsın

### 5.4 Hata Raporlama / Crash Log
- Yakalanmayan exception'lar için `AppDomain.CurrentDomain.UnhandledException` handler
- Stack trace'i `LogService.Hata` ile yaz; kullanıcıya "Hata raporu kaydedildi" toast'u göster

---

## 6. Entegrasyon Fırsatları

| Araç | Entegrasyon Tipi | Değer |
|---|---|---|
| Nmap | `nmap.exe` var ise sarmalayıcı çalıştır, OS detection sonucu göster | OS fingerprint |
| FFmpeg | RTSP frame yakala, thumbnail göster | Kamera doğrulama |
| OpenSSL | Sertifika bilgisini oku (HTTPS portları için) | Sertifika geçerlilik/CN |
| Netcat | Hızlı port/banner testi; port tara fallback | Güvenilirlik |
| Python/Scapy | Gelişmiş paket forge için opsiyonel bağımlılık | ARP spoof tespiti |

---

## 7. Güvenlik Geliştirmeleri

### 7.1 ARP Spoof / Zehirleme Tespiti
- ARP tablosunu periyodik izle; aynı IP için MAC değişirse uyar
- "Gateway MAC değişti!" kırmızı alert + log
- **Neden:** MITM saldırısının en yaygın belirtisi

### 7.2 Rogue DHCP Sunucu Tespiti
- Ağda birden fazla DHCP sunucu yanıtı alınırsa uyar
- tshark ile `dhcp` filtreli kısa yakalama
- **Neden:** Kurumsal ağlarda sık karşılaşılan güvenlik sorunu

### 7.3 Açık Port Değişiklik Alarmı
- Belirlenen IP'yi periyodik port tarama ile izle
- Öncekine göre yeni açık port çıkarsa bildirim ver
- **Neden:** Sunucu/kamera'da beklenmedik servis açılmasını fark etmek için

---

## Öncelik Özeti

| Öncelik | Özellik | Tahmini Zorluk |
|---|---|---|
| 🔴 Çok Yüksek | Ping Sweep | Kolay |
| 🔴 Çok Yüksek | MAC → Üretici (OUI) | Kolay |
| 🔴 Çok Yüksek | Bant Genişliği Monitörü | Orta |
| 🟡 Yüksek | HTTP Header Checker | Kolay |
| 🟡 Yüksek | mDNS Keşif | Orta |
| 🟡 Yüksek | Sonuç JSON/CSV Export | Kolay |
| 🟢 Orta | RTSP Önizleme | Zor (ffmpeg dep.) |
| 🟢 Orta | Port Sürüm Tespiti | Orta |
| 🟢 Orta | ARP Spoof Tespiti | Orta |
| 🟢 Orta | WiFi Ağ Tarayıcı | Kolay |
| 🔵 Düşük | SSH Komutu Çalıştırıcı | Zor |
| 🔵 Düşük | CVE Kontrolü | Zor |
| 🔵 Düşük | Çoklu Sekme | Zor |
