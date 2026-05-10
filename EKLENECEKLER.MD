# EKLENECEKLER — AG TARAMA Geliştirme Planı

> Tarih: 2026-05-10
> Mevcut sürüm: v0.1.0
> Bu plan, AG TARAMA programına eklenebilecek yeni özellikleri öncelik sırasına göre listeler.

---

## Faz 1 — DETAY.md'deki Mevcut TODO'lar (öncelikli)

Bunlar zaten kodda `// TODO` ile işaretli, en hızlı kazanç burada.

### 1.1 Port Tara
- **Durum:** `BtnPortTara_Click` boş, sadece "yakında eklenecek" mesajı veriyor.
- **Yaklaşım:**
  - Ping paneline benzer bir "Port Tara paneli" (yan açılır)
  - Hedef IP + port aralığı (ör. `1-1024`, `80,443,22`) girişi
  - `TcpClient.ConnectAsync` + `CancellationToken` ile paralel tarama (semafor ile 100 eşzamanlı limit)
  - Hızlı seçim chip'leri: "Yaygın portlar (21,22,23,25,53,80,443,3389)", "Web (80,443,8080,8443)", "Tüm (1-1024)"
  - Açık portlar yeşil, kapalı/timeout gri olarak listelenir
  - Well-known port → servis adı eşlemesi (ör. 22 = SSH)

### 1.2 Sonuçları Kaydet
- **Durum:** Yok.
- **Yaklaşım:**
  - Sağ panele `BtnKaydet` ekle
  - `SaveFileDialog` aç, `.txt` ve `.csv` filter
  - ChatPanel'deki tüm `Border > TextBlock` mesajlarını sırayla yaz (zaman damgası dahil)
  - CSV formatı: `zaman,tur,metin`

### 1.3 Wireshark "Aç" butonu (yakalama kartında)
- **Durum:** `WiresharkIleAc` metodu hazır ama UI'dan çağrı yok.
- **Yaklaşım:**
  - `YakalamaKartiOlustur` içinde `Tamamla(...)` çağrılınca karta "🔍 Wireshark'ta Aç" butonu eklensin
  - Tıklayınca `WiresharkIleAc(_sonPcap)` çağrısı

---

## Faz 2 — Ağ Teşhis Araçları (yüksek değer)

### 2.1 Traceroute (İz sürme)
- **Açıklama:** Hedefe giden IP hop'larını göster.
- **Yaklaşım:** `Ping` sınıfı + artan TTL (1'den 30'a) kullanarak manuel traceroute, ya da `tracert` process'i çağırıp output parse.
- **UI:** Ping paneline benzer açılır panel, her hop bir satır (IP, RTT).

### 2.2 DNS Lookup / Reverse DNS
- **Açıklama:** Hostname → IP, IP → Hostname.
- **Yaklaşım:** `Dns.GetHostEntryAsync()` / `Dns.GetHostAddressesAsync()`.
- **UI:** Küçük input + sonuç kutusu.

### 2.3 Ağ Adaptörü Bilgileri
- **Açıklama:** Yerel makinedeki tüm aktif adaptörlerin IP/MAC/Gateway/DNS/Subnet bilgileri.
- **Yaklaşım:** `NetworkInterface.GetAllNetworkInterfaces()` + `IPInterfaceProperties`.
- **UI:** "Yerel Bilgi" butonu → kart şeklinde her adaptör.

### 2.4 ARP Tablosu Görüntüleme
- **Açıklama:** `arp -a` çıktısını parse edip yerel ağdaki bilinen MAC↔IP eşleşmelerini göster.
- **Yaklaşım:** Process çağrısı + regex parse.
- **Bonus:** OUI veritabanı ile MAC → üretici eşleme (offline `oui.txt` ile).

### 2.5 Wake-on-LAN
- **Açıklama:** MAC adresine magic packet gönderip uzak bilgisayarı uyandır.
- **Yaklaşım:** `UdpClient` ile broadcast 0xFF×6 + MAC×16, port 9.
- **UI:** MAC giriş kutusu + "Uyandır" butonu.

---

## Faz 3 — Servis ve Erişim Kontrolleri

### 3.1 HTTP/HTTPS Endpoint Sağlık Kontrolü
- **Açıklama:** URL listesine GET atıp response code, response time, SSL geçerlilik göster.
- **Yaklaşım:** `HttpClient` + `Stopwatch`.
- **UI:** Liste girişi (her satır bir URL) + tablo sonuç (yeşil 2xx, sarı 3xx, kırmızı 4xx/5xx/timeout).

### 3.2 SSL Sertifika İncelemesi
- **Açıklama:** Bir hostname'in SSL sertifikası — issuer, validity, expiry, CN/SAN.
- **Yaklaşım:** `SslStream` + `RemoteCertificateValidationCallback`.
- **UI:** Hostname girişi → kart şeklinde detay.

### ✅ 3.3 SNMP Sorgusu (basit)
- **Açıklama:** Cihaz adı, sistem süresi, vs. SNMP v1/v2c ile.
- **Yaklaşım:** `Lextm.SharpSnmpLib` NuGet (12.*) — `Messenger.Get`, MIB-II sysGroup OID'leri.
- **Kullanım:** Yönetilebilir switch/router/yazıcı tespiti.
- **Tamamlandı:** 2026-05-10

---

## Faz 4 — Kullanılabilirlik ve Kalite

### 4.1 Favori IP Listesi
- **Açıklama:** Sık kullanılan IP'leri kaydet, ping/port tara panellerinin chip alanına otomatik ekle.
- **Yaklaşım:** `%APPDATA%\AgTarama\favorites.json` ile JSON saklama.

### 4.2 Geçmiş / Log
- **Açıklama:** Önceki yakalama dosyaları (`captures/` klasörü) listelensin, doğrudan Wireshark'ta açılabilsin.
- **UI:** "Geçmiş" butonu → liste, her satırda dosya adı, tarih, boyut + Aç/Sil butonları.

### 4.3 Ayarlar Paneli
- **Açıklama:** Yakalama dosya boyutu (`HedefMB`), test süresi (`TestSuresiSn`), ping timeout, port tarama eşzamanlılık limiti gibi sabit değerleri kullanıcıya aç.
- **Yaklaşım:** `%APPDATA%\AgTarama\settings.json`.
- **UI:** Sağ üst dişli ikonu → modal pencere.

### 4.4 Subnet/CIDR Hesaplayıcı
- **Açıklama:** `192.168.1.0/24` girince network address, broadcast, host range, subnet mask, host sayısı göster.
- **Yaklaşım:** Bit operasyonlarıyla manuel hesap.
- **UI:** Küçük yardımcı panel.

### 4.5 PDF/HTML Rapor Çıktısı
- **Açıklama:** Tarama sonuçlarını profesyonel formatta dışa aktarma (müşteri raporu için).
- **Yaklaşım:** `QuestPDF` (NuGet) ile PDF oluşturma.

---

## Faz 5 — UX İyileştirmeleri

### 5.1 Tema Değiştirici
- **Açıklama:** Şu anki tema GitHub Dark — açık tema seçeneği (opsiyonel).
- **Yaklaşım:** `ResourceDictionary` swap.

### 5.2 Klavye Kısayolları
- `Ctrl+L` → Ekranı temizle
- `Ctrl+S` → Sonuçları kaydet
- `Ctrl+P` → Ping paneli aç
- `F5` → Taramayı başlat
- `Esc` → Aktif panel/işlem iptal

### 5.3 Sürükle-Bırak pcap Açma
- **Açıklama:** Pencereye `.pcap` dosyası sürüklenince doğrudan Wireshark Portable'da açılsın.
- **Yaklaşım:** `Window.AllowDrop=True` + `Drop` event.

### 5.4 Bildirim Sesleri / Toast
- **Açıklama:** Tarama tamamlanınca opsiyonel ses + Windows toast bildirimi.
- **Yaklaşım:** `SystemSounds` + `Microsoft.Toolkit.Uwp.Notifications`.

---

## Faz 6 — Mimari İyileştirmeler (teknik borç)

### 6.1 ViewModel/Service Katmanı
- **Açıklama:** `MainWindow.xaml.cs` ~970 satır oldu; iş mantığı ayrı sınıflara bölünmeli.
- **Öneri:**
  - `Services/PingService.cs`
  - `Services/PortScanService.cs`
  - `Services/CaptureService.cs`
  - `Services/InterfaceDiscoveryService.cs`
  - MVVM gereksiz — code-behind kalabilir, ama ağ işlemleri ayrı sınıflara taşınmalı.

### 6.2 Yapılandırma Sabitleri Tek Yerde
- `AppBase`, `TsharkExe`, `WiresharkPortableExe`, `NpcapInstaller`, yeni `SadpExe` vs. → `Paths.cs` static class.

### 6.3 Loglama
- `%APPDATA%\AgTarama\logs\YYYYMMDD.log` — exception ve önemli işlemleri yaz (debug için).
- Hafif: `Microsoft.Extensions.Logging` veya custom `File.AppendAllTextAsync`.

### 6.4 Hata İşleme Standartlaştırma
- Şu an her metot kendi `try/catch` yapıyor. `MesajEkle("hata", ...)` çağrılarını tek `HataBildir(string, Exception?)` yardımcısına topla.

---

## Faz 7 — Kameralara Özel (Hikvision odaklı)

SADP entegrasyonu zaten var. Üzerine inşa edilebilecekler:

### 7.1 RTSP Stream Test
- Kamera RTSP URL'sini test et (`rtsp://user:pass@ip:554/Streaming/Channels/101`), bağlantı/codec bilgisi göster.
- Yaklaşım: `tshark` zaten var — RTSP DESCRIBE çağrısı + cevap parse, ya da `ffprobe` ekle.

### 7.2 ONVIF Cihaz Keşfi
- WS-Discovery ile yerel ağdaki ONVIF kameraları otomatik bul.
- Yaklaşım: UDP multicast `239.255.255.250:3702`.

### 7.3 Kamera Snapshot Önizleme
- HTTP `/ISAPI/Streaming/channels/101/picture` endpoint'inden anlık görüntü çek (Hikvision standardı).

---

## Önerilen Çalışma Sırası

| Sıra | Özellik | Tahmini Süre | Etki |
|------|---------|--------------|------|
| 1 | Port Tara (1.1) | 4-6 saat | Yüksek |
| 2 | Wireshark "Aç" butonu (1.3) | 30 dk | Orta |
| 3 | Sonuçları Kaydet (1.2) | 1-2 saat | Orta |
| 4 | Ağ Adaptörü Bilgisi (2.3) | 2 saat | Yüksek |
| 5 | DNS Lookup (2.2) | 1 saat | Orta |
| 6 | Traceroute (2.1) | 3 saat | Yüksek |
| 7 | ARP + OUI lookup (2.4) | 3 saat | Yüksek |
| 8 | Favoriler (4.1) | 2 saat | Orta |
| 9 | Servis katmanı refactor (6.1) | 4-6 saat | Yüksek (uzun vade) |
| 10 | Klavye kısayolları (5.2) | 1 saat | Düşük |
| 11 | RTSP/ONVIF (7.x) | 6+ saat | Domain-spesifik |

---

## Ek Notlar

- **Bağımlılık eklerken:** Single-file publish'i bozmamak için NuGet paketlerini `<PublishSingleFile>` ile uyumlu olanlarla sınırla.
- **Yeni harici exe ihtiyacı olduğunda** (tshark, sadptool gibi): `tools/` altına koy, `Paths.cs` üzerinden referans ver.
- **Yeni TODO/feature açtığında:** Bu dosyayı güncelle — tamamlananları `✅` ile işaretle, sil**me**.
- **Yeni özellik PR'ı:** Branch ismi `feature/<özellik-adi>` formatında olsun (mevcut: `yeni_ozellikler`).
