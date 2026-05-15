# AgTarama — Eklenebilecek / Geliştirilebilecek Özellikler

## Context

**Network Sniffer (AgTarama)** WPF tabanlı bir ağ tarama / paket yakalama / chatbot arayüzlü masaüstü uygulamasıdır. Mevcut araçlar: ping, port tara, traceroute, DNS, ARP, WoL, bant genişliği, çoklu protokollü cihaz tarama (ONVIF/SSDP/mDNS/NetBIOS), tshark paket yakalama, Supabase lisanslama. Bu doküman, mevcut mimariye uyumlu, pentesting ve ağ-yöneticisi senaryolarında değer üretecek **yeni özellik önerilerini** ve **mevcut zayıf noktaları** tek bir yerde toplar. Her madde; özellik adı, somut bir örnek, gerekçe ve yapay zekâ ajanına verebileceğin uygulama prompt'unu içerir.

Her özellik için ortak uyulması gereken proje kuralları (her promptun üzerine düşer):

- `async/await` + `CancellationToken` zorunlu, UI thread bloke edilmez.
- Sonuçlar **ana chat'e değil**, kendi `XxxResultPanel`'ine yazılır. Chat'e yalnızca `MesajEkle("sonuc", ...)` ile özet mesaj gider.
- Sekme geçişi: `MainTabControl.SelectedIndex = TabXxx` (sabit ekle).
- Stil kaynakları `MainWindow.xaml > Window.Resources`'ta; `ActiveActionButton`, `ActionButton`'dan sonra.
- Harici araç başlatma `HariciAracBaslat(exe, ad)`, toast `ToastGoster(mesaj, hata:bool)`.
- Yeni servis varsa `Services/` altına; UI mantığı yeni bir `MainWindow.<Ad>.cs` partial'ı içine.
- Markdown dosyaları otomatik güncellenmez (yalnız "md güncelle" denirse).

---

## ÖNERİLEN YENİ ÖZELLİKLER

---

### 1) SSL/TLS Sertifika Müfettişi

**Örnek:** Kullanıcı `192.168.1.10:443` yazar → sertifika CN/SAN, geçerlilik tarihleri, self-signed / Let's Encrypt / kurumsal CA tespiti, TLS sürümü (1.0/1.1/1.2/1.3) ve aktif cipher suite listesi panele dökülür. Süresi 30 günden az kalan sertifikalar kırmızı vurgulanır.

**Neden Gerekli:** Cihaz Tara şu anda 443/8443 portunu **açık/kapalı** olarak görüyor; sertifika içeriğine bakmıyor. Kamera/NVR/router yönetim panelleri çoğunlukla self-signed sertifika ile gelir — bu özellik, hangi cihazın orijinal hangi cihazın klonlanmış olduğunu, sertifikaların ne zaman expire olacağını görmeyi sağlar. Pentest senaryolarında zayıf TLS (TLS 1.0/RC4) tespiti için kritik.

**AI Ajan Prompt:**
```
AgTarama WPF projesinde "SSL Sertifika Müfettişi" özelliği ekle.

Gerekenler:
1) Services/SslInspectorService.cs oluştur. Tek metod: 
   async Task<SslCertificateInfo> InspectAsync(string host, int port, CancellationToken ct).
   System.Net.Security.SslStream + RemoteCertificateValidationCallback ile sertifikayı yakalayıp döndür.
   SslCertificateInfo: Subject, Issuer, NotBefore, NotAfter, Thumbprint, SignatureAlgorithm, 
   SubjectAlternativeNames (X509 extension), TlsVersion (SslStream.SslProtocol), CipherSuite (NegotiatedCipherSuite).
2) MainWindow.xaml'e yeni sekme "SSL Tara" ekle (TabSslInspector sabiti). Içeride host+port input, 
   Tara butonu, sonuç paneli (SslResultPanel).
3) MainWindow.SslInspector.cs partial'ı: butonun event handler'ı, sonucu panele basar, 
   chat'e ozet ("SSL sertifikası okundu: CN=..., gecerli X gun") MesajEkle ile yazar.
4) NotAfter - DateTime.UtcNow < 30 gun ise kirmizi vurgu.
5) Hata olursa ToastGoster ile bildir; LogService.LogAsync ile kaydet.
6) Cihaz Tara sonuc satirinda sag-tik menusune "SSL sertifikasini incele" 
   secenegi ekle (port 443/8443 acik ise).
.NET 10 / WPF / Nullable=enable.
```

---

### 2) Servis & Banner Fingerprinting Motoru

**Örnek:** Port tarama sırasında 22/tcp açık görünürse → `SSH-2.0-OpenSSH_8.9p1 Ubuntu-3ubuntu0.1` banner'ı çekilir, "Ubuntu 22.04, OpenSSH 8.9p1 (CVE-2023-38408 etkilenmiş)" şeklinde yorumlanır. FTP, Telnet, SMTP, Redis, MySQL, PostgreSQL bannerleri de tanınır.

**Neden Gerekli:** Mevcut Cihaz Tara yalnızca 15 sabit port + HTTP/RTSP banner okuyor. SSH/FTP/Telnet/SMTP/DB servislerinin sürümleri görünmüyor. Bu motor, "ne çalışıyor, hangi sürüm" sorusunu cevaplar ve eski/yamalanmamış servisleri tespit eder.

**AI Ajan Prompt:**
```
AgTarama'ya "Banner Fingerprinting" motoru ekle.

1) Services/BannerService.cs: 
   async Task<BannerResult> GrabAsync(string ip, int port, CancellationToken ct).
   TcpClient ile bagla, ilk 4096 byte oku (NetworkStream.ReadAsync, 3sn timeout). 
   Bazi servisler greeting yollar (SSH, FTP, SMTP, Telnet, Redis "+PONG"); 
   yollamayanlar (HTTP, MySQL) icin protokole ozel probe gonder:
     - HTTP: "HEAD / HTTP/1.0\r\n\r\n"
     - Redis: "PING\r\n"
     - MySQL: ilk 4 byte handshake header oku.
2) Services/FingerprintRules.cs: regex tabanli kural seti. Or:
   { Pattern: "^SSH-2\\.0-OpenSSH_([\\d\\.]+)", Service: "OpenSSH", VersionGroup: 1 }
   En az SSH, FTP (vsftpd, ProFTPD), Telnet, SMTP, HTTP (Apache/nginx/IIS), 
   MySQL, PostgreSQL, Redis, MongoDB, SMB icin kural.
3) MainWindow.DeviceScan.cs icindeki cihaz kesfine entegre et: 
   acik port bulundugunda BannerService cagrilsin, sonuc DataGrid'de "Servis/Surum" kolonu 
   olarak gosterilsin (yeni kolon ekle).
4) Export (CSV/JSON/PDF) bu kolonu da icersin.
5) Tum cagrilarda SemaphoreSlim ile concurrency=30 sinirla, CancellationToken destekle.
```

---

### 3) HTTP/HTTPS Yönetim Paneli Tespiti & Varsayılan Kimlik Kontrolü

**Örnek:** Cihaz tarama bulduğu IP'ler için `/`, `/login`, `/admin`, `/cgi-bin/login` yollarını dener; HTTP başlığı + sayfa başlığı + form alanı ile "Hikvision Web Login", "TP-Link Router", "MikroTik RouterOS" tespit eder. Bilinen varsayılan kimlikleri (`admin/admin`, `admin/12345`, `root/pass`) **yalnızca kullanıcı onayıyla** denemek için bir buton sunar.

**Neden Gerekli:** İç ağda en sık karşılaşılan zafiyet, kameraların ve router'ların varsayılan şifrelerle bırakılmasıdır. Pentest / iç güvenlik denetimlerinde manuel tarayıcı açıp tek tek bakmak vakit alır; otomatik tarama büyük zaman tasarrufu sağlar.

**AI Ajan Prompt:**
```
AgTarama'ya "Web Paneli Tespiti" + "Varsayilan Kimlik Denemesi" ekle.

1) Services/WebPanelDetectorService.cs:
   async Task<WebPanelInfo> DetectAsync(string url, CancellationToken ct).
   HttpClient ile GET /, /login, /admin yollarini dener. 
   Sayfa title'i, "Server" header'i, body icinde "Hikvision", "TP-Link" gibi 
   imza stringlerini arar. Cikti: Vendor, Model, LoginUrl, FormFields (string[]).
2) Resources/DefaultCredentials.json: vendor bazli kimlik listesi 
   (Hikvision: admin/12345, TP-Link: admin/admin, MikroTik: admin/(empty) vs.).
3) Services/DefaultCredCheckService.cs:
   async Task<CredCheckResult> TryAsync(WebPanelInfo info, IEnumerable<Cred> creds, CancellationToken ct).
   FORM POST + HTTP Basic Auth her ikisini destekle. Basari kriteri: HTTP 200 + redirect, 
   "logout" link, set-cookie session.
4) MainWindow.DeviceScan.cs: sag-tik menusune iki yeni secenek ekle:
   - "Web panelini tespit et"
   - "Varsayilan sifreleri dene"  [ONAY DIYALOGU ZORUNLU]
5) Sonuc panelinde bulunan kimlikler ACIK SARI uyari ile gosterilsin. 
   LogService'e SUCCESS/FAIL kaydi yazilsin.
6) ETIK NOT: Onay diyalogunda "Bu islem sadece yetkili oldugunuz aglarda kullanin." 
   uyarisi zorunlu.
```

---

### 4) WHOIS / GeoIP / Reverse DNS Zenginleştirme Paneli

**Örnek:** Kullanıcı bir public IP (örn `8.8.8.8`) yazar → WHOIS kayıtları (Network Range, Org, Country, Abuse contact), GeoIP (Şehir/Ülke/ISP), Reverse DNS (`dns.google`) ve PTR kayıtları görünür.

**Neden Gerekli:** Şu an proje yalnızca iç ağ odaklı. Bir log'da görülen tanımadığı dış IP'nin kim olduğunu (Cloudflare? Bot ağ? AWS?) anlamak için harici servise gitmek gerekiyor. Tek panelde toplamak loglardan kaynak tespitini hızlandırır.

**AI Ajan Prompt:**
```
AgTarama'ya "IP Intel" sekmesi ekle.

1) Services/IpIntelService.cs:
   - WhoisAsync(string ip): RIPE/ARIN/APNIC whois.iana.org -> whois.X.net TCP 43 sorgu zinciri. 
     Cevabi parse et: NetRange, OrgName, Country, AbuseEmail.
   - GeoIpAsync(string ip): https://ipinfo.io/{ip}/json (free tier, anahtarsiz) veya 
     https://ip-api.com/json/{ip} kullan; JSON deserialize.
   - ReverseDnsAsync(string ip): Dns.GetHostEntryAsync.
2) MainWindow.xaml'e "IP Intel" sekmesi (TabIpIntel). Input + Sorgula butonu + IpIntelResultPanel 
   (3 kart: WHOIS, GeoIP, RDNS).
3) MainWindow.IpIntel.cs partial. Tum cagrilar paralel (Task.WhenAll), her birinin failure'i 
   bagimsiz gosterilsin.
4) Ag isteklerinde 5sn timeout. CancellationToken kullanici "iptal" butonuna basinca.
5) Sonuc Gecmise (HistoryService) yazilsin: "IpIntel" kayit tipi olarak.
```

---

### 5) DNS Enumerasyon (Reverse Sweep + Zone Transfer + Wildcard)

**Örnek:** Kullanıcı `example.com` yazar → A/AAAA/MX/NS/TXT/CNAME kayıtları, NS sunucularına AXFR (zone transfer) denemesi, alt domain wildcard tespiti, `192.168.1.0/24` için reverse DNS sweep.

**Neden Gerekli:** Mevcut DNS sekmesi tek bir hostname'i tek bir IP'ye çeviren basit bir `Dns.GetHostEntry` çağrısı. Profesyonel kullanım için yetersiz; bu özellik domain reconnaissance için gerekli minimum.

**AI Ajan Prompt:**
```
AgTarama'nin mevcut DNS aracini "DNS Enum" olarak yukselt.

1) DnsClient.NET NuGet paketi EKLEME (proje no-deps; bunun yerine):
   Services/DnsEnumService.cs icinde System.Net.Dns + UdpClient ile manuel DNS paketi 
   olustur (Header + Question section, big-endian).
   - QueryAsync(string domain, DnsType type) -> List<DnsRecord>
   - Desteklenen tipler: A(1), AAAA(28), MX(15), NS(2), TXT(16), CNAME(5), PTR(12), SOA(6).
2) ReverseSweepAsync(string cidr, CancellationToken ct): /24 icin 254 PTR sorgu, paralel 50.
3) ZoneTransferAsync(string domain, string nsServer): TCP 53'e AXFR (tip 252) gonder. 
   Genelde reddedilir; reddi acikla.
4) MainWindow.xaml'de DNS sekmesini "DNS Enum" olarak yeniden duzenle. 
   3 kart: Sorgu (tip dropdown), Zone Transfer (NS listesinden dene), Reverse Sweep.
5) Sonuclar DnsResultPanel'de ayri kartlarda.
6) Mevcut Services/'da DNS ile ilgili kod varsa migrasyon yap (kirma).
```

---

### 6) SNMP v1/v2c Walk & Community String Tarama

**Örnek:** Cihaz tarama bulduğu yazıcı/switch/router için UDP 161 açıksa `public`, `private` community ile SNMP walk yapar; sistem adı (`sysName.0`), açıklama (`sysDescr.0`), interface listesi (`ifTable`) dökülür.

**Neden Gerekli:** AGENTS.md'de SharpSnmpLib bağımlılığının düşürüldüğü görüldü ama SNMP, ağ envanteri için temel araç. Yöneticisiz kalmış switch/printer'ları tespit etmek için kritik (özellikle "public" community açıksa).

**AI Ajan Prompt:**
```
AgTarama'ya SNMP v1/v2c walk araci ekle (NuGet'siz, manuel BER kodlama).

1) Services/SnmpService.cs:
   - Manuel ASN.1 BER encoding/decoding (SEQUENCE, INTEGER, OCTET STRING, OBJECT IDENTIFIER).
   - GetAsync(string ip, string community, string oid, CancellationToken ct)
   - WalkAsync(string ip, string community, string rootOid, CancellationToken ct) 
     -> IAsyncEnumerable<SnmpVariable>. GETNEXT loop, oid root ile baslamayinca dur.
   - UDP 161, 2sn timeout, 3 retry.
2) Yaygin OID'leri sabitle (Constants/SnmpOids.cs):
   sysDescr=1.3.6.1.2.1.1.1.0, sysName=1.3.6.1.2.1.1.5.0, 
   sysUpTime=1.3.6.1.2.1.1.3.0, ifTable=1.3.6.1.2.1.2.2.1.
3) Yeni "SNMP" sekmesi. Input: IP, Community (default "public"), OID (default sysDescr). 
   "Walk" butonu (ifTable'i yurur).
4) Community Brute-Force: ["public","private","cisco","admin","manager"] - SADECE kullanici onaylar ise.
5) Cihaz Tara entegrasyon: 161/udp acik bulundugunda otomatik sysName + sysDescr cek, 
   DataGrid'e ekle.
```

---

### 7) SMB / Windows Paylaşımı Enumerasyon

**Örnek:** Bir IP için `\\10.0.0.5\` üzerindeki paylaşımları listeler (`net view` benzeri), null session ile kullanıcı listesi, paylaşım izinleri (anonymous erişim açık mı?), SMB sürümü (v1 EOL uyarısı).

**Neden Gerekli:** İç ağ pentestlerinin en bilinen vektörü açık SMB paylaşımları (WannaCry/EternalBlue izleri). SMBv1'in hâlâ açık olduğu sistemleri görmek değerli.

**AI Ajan Prompt:**
```
AgTarama'ya "SMB Enum" araci ekle.

1) Services/SmbEnumService.cs:
   - ListSharesAsync(string ip, CancellationToken ct): Process.Start("net.exe", $"view \\\\{ip}")
     stdout'u parse et. Async output okuma (BeginOutputReadLine yerine ProcessStartInfo + 
     RedirectStandardOutput + ReadToEndAsync).
   - DetectSmbVersionAsync(string ip): TCP 445 e SMB Negotiate Protocol Request (raw byte). 
     Cevaptaki Dialect'e bak: 0x0202=SMB1, 0x0210=SMB2.x, 0x0311=SMB3.1.1.
   - SMBv1 tespit edilirse SARI uyari.
2) UNC Path erisim testi: Directory.Exists($@"\\{ip}\IPC$") ile null session basariyi tahmin et.
3) Yeni sekme "SMB" + SmbResultPanel.
4) Cihaz Tara: 445 acik IP'lerde otomatik SMB versiyonu cek, "Servis" kolonuna yazdir.
5) ETIK NOT: Sadece yetkili aglarda kullanim disclaimer'i.
```

---

### 8) HTTP Path / Dizin Tarama (Web Discovery)

**Örnek:** Hedef URL `http://192.168.1.1` için `admin/`, `backup/`, `config.php`, `.git/`, `robots.txt`, `web.config` gibi 200 maddelik bir liste denenir; HTTP 200 dönenler vurgulanır.

**Neden Gerekli:** Kameralar, router'lar ve IoT cihazlarda çoğu zaman gizli kalmış yönetim sayfaları, yedek dosyalar olur. Dirb/Gobuster eşdeğeri minimal bir araç bu açığı kapatır.

**AI Ajan Prompt:**
```
AgTarama'ya HTTP "Path Discovery" araci ekle.

1) Resources/CommonPaths.txt: 200 yaygin yol (admin, login, robots.txt, .git/HEAD, 
   .env, web.config, phpMyAdmin/, wp-admin/, cgi-bin/, backup.zip vs.).
   File project resource olsun, CopyToOutputDirectory=PreserveNewest.
2) Services/PathScannerService.cs:
   async IAsyncEnumerable<PathHit> ScanAsync(string baseUrl, CancellationToken ct).
   HttpClient (HttpClientHandler { AllowAutoRedirect=false }), HEAD onceligi, GET fallback. 
   SemaphoreSlim(20). PathHit: Path, Status, ContentLength, ContentType.
3) 200/301/302/401/403 sonuclarini panele yaz, 404'leri filtrele (UI checkbox: "404'leri goster").
4) Yeni sekme "Path Tara" + WebPathResultPanel (DataGrid).
5) Export: CSV/JSON.
6) Rate limit: kullanici "saniyede istek" slider'i ile (1-50 arasi).
```

---

### 9) ARP Spoofing Tespiti & Pasif Ağ Gözlemi

**Örnek:** Arka planda 30sn'de bir ARP tablosunu okur; aynı MAC'in birden fazla IP'ye atandığı veya bir IP'nin MAC'inin sürekli değiştiği durumları "olası ARP spoofing" uyarısı olarak gösterir.

**Neden Gerekli:** Saldırgan ortada (MitM) saldırılarının erken tespiti için pratik bir savunma aracı. Mevcut ARP sekmesi statik snapshot veriyor; bu özellik *değişimi* takip eder.

**AI Ajan Prompt:**
```
AgTarama'ya "ARP Watcher" arka plan servisi + UI ekle.

1) Services/ArpWatcherService.cs:
   - StartAsync(TimeSpan interval, CancellationToken ct): IAsyncEnumerable<ArpAnomaly>
   - Her interval'de `arp -a` ciktisini parse et (mevcut NetworkTools.cs'deki ARP parser'i 
     refaktor edip kullan; duplikasyon olmasin).
   - Onceki snapshot ile karsilastir. Anomaliler:
       1) DuplicateMac (ayni MAC, farkli IP)
       2) MacChanged (ayni IP, farkli MAC)
       3) GatewayMacChanged (default gateway MAC'i degisti -> KRITIK)
2) Yeni sekme "ARP Watcher". Bas/Dur butonu, interval slider (10-300sn), 
   anomali listesi (DataGrid).
3) Yeni anomali geldiginde ToastGoster + Windows toast (varsa). 
   LogService'e WARNING seviyesinde yaz.
4) Default gateway MAC'i UI'de sabit gosterilsin; degisirse satir KIRMIZI.
5) Uygulama kapanirken cancellation token tetiklensin.
```

---

### 10) Bant Genişliği Geçmişi & Grafiği

**Örnek:** Mevcut bant genişliği sekmesinde anlık Tx/Rx rakam olarak görünüyor. Üstüne son 5/15/60 dakikayı kapsayan bir line chart + en yüksek/ortalama hız + per-application breakdown (varsa, `netstat -bo`).

**Neden Gerekli:** Anlık rakam kullanışsız; trend ve pik tespiti gerekiyor. Anormal trafik (örn. exfiltration, P2P) ancak grafikle göze çarpar.

**AI Ajan Prompt:**
```
AgTarama Bant Genisligi sekmesini gelistir.

1) Services/BandwidthHistoryService.cs:
   - Circular buffer (in-memory) 3600 sample (1 saat).
   - Adapter bazli BytesReceived/BytesSent farkini saniyede orneklemek icin DispatcherTimer 
     (mevcut Bandwidth.cs'de var; ondan event yayinla).
2) UI: ScottPlot veya OxyPlot NuGet'i EKLEME -> bunlar yerine WPF Canvas + Path/PolyLine ile 
   minimal cizim. X ekseni zaman, Y ekseni Mbps.
3) 3 zaman dilimi butonu: 5dk / 15dk / 60dk. 
4) Sag panelde: Peak Up/Down, Avg Up/Down, Total transferred (MB).
5) "Per-app trafik" (opsiyonel): Process.Start("netstat", "-bo") parse -> top 5 process listele. 
   YALNIZCA admin ise (UAC tespit), yoksa "Yonetici hakki gerekli" uyarisi.
```

---

### 11) Wi-Fi / WLAN Tarama & Sinyal Haritası

**Örnek:** `netsh wlan show networks mode=bssid` çıktısını parse eder; SSID, BSSID, sinyal gücü (RSSI), kanal, şifreleme (WEP/WPA2/WPA3) listelenir. Aynı SSID'nin farklı BSSID ile birden fazla göründüğü durumlar (Evil Twin) işaretlenir.

**Neden Gerekli:** Cihaz Tara tüm Ethernet odaklı. Saha mühendisleri için kablosuz tarama da gerekli; özellikle "rogue access point" tespiti.

**AI Ajan Prompt:**
```
AgTarama'ya "Wi-Fi Tara" sekmesi ekle.

1) Services/WlanService.cs:
   - ScanAsync(): netsh wlan show networks mode=bssid -> parse.
   - Cikti: SSID, BSSID, Authentication, Encryption, Signal (%), Channel, RadioType.
   - Native WLAN API'si (wlanapi.dll) ile P/Invoke OPSIYONEL ileri seviye.
2) Yeni sekme "Wi-Fi" + WlanResultPanel (DataGrid).
3) Yenile butonu, otomatik refresh checkbox (10sn).
4) WEP / Open ag -> KIRMIZI; WPA -> SARI; WPA2/WPA3 -> NORMAL.
5) "Evil Twin tespit": ayni SSID farkli BSSID -> SARI ikon. 
6) Cihaz uzerinde Wi-Fi adaptoru yoksa sekme grayed-out + aciklayici tooltip.
```

---

### 12) Rapor PDF Üretimi (Düzgün, HTML-string Değil)

**Örnek:** Cihaz Tara sonucu için "PDF Rapor" butonu → kapak sayfası (proje, tarih, operatör), özet (cihaz sayısı, tip dağılımı), detay tablo (DataGrid kolonları), grafik (cihaz tipi pasta), opsiyonel logo.

**Neden Gerekli:** Şu anki "PDF" export'u aslında HTML string concat — gerçek PDF değil. Ticari raporlama için profesyonel görünüm şart.

**AI Ajan Prompt:**
```
AgTarama'da Cihaz Tara PDF export'unu gercek PDF'e cevir.

1) QuestPDF NuGet paketi ekle (Community License, ucretsiz). 
   <PackageReference Include="QuestPDF" Version="2024.12.*" />
2) Services/PdfReportService.cs:
   - GenerateDeviceScanReportAsync(IEnumerable<Device> devices, string outputPath, 
     ReportMetadata meta, CancellationToken ct)
   - Sayfa duzeni: Header (logo + baslik), Footer (sayfa no + tarih), 
     Section 1: Ozet (toplam, tip kirilimi tablo), 
     Section 2: Detay tablo (IP, MAC, Vendor, Tip, Acik Portlar), 
     Section 3: Uyarilar (eski TLS, varsayilan kimlik, vb.)
3) MainWindow.DeviceScan.cs icindeki PDF export'unu yeni servise yonlendir; 
   eski HTML implementasyonunu sil.
4) Logo: Resources/logo.png (yoksa metin baslik).
5) Excel export'u da ayni anda gercek XLSX'e cevir: ClosedXML NuGet ile.
```

---

### 13) Etkileşimli Komut Console'u (Power-User Modu)

**Örnek:** F12 ile aşağıdan kayan bir console paneli açılır. Kullanıcı `ping 8.8.8.8 -c 5`, `scan 192.168.1.0/24 ports 22,80,443`, `snmp 192.168.1.1 public sysName` gibi tek satır komutlarla servisleri çağırır. Sonuç inline ya da ilgili sekmeye yönlendirilir.

**Neden Gerekli:** Birden fazla buton aramaktan tek satır komut yazmak daha hızlıdır. Power user'lar için ergonomi atlaması; aynı zamanda tüm servislerin tek bir API yüzeyinden çağrılabilirliğini test eder (mimari sağlık).

**AI Ajan Prompt:**
```
AgTarama'ya alttan acilan komut console'u ekle.

1) MainWindow.xaml'e en alta gizli Grid: Console (Height=200, Visibility=Collapsed). 
   F12 toggle.
2) Services/CommandRouter.cs:
   - Register(string name, Func<string[], CancellationToken, Task<string>> handler)
   - ExecuteAsync(string commandLine, CancellationToken ct) -> string (markdown ozet)
3) Baslangic komutlari: ping, port, traceroute, dns, arp, wol, scan, snmp, 
   ssl, banner, web, smb, help, clear, history.
4) Her komut, ilgili mevcut servisi cagirir (NetworkTools/DeviceScan/...). 
   Cikti console'a markdown formatinda yazilir.
5) Komut gecmisi: ok/asagi tuslari ile son 50 komut. 
   Tab tuslari ile autocomplete (komut adi).
6) "&&" ile zincirleme: ping 8.8.8.8 && dns google.com
```

---

### 14) Lisans Panelinde Trial / Süre Kalan Gösterimi & Hatırlatma

**Örnek:** Şu an lisans paneli "Aktif/Pasif" gösteriyor. Eklenir: kalan gün/saat, geçen 7 günde online doğrulama sayısı, son NTP zamanı, sona ermeye 7 gün kala UI'da sürekli uyarı banner'ı.

**Neden Gerekli:** Müşteri bilgilendirme; satış/yenileme döngüsü için gerekli görsel sinyal. Mevcut lisans altyapısı (LicenseService, TrustedTimeService) zaten bu veriyi tutuyor — sadece UI'a yansıtılmıyor.

**AI Ajan Prompt:**
```
AgTarama lisans panelini gelistir.

1) MainWindow.License.cs partial'inda mevcut UI'a ekle:
   - "Kalan: X gun Y saat" (LicenseService'den ExpiryDate, TrustedTimeService'den Now)
   - "Son online dogrulama: 2026-05-15 13:09 UTC"
   - "NTP zamani: ..." (TrustedTimeService.LastNtpTime)
   - "Cihaz: MachineId[ilk 8 karakter]..."
2) Kalan sure < 7 gun -> MainWindow'un en ustunde sticky banner 
   "Lisansiniz X gun icinde sona eriyor. [Yenile]"
3) Banner kapatma butonu (o oturum icin gizle).
4) "Lisans bilgilerini kopyala" butonu: clipboard'a destek mesaji icin ozet.
5) Hicbir yerde private key/secret expose etme; sadece expiry, machine id (truncated), 
   son dogrulama zamani.
```

---

## ÖNCELİK SIRASI ÖNERİSİ

Hızlı kazanım → Orta efor → Büyük efor:

1. **Hızlı (1-2 gün):** #12 (Gerçek PDF/XLSX), #14 (Lisans UI), #10 (Bant grafiği)
2. **Orta (3-5 gün):** #1 (SSL), #2 (Banner FP), #4 (IP Intel), #9 (ARP Watcher), #11 (Wi-Fi)
3. **Büyük (1-2 hafta):** #3 (Web panel + default cred), #5 (DNS Enum), #6 (SNMP), #7 (SMB), #8 (Path Discovery), #13 (Komut console)

## Dosya Konumu

Bu plan kullanıcının isteği üzerine ayrıca proje kökünde de saklanabilir:
`C:\Projects\AG TARAMA PROGRAMI\AgTarama\docs\new-features.md`

(Plan mode aktif olduğu için şimdilik sadece plan dosyasında. Onaylanırsa docs/ altına da kopyalanır.)
