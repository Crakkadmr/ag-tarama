# AI Modu — Uygulama Planı

> AgTarama v0.3.0 üzerine eklenecek AI modu için hazırlanmış uygulama planıdır. Onay sonrası faz faz uygulanır.

---

## Context

AgTarama (v0.3.0) şu an statik bir ağ tarayıcısı: kullanıcı sonuçları yorumlamak için kendisi düşünmek zorunda. Programa **AI modu** ekleniyor ki:
- Paket yakalama sonrası AI **pcap'i analiz edip** "internet yavaşlığı, bant sömüren cihaz, problem çıkaran trafik" tespit etsin.
- Cihaz Tara sonuçlarına AI ile **daha kapsamlı analiz** ya da **hazır eylem önerileri** çıkarsın.
- Chatbot tab'ının altında **AI ile serbest sohbet** kutusu olsun.
- Geliştirici API anahtarı **şimdilik default** olarak kullanılacak — bu nedenle anahtar güvenliği kritik.

Bu plan; sağlayıcı, prompt mimarisi, anahtar kasası, UI değişiklikleri ve yeni AI özelliklerini tek başlık altında topluyor.

---

## Sağlayıcı Seçimi — Çoklu / OpenAI-Uyumlu

Kullanıcı kararı: "OpenRouter veya Google AI; hangi API key girilirse çalışsın."

**Strateji:** Tüm bu sağlayıcıların **OpenAI-uyumlu `chat/completions` endpoint'i** vardır. Tek `AiClient`, OpenAI mesaj formatıyla konuşur; sağlayıcı = **(BaseUrl + Model + ApiKey)** üçlüsü.

Hazır preset'ler (Ayarlar > AI > Sağlayıcı dropdown):

| Preset | BaseUrl | Önerilen default model | Anahtar formatı |
|---|---|---|---|
| **OpenRouter** (default) | `https://openrouter.ai/api/v1` | `google/gemini-2.0-flash-exp:free` | `sk-or-v1-…` |
| **Google AI Studio** | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-2.0-flash` | `AIza…` |
| **OpenAI** | `https://api.openai.com/v1` | `gpt-4o-mini` | `sk-…` |
| **Özel (Custom)** | (kullanıcı yazar) | (kullanıcı yazar) | (her format kabul) |

Endpoint tek: `POST {BaseUrl}/chat/completions` — header `Authorization: Bearer {ApiKey}`.

Kullanıcı **herhangi bir** OpenAI-uyumlu API anahtarı yapıştırırsa çalışır (Custom preset). Sağlayıcı doğrulaması "Test Et" butonu ile yapılır.

> Anthropic için ayrı endpoint formatı gerekir; bu PR'da kapsam dışı (sonradan `AiClient`'a `IChatProvider` interface'i ile eklenebilir).

---

## Mimari Özet

Yeni klasör: `Services/Ai/`

| Dosya | Sorumluluk |
|---|---|
| `Services/CryptoHelper.cs` | LicenseService'ten **çıkarılan** AES-CBC+HMAC helper (yeniden kullanım). LicenseService onu çağıracak. |
| `Services/Ai/AiKeyVault.cs` | API anahtarını DPAPI + AES (machine-bound) ile şifreli sakla/oku. Plain text **hiçbir yere** yazılmaz. |
| `Services/Ai/AiClient.cs` | `HttpClient` + OpenAI-uyumlu `chat/completions` çağrısı (OpenRouter / Google / OpenAI / Custom), retry, rate-limit handling. |
| `Services/Ai/AiProvider.cs` | Preset list (OpenRouter, Google, OpenAI, Custom) + her preset için default model + base URL. |
| `Services/Ai/AiPrompts.cs` | Tüm system prompt'lar TR olarak burada (pcap, cihaz tara, sohbet). |
| `Services/Ai/AiPcapAnalyzer.cs` | tshark istatistik komutlarını koşturur → AiClient'a gönderir. |
| `Services/Ai/AiDeviceAnalyzer.cs` | `KameraBilgi` listesi JSON → AiClient. |
| `Services/Ai/AiUsageMeter.cs` | Günlük/aylık token sayacı + maliyet tahmini. |
| `Partials/MainWindow.Ai.cs` | UI olayları (chat input, "✨ AI ile analiz et" butonları). |

Tüm AI çağrıları **`async/await + CancellationToken`** + `Dispatcher.InvokeAsync` ile UI thread'i bloklamayacak.

---

## 1. API Anahtarı Güvenliği

### 1.1 Anahtar saklama — `AiKeyVault`

İki katmanlı koruma:

1. **DPAPI** (`ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`) — Windows kullanıcısına bağlı, anahtar başka makinede / başka kullanıcıda açılamaz.
2. **AES-CBC + HMAC-SHA256** (machine-bound) — `CryptoHelper`'a refactor edilen LicenseService deseninin aynısı.

```csharp
// Yaklaşık API
public static class AiKeyVault
{
    public static void Save(string apiKey);     // DPAPI(AES_HMAC(plain))
    public static string? Load();               // null = anahtar yok / bozuk
    public static void Clear();
    public static bool HasKey();                // ekranlarda durum göstermek için
}
```

Saklama yolu: `%APPDATA%\AgTarama\ai.vault` (binary, opak).

### 1.2 Geliştirici "default" anahtarı

Kullanıcı talebi: "şimdilik benim api keyim kullanılacak". Yaklaşım:

- Anahtar **kaynak kodda plain** olarak **bulunmaz**.
- `Services/Ai/AiDefaultKey.cs` içinde **XOR-obfuscated** byte array (Supabase anon key deseninin aynısı — `LicenseService.cs:26-59`).
- İlk açılışta `AiKeyVault.HasKey() == false` ise default anahtar `AiKeyVault.Save(...)` ile makine-spesifik şifreli kasaya yazılır; bellekten silinir.
- **Obfuscar** (`obfuscar.xml`) Release build'de string'leri zaten karıştırıyor — `AiDefaultKey` sınıfını `IncludeTypes` listesine ekle.
- README/AGENTS'a not: "Default anahtar geçicidir; kullanıcı kendi anahtarını Ayarlar > AI'dan girebilir, sıfırlayabilir."

### 1.3 Network güvenliği

- `HttpClient` `Timeout = 60s`.
- `User-Agent: "AgTarama-AI/0.3.0"`.
- TLS varsayılan (.NET 10 = TLS 1.2/1.3).
- API anahtarı **log'a yazılmaz** (`LogService.Hata`'da prompt/response trim'li yazılır).
- Hata mesajlarında `Authorization` header **mask'lenir** (`sk-…***last4`).

### 1.4 Rate limit / maliyet koruması

`AiUsageMeter` (`%APPDATA%\AgTarama\ai.usage.json`):
- Günlük token bütçesi (default 200K input + 50K output).
- Aylık token bütçesi (default 5M input + 1M output).
- Aşılırsa istek **kullanıcı onayı** isteyen modal ile (`Bugünkü kullanım sınırı aşıldı, devam etmek istiyor musunuz?`).

---

## 2. Özellik #1 — Pcap AI Analizi

### Akış

1. Kullanıcı yakalama başlatır → `MainWindow.Capture.cs:103` (tamamlanma noktası) callback'i çalışır.
2. Yakalama tamamlanma kartına yeni buton: **"✨ AI ile analiz et"**.
3. Tıklanınca `AiPcapAnalyzer.AnalyzeAsync(pcapPath, CancellationToken)`:
   - **tshark istatistikleri** koşulur (raw pcap göndermek yerine — boyut ve gizlilik):
     - `tshark -r <pcap> -q -z conv,ip` → IP konuşmalar (top talkers)
     - `tshark -r <pcap> -q -z io,stat,1` → saniye bazında IO
     - `tshark -r <pcap> -q -z io,phs` → protokol hiyerarşisi
     - `tshark -r <pcap> -q -z endpoints,ip` → endpoint listesi
     - `tshark -r <pcap> -q -z http,tree` → HTTP isteği özeti (varsa)
     - `tshark -r <pcap> -q -z dns,tree` → DNS sorgu özeti
   - Çıktılar **trimlenip** (üst 50 satır) tek string'e birleştirilir.
4. AI'ya gönderilen prompt (system + user):

   **System (TR):** "Sen bir ağ trafiği analistisin. Aşağıda tshark istatistik çıktıları verilecek. Şunları tespit et: (1) en çok bant kullanan IP'ler (sömürücü), (2) anormal trafik desenleri, (3) DNS / HTTP'de şüpheli istekler, (4) muhtemel internet yavaşlığı nedeni. Cevabı kısa, maddeler halinde, Türkçe ver. JSON formatında değil, okunabilir metin."

   **User:** İstatistik blokları + yakalama süresi + adapter adı.

5. Yanıt `ChatPanel`'e `MesajEkle("sonuc", aiMetin)` ile **"🤖 AI Analizi"** başlığıyla yazılır.
6. History kaydı: `HistoryService.Kaydet("AI ANALIZ", pcapDosyaAdi, ozet, satirlar, ...)`.

### Veri gizliliği

- **Payload boyutu** ≤ 30 KB tutulur (istatistik çıktıları kısaltılır).
- Raw pcap **hiçbir zaman** dışarı gönderilmez.
- Yerel IP'ler maskelenebilir (opsiyonel: `192.168.1.42` → `192.168.x.42`) — başlangıçta kapalı, ayar olarak eklenir.

---

## 3. Özellik #2 — Cihaz Tara AI Butonu

### Akış

1. `MainWindow.xaml:811-819` civarına Tara/Durdur grubu yanına **"✨ AI"** butonu eklenir (`KameraAiBtn`).
2. Tarama bitmeden disabled; bittikten sonra enabled.
3. Tıklayınca açılan **küçük popup/Flyout** (`Window` veya inline `Popup`):

   Hazır komut seçenekleri (preset chip'ler):
   - "🛡️ Güvenlik riski olan cihazları işaretle"
   - "📷 Kamera/NVR/DVR listesi çıkar"
   - "📡 Wi-Fi AP / router / switch tipi cihazları grupla"
   - "❓ Bilinmeyen cihazlar için ek sorgu önerileri ver"
   - "🔍 Her cihaz için sonraki tarama önerisi (port, SNMP, ONVIF)"
   - "✏️ Kendi sorum (serbest metin)" — TextBox aç

4. Seçilince `AiDeviceAnalyzer.AnalyzeAsync(devices, preset, CancellationToken)`:
   - `_kameraBilgileri.Values` → JSON serialize (max 50 cihaz, fazlası "...ve N daha" notu).
   - Prompt: System TR + user = preset talimatı + JSON cihaz listesi.
5. Yanıt **yeni modal pencerede** açılır: `AiDeviceReportWindow.xaml` (koyu tema, mevcut `DarkCheckBox`/`PingInputBox` stilleri ile tutarlı). İçerik: başlık + AI yanıtı (Markdown benzeri basit render) + altta `[📋 Kopyala]`, `[💾 TXT kaydet]`, `[🔁 Yeniden sor]`, `[Kapat]` butonları.
6. "Bu cihazları yeniden tara" gibi önerilerde **aksiyon butonu** eklenebilir (`TekIpTaraAsync(ip)` zaten mevcut — `MainWindow.DeviceScan.cs:~975`). AI yanıtında **IP listesi tespit edilirse** modalın altına "Bu IP'leri yeniden tara" butonu eklenir.

---

## 4. Özellik #3 — Chatbot Alt Kısmında AI Input

### XAML değişikliği

`MainWindow.xaml:847-856` (`TabChatbot` içeriği) yapısı:

```
TabItem TabChatbot
└── Grid (Row 0=ChatPanel, Row 1=AI Input — yeni)
    ├── ScrollViewer (Row 0)
    │   └── StackPanel x:Name="ChatPanel"
    └── Grid (Row 1) — yeni AI input bar
        ├── TextBox x:Name="AiInputBox"  (PingInputBox stiliyle)
        ├── Button  x:Name="AiGonderBtn" (PrimaryButton, "✨ Sor")
        └── Button  x:Name="AiTemizleBtn" (ChipButton, son sohbet bağlamını sil)
```

Mevcut `<StackPanel x:Name="ChatPanel"/>` `Grid.Row="0"` altına alınır.

### Akış

1. Kullanıcı yazar → `AiGonderBtn` tıklar veya `Enter`.
2. `MesajEkle("kullanici", soru)` ile chat'e yazılır.
3. `AiClient.ChatAsync(history, soru, ct)` — son N (default 6) mesaj **rolü ile** beraber gönderilir.
4. Yanıt streaming değil (basit `chat/completions` endpoint). Cevap geldiğinde `MesajEkle("sonuc", "🤖 " + yanit)`.
5. "Yazılıyor..." indicator (`MesajEkle("sistem", "⏳ AI düşünüyor...")` ekle, gelince sil).
6. **Bağlam:** `_aiSohbetGecmisi : List<(string Rol, string Icerik)>` — yeni alan `MainWindow.xaml.cs`.
7. `AiTemizleBtn` → bu listeyi temizler ("Yeni sohbet").

### System prompt (TR)

> "Sen Network Sniffer (AgTarama) uygulamasında çalışan bir ağ asistanısın. Kullanıcının ağ taraması, paket yakalama, ping/port/wifi/Cihaz Tara sonuçlarıyla ilgili sorularını Türkçe, kısa ve teknik olarak yanıtla. Uygulamanın özelliklerini biliyorsun: Paket Yakalama, Ping, Port Tara, Traceroute, DNS, ARP, WoL, Bant Genişliği, Cihaz Tara (ONVIF/SSDP/mDNS/SNMP/Ubiquiti/MikroTik), Wi-Fi (Evil-Twin tespiti), F12 konsolu. Gerekirse hangi sekmeden / butondan çalıştırılacağını söyle. Komut çalıştırmıyorsun, sadece yol gösteriyorsun."

---

## 5. Ek AI Modu Fikirleri

| # | Özellik | Tetikleyici | Faydası |
|---|---|---|---|
| A | **Akıllı port yorumcusu** | Port Tara sonrası "✨ Yorumla" | Açık portların ne olduğu, riskleri, kapatma önerisi |
| B | **Wi-Fi güvenlik raporu** | Wi-Fi tab "✨ Rapor" | WEP/Open/WPA versiyon dağılımı + Evil-Twin tespiti yorumu |
| C | **Bant darboğaz tespiti** | Bant grafik üstü "✨ Analiz" | Son N saniyenin peak/avg/trend yorumu |
| D | **Cihaz yeniden adlandırma önerisi** | Cihaz Tara sağ tık | OUI + banner + servisten "muhtemel rol" çıkarımı |
| E | **PDF rapor sihirbazı** | Export sırasında "✨ Yöneticiye özet ekle" | QuestPDF rapora AI yazılı yönetici özeti |
| F | **Komut tamamlayıcı** | F12 konsolda Tab+`?` | "ping 192… ile ne yapmak istiyorsun?" — komut önerisi |
| G | **Konuşmalı subnet seçimi** | Cihaz Tara'da "Hangi subnet'i tarayayım?" | NIC'ten doğal dilde subnet türetme |

**Kullanıcı kararı:** Bu PR'a **A, B, F** dahil edilecek. C/D/E/G sonraki PR'a bırakılır.

**A — Akıllı port yorumcusu** (`MainWindow.NetworkTools.cs` Port sweep sonrası):
- Port sonuç paneline `KameraPortAiBtn` ("✨ Yorumla") eklenir; tarama bitince enabled.
- `AiClient.AskAsync` prompt: "{ip} adresinde {portliste} açık. Her port için: servis adı, tipik kullanım, risk seviyesi (düşük/orta/yüksek), kapatma/sertleştirme önerisi. Türkçe, kısa, maddeler."
- Yanıt aynı port panelinde `PortKutucugaYaz` ile gösterilir; 6. mesaj kartı olarak eklenir.

**B — Wi-Fi güvenlik raporu** (`MainWindow.Wlan.cs` tarama sonrası):
- Wi-Fi tab başlık satırına `WlanAiBtn` ("✨ Rapor") eklenir.
- `_wlanSatirlar` koleksiyonundan SSID/BSSID/Auth/Encryption/Signal/Channel/EvilTwin JSON üretilir.
- AI'dan: WPA2/3 vs WEP/Open dağılımı, Evil-Twin şüphelilerinin gerçekten riskli olup olmadığı, kanal çakışmaları, "sömüren" güçlü sinyalli açık AP'ler. Modal'da göster (aynı `AiDeviceReportWindow` template'i yeniden kullanılır).

**F — F12 komut tamamlayıcı** (`Partials/MainWindow.Console.cs`):
- `ConsoleInput_KeyDown` içinde mevcut Tab autocomplete'ten **sonra** `Ctrl+Tab` veya `?` + Enter binding'i eklenir (eski Tab davranışı bozulmaz).
- Girilen kısmi metin → `AiClient.AskAsync` prompt: "Kullanıcı F12 konsolunda '{metin}' yazdı. Tahminen ne yapmak istiyor? En olası 3 tam komutu öner. Komutlar: help, clear, history, ping, dns, port, traceroute, arp, wol, scan, ssl, banner, web, smb, snmp."
- Yanıt `KonsoleYaz`'a sarı renkte yazılır; kullanıcı `↑` ile geçmişten seçer gibi öneriden seçebilir.

---

## 6. Settings UI Değişikliği

`SettingsWindow.xaml` (yeni "AI" section, mevcut Wi-Fi section pattern'i):

```
─── AI ───────────────────────────────
Sağlayıcı:        [▾ OpenRouter | Google AI | OpenAI | Özel]
API Anahtarı:     [********or-v1]  [Değiştir] [Sıfırla]
Model:            [google/gemini-2.0-flash-exp:free          ]
Base URL:         [https://openrouter.ai/api/v1              ]  (preset değişince auto-fill)
Günlük token sınırı: [200000]
Aylık token sınırı:  [5000000]
Yerel IP maskele:    [☐]  (gönderirken 192.168.x.x'e dönüştür)
☑ AI özelliklerini etkinleştir
Test bağlantısı:     [Test Et] → ✓ "model: gemini-2.0-flash, 142 ms" / ✗ "401: invalid api key"
```

Sağlayıcı dropdown'u "Özel" seçilince **Model** ve **Base URL** kutuları kullanıcıya açık (editable); preset seçilince auto-fill.

`AppSettings.cs` yeni alanlar:

```csharp
public bool   AiEnabled            { get; set; } = true;
public string AiSaglayici          { get; set; } = "OpenRouter";     // OpenRouter|Google|OpenAI|Custom
public string AiBaseUrl            { get; set; } = "https://openrouter.ai/api/v1";
public string AiModel              { get; set; } = "google/gemini-2.0-flash-exp:free";
public int    AiGunlukTokenLimiti  { get; set; } = 200_000;
public int    AiAylikTokenLimiti   { get; set; } = 5_000_000;
public bool   AiYerelIpMaskele     { get; set; } = false;
// API anahtarı AppSettings'e ASLA yazılmaz — sadece AiKeyVault'ta.
```

---

## 7. Değiştirilecek / Eklenecek Dosyalar

### Yeni dosyalar
- `Services/CryptoHelper.cs` (LicenseService'ten refactor)
- `Services/Ai/AiProvider.cs` (preset listesi)
- `Services/Ai/AiClient.cs`
- `Services/Ai/AiKeyVault.cs`
- `Services/Ai/AiDefaultKey.cs` (XOR-obfuscated OpenRouter default key)
- `Services/Ai/AiPrompts.cs`
- `Services/Ai/AiPcapAnalyzer.cs`
- `Services/Ai/AiDeviceAnalyzer.cs`
- `Services/Ai/AiUsageMeter.cs`
- `Partials/MainWindow.Ai.cs`
- `AiDeviceReportWindow.xaml` + `.cs` (Cihaz Tara modalı — A/B için de yeniden kullanılır)

### Değiştirilecek
- `Services/LicenseService.cs` — `EncryptAesHmac` / `DecryptAesHmac` → `CryptoHelper.cs`'e taşı, çağrı güncelle.
- `Services/AppSettings.cs` — yukarıdaki yeni alanlar.
- `SettingsWindow.xaml` / `.cs` — AI section.
- `MainWindow.xaml` — chatbot tab Grid yapısı (Row 0/1), Cihaz Tara `KameraAiBtn`, yakalama kartında AI butonu için template güncellemesi.
- `MainWindow.xaml.cs` — `_aiSohbetGecmisi` alanı.
- `Partials/MainWindow.Capture.cs` — `YakalamaKartiOlustur` tuple'ına AI buton callback ekle (`Tamamla` sonrası gözükecek).
- `Partials/MainWindow.NetworkTools.cs` — Port sonuç paneline `KameraPortAiBtn` (Özellik A).
- `Partials/MainWindow.Wlan.cs` — Wi-Fi tab başlık alanına `WlanAiBtn` (Özellik B).
- `Partials/MainWindow.Console.cs` — F12 komut tamamlayıcı binding (Özellik F).
- `obfuscar.xml` — `AiDefaultKey`, `AiKeyVault` sınıflarını `IncludeTypes` listesine ekle.
- `AGENTS.md` + `docs/services.md` + `docs/ui.md` — AI modu açıklaması (kullanıcı "md güncelle" dediğinde).

---

## 8. Yeniden Kullanılan Mevcut Yardımcılar

| Mevcut | Yer | Nasıl kullanılacak |
|---|---|---|
| `MesajEkle(tur, metin)` | `Partials/MainWindow.NetworkTools.cs:26` | AI yanıtlarını chat'e yazma |
| `ToastGoster(mesaj, hata)` | `Partials/MainWindow.UI.cs:101+` | "Anahtar yok", "Limit aşıldı" bildirimleri |
| `HistoryService.Kaydet(...)` | `Services/HistoryService.cs` | AI analiz kayıtları |
| `LicenseService.EncryptAesHmac/DecryptAesHmac` | `Services/LicenseService.cs:330-363` | `CryptoHelper`'a refactor edilip yeniden kullanılacak |
| `LicenseService.GetMachineId()` | `Services/LicenseService.cs:298` | Machine-bound key üretimi |
| `Paths.TsharkExe` | `Paths.cs:13` | Pcap istatistik komutları |
| `HttpClient` deseni | `Services/UpdateService.cs:31` | `AiClient`'taki HTTP client |
| Mevcut stiller (`PingInputBox`, `PrimaryButton`, `ChipButton`, `DarkCheckBox`) | `MainWindow.xaml` | AI UI elemanları |
| `MasterCts` | `MainWindow.xaml.cs` | AI CancellationToken'larını linkle |

---

## 9. Doğrulama

**Build:**
```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build -c Debug
dotnet build -c Release
```
0 uyarı / 0 hata zorunlu.

**Manuel testler:**
1. **Anahtar kasası:**
   - Ayarlar > AI > Anahtar Değiştir → `sk-or-v1-…` gir → kaydet → `%APPDATA%\AgTarama\ai.vault` binary oluşmalı, plain text içermemeli.
   - Anahtar Sıfırla → dosya silinmeli, "AI etkinleştirmek için anahtar gir" mesajı.
2. **Test Et butonu:** Anahtar girildikten sonra sağlayıcıya küçük `ping` isteği → ✓ / ✗ göstermeli.
3. **Pcap analiz:** Kısa (10 sn) yakalama yap → Tamamla kartında "✨ AI ile analiz et" görünmeli → tıkla → ChatPanel'e Türkçe analiz yazılmalı, History'ye kayıt eklenmeli.
4. **Cihaz Tara AI:** /24 tara → KameraAiBtn aktifleşmeli → "Güvenlik riski" preset → modal'da Türkçe rapor gelmeli.
5. **Chat input:** AiInputBox'a "192.168.1.1 ile ping testi nasıl yaparım?" yaz → Enter → AI sekme yönlendirmesi vermeli.
6. **Rate limit:** `AiGunlukTokenLimiti = 100` (geçici) yap → 2-3 istek sonrası "Bugünkü sınır aştın" modalı çıkmalı.
7. **İptal:** Yanıt beklerken sekme değiştir / `Esc` → `CancellationToken` ile istek iptal olmalı.
8. **Hata yolu:** Yanlış API key → kullanıcıya "anahtar geçersiz", **gerçek anahtar log'a yazılmamalı**.

**Güvenlik denetimi:**
- `findstr /S "sk-or-v1" bin\Release\*.dll` → eşleşme **olmamalı** (Obfuscar + XOR sayesinde).
- `LogService` çıktısında anahtar substring'i geçmemeli.
- ai.vault hex dump'ı 16 byte IV + ciphertext + 32 byte HMAC formatına uymalı, anahtar gözükmemeli.

---

## 10. Aşamalama

1. **Faz 1 (alt yapı):** `CryptoHelper`, `AiKeyVault`, `AiProvider`, `AiClient`, `AiUsageMeter`, `AppSettings`, `SettingsWindow` AI section, Test Et butonu. **Doğrulama:** Test Et 200 OK döner.
2. **Faz 2 (chat input):** `MainWindow.Ai.cs`, XAML Grid değişikliği, sohbet geçmişi. **Doğrulama:** İki tur soru-cevap çalışır.
3. **Faz 3 (pcap analiz):** `AiPcapAnalyzer`, capture kartına buton. **Doğrulama:** Gerçek pcap üzerinde analiz çıktısı.
4. **Faz 4 (cihaz analiz + modal):** `AiDeviceAnalyzer`, `AiDeviceReportWindow`, KameraAiBtn ve preset popup. **Doğrulama:** /24 tara → preset → modal'da rapor + "IP'leri yeniden tara" aksiyonu.
5. **Faz 5 (A/B/F):** Port yorumcusu (`MainWindow.NetworkTools.cs`), Wi-Fi raporu (`MainWindow.Wlan.cs` — `AiDeviceReportWindow` yeniden kullanır), F12 komut tamamlayıcı (`MainWindow.Console.cs`).

Her faz **ayrı commit** ile bitebilir; ara release şart değil — tüm fazlar bittiğinde **v0.4.0** olarak yayımlanır (kullanıcı "release yap" dediğinde).

---

## 11. Notlar

- v0.3.0'da eklenen `DarkChip`/`DarkCheckBox` stilleri AI modal ve butonlarında **mevcut tema tutarlılığı** için yeniden kullanılır.
- AI hata senaryoları (anahtar yok, ağ yok, 429 rate limit, 5xx) için `ToastGoster` + chat'e kırmızı `MesajEkle("hata", …)`.
- Plan onaylandı; uygulama "Faz 1'den başla" denildiğinde devreye girer.
