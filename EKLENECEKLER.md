# EKLENECEKLER.md — Geliştirme Yol Haritası

> Analiz tarihi: 2026-05-13
> Mevcut durum: v0.1.0, .NET 10 WPF, `MainWindow.xaml.cs` ~3454 satır
> Kapsam: Bu dosyada yalnızca henüz uygulanmamış veya mevcut özelliği anlamlı biçimde büyüten işler tutulur. `AGENTS.md` içinde tamamlanmış görünen özellikler bu listeden çıkarılmıştır.

---

## 1. Öncelikli Ürün Özellikleri

### 1.1 HTTP/HTTPS Başlık Denetleyicisi
- URL, hostname veya IP girilince HTTP/HTTPS yanıt başlıklarını ayrı bir sekmede göster.
- Status code, redirect zinciri, Server, Date, Content-Type, HSTS, CSP, X-Frame-Options ve X-Content-Type-Options alanlarını öne çıkar.
- HTTPS ise sertifika özetini aynı ekranda göster: CN/SAN, issuer, başlangıç-bitiş tarihi, kalan gün.
- **Neden:** Cihaz Tara içinde HTTP banner okuma var; bunu bağımsız saha aracına çevirmek hızlı değer üretir.

---

## 2. Cihaz Tara Geliştirmeleri

### 2.1 Cihaz Detay Çekmecesi
- DataGrid satırına çift tıklayınca web arayüzünü açmak yerine veya yanında sağdan detay paneli aç.
- Detayda kaynaklara göre ayrılmış bilgi göster: Ping, portlar, NetBIOS, DNS, ONVIF, SSDP, ARP, Advanced IP Scanner, banner.
- “Web arayüzünü aç”, “Ping”, “Port tara”, “Favorilere ekle”, “JSON kopyala” aksiyonları aynı panelde olsun.
- **Neden:** Tablo hızlı tarama için iyi; ayrıntılı teşhis için düzenli bir detay görünümü gerekiyor.

### 2.2 Cihaz Türü Düzeltme / Elle Etiketleme
- Kullanıcı bir cihazın türünü/markasını/modelini elle düzeltebilsin.
- Düzeltmeler `%APPDATA%\AgTarama\device-overrides.json` altında IP, MAC veya hostname anahtarıyla saklansın.
- Sonraki taramalarda otomatik tespitin üstüne kullanıcı etiketi uygulansın.
- **Neden:** Ağ keşfinde yüzde yüz otomatik sınıflandırma zor; kullanıcı bilgisini kalıcı yapmak kaliteyi artırır.

### 2.3 Risk ve Önem Rozetleri
- Cihaz satırlarına “Kritik”, “Yeni”, “Kayboldu”, “Web açık”, “RDP açık”, “Varsayılan kamera portu” gibi rozetler ekle.
- Basit kurallar ayarlanabilir olsun: örn. 3389 açık → dikkat, 23 açık → yüksek risk.
- Filtrelere risk rozeti de eklensin.
- **Neden:** Envanter kalabalıklaştığında sadece liste değil önceliklendirme gerekir.

### 2.4 ONVIF Profil ve Stream URI Çekme
- WS-Discovery sonrası ONVIF servis URL’sinden profil ve stream URI bilgisi al.
- Kimlik bilgisi gerekiyorsa kullanıcıdan iste; boş/anonim denemeyi güvenli timeout ile yap.
- Bulunan RTSP URI’leri detay panelinde kopyalanabilir olarak göster.
- **Neden:** RTSP yolunu tahmin etmek yerine cihazdan doğrudan almak daha doğru.

### 2.5 RTSP Snapshot / Kamera Önizleme
- FFmpeg portable varsa RTSP URL adaylarından tek kare yakala.
- Önizlemeleri `captures/previews/` altında sakla ve Cihaz Tara detayında thumbnail göster.
- Hata durumunda kimlik doğrulama, timeout veya stream bulunamadı nedenini kısa göster.
- **Neden:** Kameranın gerçekten görüntü verip vermediği tek bakışta anlaşılır.

---

## 3. Güvenlik ve İzleme

### 3.1 ARP Spoof / Gateway MAC Alarmı
- Gateway IP’sinin MAC adresini periyodik izle.
- Gateway MAC değişirse, aynı MAC birden fazla IP’de görünürse veya aynı IP farklı MAC’e geçerse uyar.
- Uyarıyı logla ve geçmiş paneline olay olarak yaz.
- **Neden:** MITM ve yanlış ağ cihazı problemlerinin erken belirtisi.

### 3.2 Rogue DHCP Sunucu Tespiti
- `tshark` ile kısa süreli DHCP/BOOTP yakalama yap.
- DHCP Offer/Ack veren server IP/MAC listesini çıkar.
- Birden fazla DHCP sunucu varsa uyarı göster.
- **Neden:** Kurumsal ağlarda yanlış takılmış modem/router hızlı bulunur.

### 3.3 Açık Port Değişiklik İzleme
- Kullanıcı seçtiği IP’leri periyodik port tarama ile izleyebilsin.
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
- Skoru Bant veya yeni “Ağ Sağlığı” sekmesinde göster.
- **Neden:** Teknik çıktıları hızlı okunur operasyonel özet haline getirir.

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
- Mevcut DNS Lookup’a kayıt tipi seçimi ekle: A, AAAA, CNAME, MX, TXT, NS, PTR.
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

---

## 5. Kullanıcı Deneyimi

### 5.1 Chat İçinde Arama ve Filtre
- Chat alanında IP, port veya kelime arama kutusu ekle.
- Eşleşen mesajları vurgula, eşleşmeyenleri soluklaştır veya gizle.
- Enter ile sonraki eşleşmeye git.
- **Neden:** Uzun tarama çıktılarında belirli sonucu bulmak zorlaşıyor.

### 5.2 Komut Paleti
- `Ctrl+K` ile hızlı komut paleti aç.
- “Ping 192.168.1.1”, “Cihaz Tara”, “ARP göster”, “Ayarlar” gibi komutlar aranabilir olsun.
- Son kullanılan hedefleri öner.
- **Neden:** Sekme ve butonlar çoğaldıkça hızlı klavye akışı değer kazanır.

### 5.3 Favorilere İsim ve Grup Ekleme
- Favori IP’lere görünen ad, not ve grup atanabilsin.
- Cihaz Tara’dan bulunan adlar favorilere öneri olarak gelsin.
- Favorilerde hızlı ping/port/web aksiyonları korunsun.
- **Neden:** Sadece IP listesi zamanla yetersiz kalır.

### 5.4 Tray Bildirimi ve Arka Plan Çalışma
- Uygulama sistem tepsisine küçültülebilsin.
- Uzun tarama bitince, riskli port bulununca veya izleme alarmı oluşunca bildirim çıksın.
- **Neden:** Kullanıcı uzun ağ taramalarını beklerken uygulamayı açık ekranda tutmak zorunda kalmaz.

### 5.5 Tema ve Yoğunluk Ayarları
- Mevcut koyu tema korunarak açık tema veya yüksek kontrast tema ekle.
- DataGrid yoğunluğu için kompakt/rahat görünüm seçeneği sun.
- **Neden:** Farklı ekran ve saha koşullarına uyum sağlar.

---

## 6. Mimari ve Bakım

### 6.1 `MainWindow.xaml.cs` Dosyasını Partial Parçalara Bölme
- Davranışı değiştirmeden büyük dosyayı partial sınıflara ayır.
- Önerilen parçalar: `MainWindow.Capture.cs`, `MainWindow.NetworkTools.cs`, `MainWindow.DeviceScan.cs`, `MainWindow.Export.cs`, `MainWindow.Rendering.cs`, `MainWindow.Helpers.cs`.
- **Neden:** Kod okunabilirliği artar, Claude/Codex ortak çalışmasında çakışma riski azalır.

### 6.2 Cihaz Tara Motorunu Servis Katmanına Alma
- Port scan, ONVIF, SSDP, NetBIOS, ARP ve kimlik belirleme akışını `Services/DeviceDiscoveryService.cs` altına taşı.
- UI yalnızca ilerleme ve sonuç bağlama işi yapsın.
- **Neden:** Test yazmak ve yeni keşif protokolü eklemek kolaylaşır.

### 6.3 Ortak ExportService
- Cihaz Tara export kodu çalışıyor; bunu Port, ARP ve geçmiş export için ortak servise çıkar.
- CSV, TXT, HTML Excel, PDF ve JSON üretimini tek yerde topla.
- **Neden:** Yeni rapor türlerinde kopya kod büyümesini önler.

### 6.4 Test Projesi
- Parse ve sınıflandırma fonksiyonları için test projesi ekle.
- Test adayları: port aralığı parse, IPv4/hostname doğrulama, ARP parse, OUI lookup, HTTP title parse, NetBIOS parse, cihaz türü sınıflandırma.
- **Neden:** Ağ araçlarında parse hataları sessiz ve can sıkıcı olur; küçük testler güven verir.

### 6.5 İşlem Durum Merkezi
- Aktif yakalama, ping, port tarama, cihaz tarama ve izleme görevlerini tek noktada listele.
- İptal, tekrar çalıştır ve log aç aksiyonları sun.
- **Neden:** Uygulama büyüdükçe “şu anda ne çalışıyor?” sorusuna net cevap gerekir.

---

## 7. Önerilen Uygulama Sırası

| Sıra | Özellik | Öncelik | Zorluk |
|---:|---|---|---|
| 1 | HTTP/HTTPS Başlık Denetleyicisi | Yüksek | Kolay |
| 2 | Cihaz detay çekmecesi | Orta-Yüksek | Orta |
| 3 | Favorilere isim ve grup ekleme | Orta-Yüksek | Kolay-Orta |
| 4 | ARP Spoof / Gateway MAC alarmı | Orta | Orta |
| 5 | DNS araçlarını genişletme | Orta | Kolay-Orta |
| 6 | WiFi Ağ Tarayıcı | Orta | Kolay |
| 7 | `MainWindow.xaml.cs` partial parçalara bölme | Orta | Orta |
| 8 | Cihaz Tara motorunu servis katmanına alma | Orta | Orta-Zor |
| 9 | ONVIF profil ve stream URI çekme | Orta | Zor |
| 10 | RTSP snapshot / kamera önizleme | Orta | Zor |

---

## 8. Bilerek Çıkarılan Tamamlanmış Maddeler

Aşağıdaki maddeler `AGENTS.md` içinde tamamlanmış olarak işlendiği için açık yol haritasından çıkarıldı:

- Bant Genişliği Monitörü
- Ping Sweep
- ARP tablosu ve MAC/OUI üretici gösterimi
- Cihaz Tara içinde ARP/MAC/OUI zenginleştirme
- NetBIOS / SMB cihaz adı çözümleme
- ONVIF WS-Discovery ve SSDP/UPnP keşif
- Advanced IP Scanner console zenginleştirme
- Cihaz Tara DataGrid, sütun filtreleri ve sağ tık export menüsü
- Cihaz Tara Excel/PDF/TXT/CSV dışa aktarma
- Cihaz Tara JSON dışa aktarma
- Port Tara panelinde banner/sürüm tespiti
- Ping, Port Tara, Cihaz Tara, ARP ve yakalama geçmişi
- Geçmiş sekmesi, JSON kayıt açma, tekrar çalıştırma ve Cihaz Tara karşılaştırma
- mDNS / Bonjour keşif
- Ayarlar kalıcılığı
- Favori IP listesi
- Sekme tabanlı tam ekran UI
