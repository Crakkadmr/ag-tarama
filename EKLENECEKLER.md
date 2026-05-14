# EKLENECEKLER.md — Geliştirme Yol Haritası

> Güncelleme: 2026-05-14
> Mevcut durum: v0.1.0, .NET 10 WPF, `Partials/` mimarisi (8 dosya), sıfır NuGet bağımlılığı
> Kapsam: Yalnızca henüz uygulanmamış veya mevcut özelliği anlamlı biçimde büyüten işler.

---

## 1. Öncelikli Ürün Özellikleri

### 1.1 HTTP/HTTPS Başlık Denetleyicisi
- URL, hostname veya IP girilince HTTP/HTTPS yanıt başlıklarını ayrı bir sekmede göster.
- Status code, redirect zinciri, Server, Date, Content-Type, HSTS, CSP, X-Frame-Options ve X-Content-Type-Options alanlarını öne çıkar.
- HTTPS ise sertifika özetini aynı ekranda göster: CN/SAN, issuer, başlangıç-bitiş tarihi, kalan gün.
- **Neden:** Cihaz Tara içinde HTTP banner okuma var; bunu bağımsız saha aracına çevirmek hızlı değer üretir.

### 1.2 Çoklu Hedef Ping
- Ping Testi sekmesine virgülle ayrılmış IP listesi veya subnet aralığı (`192.168.1.1-50`) girişi ekle.
- Tüm hedeflere eşzamanlı ping at; sonuçları tablo olarak göster: IP, durum, gecikme, kayıp %.
- En yavaş ve en kayıplı hedefleri renkli vurgula.
- **Neden:** Tek ping yeterli; ama segment sorunlarını bulmak için toplu tablo çok daha hızlı.

### 1.3 Subnet / CIDR Hesaplayıcı
- IP + CIDR mask (veya subnet mask) gir → Ağ adresi, broadcast, ilk/son host, host sayısı, wildcard mask hesapla.
- Supernet ve alt-subnet bölme önizlemesi göster.
- Sonucu panoya kopyalama butonu ekle.
- **Neden:** Saha çalışmasında ağ sınırlarını hesaplamak günlük ihtiyaç; tarayıcıya gerek kalmasın.

---

## 2. Cihaz Tara Geliştirmeleri

### 2.1 Cihaz Detay Çekmecesi
- DataGrid satırına çift tıklayınca web arayüzünü açmak yerine sağdan kayan detay paneli aç.
- Detayda kaynaklara göre ayrılmış bilgi göster: Ping, portlar, NetBIOS, DNS, ONVIF, SSDP, ARP, Advanced IP Scanner, banner.
- "Web arayüzünü aç", "Ping", "Port tara", "Favorilere ekle", "JSON kopyala" aksiyonları aynı panelde olsun.
- **Neden:** Tablo hızlı tarama için iyi; ayrıntılı teşhis için düzenli bir detay görünümü gerekiyor.

### 2.2 Cihaz Türü Düzeltme / Elle Etiketleme
- Kullanıcı bir cihazın türünü/markasını/modelini elle düzeltebilsin.
- Düzeltmeler `%APPDATA%\AgTarama\device-overrides.json` altında IP, MAC veya hostname anahtarıyla saklansın.
- Sonraki taramalarda otomatik tespitin üstüne kullanıcı etiketi uygulansın.
- **Neden:** Ağ keşfinde yüzde yüz otomatik sınıflandırma zor; kullanıcı bilgisini kalıcı yapmak kaliteyi artırır.

### 2.3 Risk ve Önem Rozetleri
- Cihaz satırlarına "Kritik", "Yeni", "Kayboldu", "Web açık", "RDP açık", "Varsayılan kamera portu" gibi rozetler ekle.
- Basit kurallar ayarlanabilir olsun: örn. 3389 açık → dikkat, 23 açık → yüksek risk.
- Filtrelere risk rozeti de eklensin.
- **Neden:** Envanter kalabalıklaştığında sadece liste değil önceliklendirme gerekir.

### 2.4 ONVIF Profil ve Stream URI Çekme
- WS-Discovery sonrası ONVIF servis URL'sinden profil ve stream URI bilgisi al.
- Kimlik bilgisi gerekiyorsa kullanıcıdan iste; boş/anonim denemeyi güvenli timeout ile yap.
- Bulunan RTSP URI'leri detay panelinde kopyalanabilir olarak göster.
- **Neden:** RTSP yolunu tahmin etmek yerine cihazdan doğrudan almak daha doğru.

### 2.5 RTSP Snapshot / Kamera Önizleme
- FFmpeg portable varsa RTSP URL adaylarından tek kare yakala.
- Önizlemeleri `captures/previews/` altında sakla ve Cihaz Tara detayında thumbnail göster.
- Hata durumunda kimlik doğrulama, timeout veya stream bulunamadı nedenini kısa göster.
- **Neden:** Kameranın gerçekten görüntü verip vermediği tek bakışta anlaşılır.

### 2.6 Cihaz Notları
- Her IP'ye kalıcı serbest metin notu eklenebilsin (`%APPDATA%\AgTarama\device-notes.json`).
- Not varsa DataGrid satırında küçük ikon göster; üzerine gelinince ToolTip ile içerik çıksın.
- Cihaz detay çekmecesinde düzenleme alanı olsun.
- **Neden:** "Bu IP ne işe yarıyor?" sorusunun cevabını tarama sonuçlarıyla birlikte tutmak envanter kalitesini artırır.

---

## 3. Güvenlik ve İzleme

### 3.1 ARP Spoof / Gateway MAC Alarmı
- Gateway IP'sinin MAC adresini periyodik izle.
- Gateway MAC değişirse, aynı MAC birden fazla IP'de görünürse veya aynı IP farklı MAC'e geçerse uyar.
- Uyarıyı logla ve geçmiş paneline olay olarak yaz.
- **Neden:** MITM ve yanlış ağ cihazı problemlerinin erken belirtisi.

### 3.2 Rogue DHCP Sunucu Tespiti
- `tshark` ile kısa süreli DHCP/BOOTP yakalama yap.
- DHCP Offer/Ack veren server IP/MAC listesini çıkar.
- Birden fazla DHCP sunucu varsa uyarı göster.
- **Neden:** Kurumsal ağlarda yanlış takılmış modem/router hızlı bulunur.

### 3.3 Açık Port Değişiklik İzleme
- Kullanıcı seçtiği IP'leri periyodik port tarama ile izleyebilsin.
- Önceki sonuca göre yeni açılan veya kapanan portları bildir.
- İzleme profilleri ayarlardan yönetilsin.
- **Neden:** Beklenmedik RDP/SSH/web arayüzü açılması güvenlik riski olabilir.

### 3.4 CVE / Güvenlik Açığı Kontrolü
- Banner/sürüm tespiti sonrası yerel küçük bir CVE eşleştirme tablosuyla uyarı üret.
- İlk aşamada NVD tamamı yerine kamera/router/web server için elle seçilmiş kritik imza seti kullanılabilir.
- Dışa aktarma raporlarına risk özeti ekle.
- **Neden:** Envanter bilgisi, güvenlik kararına dönüşür.

### 3.5 Ağ Sağlığı Skoru
- Ping gecikmesi, kayıp, açık riskli portlar, gateway değişimi, DNS başarısı ve cihaz sayısı değişiminden basit skor üret.
- Skoru Bant veya yeni "Ağ Sağlığı" sekmesinde göster.
- **Neden:** Teknik çıktıları hızlı okunur operasyonel özet haline getirir.

### 3.6 Zamanlanmış / Otomatik Tarama
- Belirli aralıklarla (örn. her 10 dakika veya gün başında) Cihaz Tara veya Ping Sweep otomatik çalışsın.
- Önceki taramaya göre fark varsa (yeni cihaz, kaybolan cihaz) toast + log ile bildir.
- Zamanlama profilleri ayarlardan yapılandırılsın; uygulama tepsiye küçülmüşken de devam etsin.
- **Neden:** Ağ değişikliklerini fark etmek için kullanıcının manuel tarama başlatması gerekmemeli.

---

## 4. Ağ Araçları ve Protokoller

### 4.1 WiFi Ağ Tarayıcı
- `netsh wlan show networks mode=bssid` çıktısını parse et.
- SSID, BSSID, kanal, sinyal gücü, şifreleme türü ve kanal kalabalığını göster.
- Kanal çakışması için kısa öneri üret.
- **Neden:** Kablosuz sorunlarında kanal ve sinyal görünürlüğü gerekir.

### 4.2 LLDP / CDP Komşu Okuyucu
- `tshark` ile kısa süre `lldp` veya `cdp` filtreli yakalama yap.
- Komşu switch/router adı, port, VLAN ve management IP bilgilerini göster.
- **Neden:** Kurumsal ağlarda fiziksel topoloji ve switch portu bulma için değerli.

### 4.3 DNS Araçlarını Genişletme
- Mevcut DNS Lookup'a kayıt tipi seçimi ekle: A, AAAA, CNAME, MX, TXT, NS, PTR.
- DNS sunucusu seçilebilir olsun: sistem DNS, 1.1.1.1, 8.8.8.8 veya özel.
- Yanıt süresini ve TTL değerlerini göster.
- **Neden:** DNS sorunları için mevcut panel daha teşhis odaklı hale gelir.

### 4.4 Traceroute Görsel Yol Haritası
- Traceroute sonuçlarını tablo ve çizgisel akış olarak göster.
- Hop gecikme ortalaması, zaman aşımı ve ani gecikme artışı renklendirilsin.
- İsteğe bağlı olarak IP geolocation verisi desteklenebilir.
- **Neden:** Düz metin traceroute çıktısını okumak zor; görsel farklar daha hızlı anlaşılır.

### 4.5 iperf3 Performans Testi Entegrasyonu
- `iperf3.exe` varsa client/server modunu arayüzden yönet.
- Hedef, süre, paralel stream ve yön seçenekleri sun.
- Sonuçları Mbps/Gbps ve jitter/loss olarak raporla.
- **Neden:** Bant monitörü yerel hızları gösterir; iperf3 gerçek uçtan uca performansı ölçer.

### 4.6 SSH / Telnet Gömülü Terminal
- Seçili cihaza Cihaz Tara sağ tık → "Terminal Aç" ile bağlan.
- SSH için `ssh.exe` (Windows 10+ yerleşik) veya `plink.exe` process olarak başlat; çıktıyı uygulama içi panel'de göster.
- Telnet için `telnet.exe` aynı şekilde wrap et.
- **Neden:** RDP yerine SSH ile yönetilen cihazlara tek tıkla bağlanmak saha verimliliğini artırır.

### 4.7 Ping Gecikmesi Trend Grafiği
- Süregelen ping modunda gecikme değerlerini gerçek zamanlı çizgi grafikle göster.
- Son 60 değeri WPF `Polyline` / `Canvas` ile sıfır NuGet'e çiz; min/maks/ortalama değerleri göster.
- Kayıp paketleri kırmızı nokta ile işaretle.
- **Neden:** Anlık gecikme tek değer gösterir; trend kayıp ve spike'ları açıkça ortaya koyar.

---

## 5. Kullanıcı Deneyimi

### 5.1 Chat İçinde Arama ve Filtre
- Chat alanında IP, port veya kelime arama kutusu ekle (`Ctrl+F`).
- Eşleşen mesajları vurgula, eşleşmeyenleri soluklaştır veya gizle.
- Enter ile sonraki eşleşmeye git.
- **Neden:** Uzun tarama çıktılarında belirli sonucu bulmak zorlaşıyor.

### 5.2 Komut Paleti
- `Ctrl+K` ile hızlı komut paleti aç.
- "Ping 192.168.1.1", "Cihaz Tara", "ARP göster", "Ayarlar" gibi komutlar aranabilir olsun.
- Son kullanılan hedefleri öner.
- **Neden:** Sekme ve butonlar çoğaldıkça hızlı klavye akışı değer kazanır.

### 5.3 Favorilere İsim ve Grup Ekleme
- Favori IP'lere görünen ad, not ve grup atanabilsin.
- Cihaz Tara'dan bulunan adlar favorilere öneri olarak gelsin.
- Favorilerde hızlı ping/port/web aksiyonları korunsun.
- **Neden:** Sadece IP listesi zamanla yetersiz kalır.

### 5.4 Tray Bildirimi ve Arka Plan Çalışma
- Uygulama sistem tepsisine küçültülebilsin.
- Uzun tarama bitince, riskli port bulununca veya izleme alarmı oluşunca Windows bildirim baloncuğu çıksın.
- Kapatma yerine tepsiye küçültme varsayılan davranış olsun (çarpı → tepsiye, sağ tık → Çıkış).
- **Neden:** Kullanıcı uzun ağ taramalarını beklerken uygulamayı açık ekranda tutmak zorunda kalmaz.

### 5.5 Tema ve Yoğunluk Ayarları
- Mevcut koyu tema korunarak açık tema veya yüksek kontrast tema ekle.
- DataGrid yoğunluğu için kompakt/rahat görünüm seçeneği sun.
- **Neden:** Farklı ekran ve saha koşullarına uyum sağlar.

### 5.6 Giriş Otomatik Tamamlama
- Ping, Port Tara, DNS, WoL ve Traceroute giriş kutularında daha önce girilen IP/hostname değerlerini öneri olarak göster.
- Son 20 benzersiz değer `%APPDATA%\AgTarama\input-history.json` içinde saklansın.
- `↓` tuşu veya açılır liste ile seçim yapılabilsin.
- **Neden:** Aynı hedeflere tekrar tekrar yazma zahmetini kaldırır; Geçmiş sekmesine alternatif değil tamamlayıcı.

---

## 6. Mimari ve Bakım

### 6.1 Cihaz Tara Motorunu Servis Katmanına Alma
- Port scan, ONVIF, SSDP, NetBIOS, ARP ve kimlik belirleme akışını `Services/DeviceDiscoveryService.cs` altına taşı.
- UI yalnızca ilerleme ve sonuç bağlama işi yapsın.
- **Neden:** Test yazmak ve yeni keşif protokolü eklemek kolaylaşır.

### 6.2 Ortak ExportService
- Cihaz Tara export kodu çalışıyor; bunu Port, ARP ve geçmiş export için ortak servise çıkar.
- CSV, TXT, HTML/Excel, PDF ve JSON üretimini tek yerde topla.
- **Neden:** Yeni rapor türlerinde kopya kod büyümesini önler.

### 6.3 Test Projesi
- Parse ve sınıflandırma fonksiyonları için `AgTarama.Tests` projesi ekle.
- Test adayları: port aralığı parse, IPv4/hostname doğrulama, ARP parse, OUI lookup, HTTP title parse, NetBIOS parse, cihaz türü sınıflandırma.
- **Neden:** Ağ araçlarında parse hataları sessiz ve can sıkıcı olur; küçük testler güven verir.

### 6.4 İşlem Durum Merkezi
- Aktif yakalama, ping, port tarama, cihaz tarama ve izleme görevlerini tek noktada listele.
- İptal, tekrar çalıştır ve log aç aksiyonları sun.
- **Neden:** Uygulama büyüdükçe "şu anda ne çalışıyor?" sorusuna net cevap gerekir.

### 6.5 Özelleştirilebilir Port / Servis Listesi
- `BilindikPortlar` sözlüğü şu an kod içinde sabit; bunu `%APPDATA%\AgTarama\custom-ports.json` ile kullanıcı tarafından genişletilebilir hale getir.
- Ayarlar penceresine port ekle/sil/düzenle arayüzü ekle.
- **Neden:** Kamera markaları ve kurumsal uygulamalar farklı özel portlar kullanabilir; kullanıcı bunları bir kez tanımlamalı.

---

## 7. Önerilen Uygulama Sırası

| Sıra | Özellik | Öncelik | Zorluk |
|---:|---|---|---|
| 1 | HTTP/HTTPS Başlık Denetleyicisi | Yüksek | Kolay |
| 2 | Subnet / CIDR Hesaplayıcı | Yüksek | Kolay |
| 3 | Çoklu Hedef Ping | Yüksek | Kolay-Orta |
| 4 | Ping Gecikmesi Trend Grafiği | Orta-Yüksek | Orta |
| 5 | Cihaz Detay Çekmecesi | Orta-Yüksek | Orta |
| 6 | Giriş Otomatik Tamamlama | Orta-Yüksek | Kolay |
| 7 | Favorilere İsim ve Grup Ekleme | Orta | Kolay-Orta |
| 8 | DNS Araçlarını Genişletme | Orta | Kolay-Orta |
| 9 | Cihaz Notları | Orta | Kolay |
| 10 | ARP Spoof / Gateway MAC Alarmı | Orta | Orta |
| 11 | Zamanlanmış / Otomatik Tarama | Orta | Orta |
| 12 | WiFi Ağ Tarayıcı | Orta | Kolay |
| 13 | Tray Bildirimi ve Arka Plan Çalışma | Orta | Orta |
| 14 | Özelleştirilebilir Port / Servis Listesi | Orta | Kolay |
| 15 | Cihaz Türü Düzeltme / Elle Etiketleme | Orta | Orta |
| 16 | Cihaz Tara motorunu servis katmanına alma | Düşük-Orta | Orta-Zor |
| 17 | Ortak ExportService | Düşük | Orta |
| 18 | Test Projesi | Düşük | Kolay-Orta |
| 19 | ONVIF Profil ve Stream URI Çekme | Düşük | Zor |
| 20 | RTSP Snapshot / Kamera Önizleme | Düşük | Zor |
