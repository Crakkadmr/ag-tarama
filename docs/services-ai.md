# AI Servisleri (`Services/Ai/`)

Tüm AI özellikleri OpenAI-uyumlu `chat/completions` API üzerine kurulur. Varsayılan sağlayıcı: OpenRouter, model `deepseek/deepseek-v4-flash`.

## AiProvider.cs

Sağlayıcı preset listesi.

| Id | Display | BaseUrl | DefaultModel |
|---|---|---|---|
| OpenRouter | OpenRouter | `https://openrouter.ai/api/v1` | `deepseek/deepseek-v4-flash` |
| Google | Google AI | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-2.0-flash` |
| OpenAI | OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| Custom | Özel | (kullanıcı girer) | (kullanıcı girer) |

```csharp
static AiProviderPreset GetById(string? id)  // bulunamazsa Presets[0] döner
```

## AiKeyVault.cs

API anahtarını DPAPI + AES-CBC/HMAC-SHA256 (machine-bound) ile şifreli saklar. Yer: `%APPDATA%\AgTarama\ai.vault`.

```csharp
static void Save(string apiKey)
static string? Load()      // null = anahtar yok / bozuk
static void Clear()
static bool HasKey()
```

## AiDefaultKey.cs

XOR-obfuscated varsayılan OpenRouter anahtarı. `MainWindow()` constructor'da `AiKeyVault.EnsureDefaultKey()` ile vault boşsa otomatik yüklenir. Maliyet kontrolü `AiUsageMeter` kotalarıyla.

## AiClient.cs

OpenAI-uyumlu `chat/completions` HTTP istemcisi.

```csharp
static Task<string> AskAsync(AppSettings settings, string systemPrompt, string userPrompt, CancellationToken)
static Task<string> ChatAsync(AppSettings settings, IReadOnlyList<AiChatMessage> messages, CancellationToken)
static Task<AiTestResult> TestConnectionAsync(AppSettings settings, string? explicitApiKey, CancellationToken)

// AiChatMessage: record(Role, Content)
// AiTestResult:  record(Success, Message, StatusCode, LatencyMs)
```

**Davranış:**
- `ChatAsync` öncesinde `AiEnabled`, günlük/aylık token limiti kontrolü → aşılırsa `InvalidOperationException`.
- Retry: max 2; 429 / 5xx için `700ms * (attempt+1)` bekleme.
- `Timeout = 60s`, `User-Agent = AgTarama-AI/0.4.0`.
- Hata mesajlarında API anahtarı `sk-or-***last4` maskelenir.
- Başarılı çağrı sonrası `AiUsageMeter.AddUsage(promptTokens, completionTokens)`.
- **İptal:** `cancellationToken` set edilmişse `OperationCanceledException` çağırana propagate edilir (yutulmaz).
- BaseUrl/Model `AppSettings.AiBaseUrl` / `AppSettings.AiModel` üzerinden okunur; fallback `AiProvider.GetById(settings.AiSaglayici)`.

## AiUsageMeter.cs

Günlük/aylık token sayacı. Yer: `%APPDATA%\AgTarama\ai.usage.json`.

```csharp
static AiUsageSnapshot Load()
static void AddUsage(int promptTokens, int completionTokens)
```

- Periyot rollover: yeni gün/ay başında sıfırlanır.
- `Load()` ve `AddUsage()` aynı `_lock` nesnesi altında — paralel AI isteklerinde race condition yok.

## AiPrompts.cs

```csharp
const string SohbetSystemPrompt   // Chatbot serbest sohbet — TR ağ asistanı
const string PcapSystemPrompt     // Pcap tshark istatistik analizi
const string CihazSystemPrompt    // Cihaz listesi analizi — KRITIK/ORTA/DUSUK
```

## AiPcapAnalyzer.cs

tshark istatistiklerini toplayıp AI'ya gönderir.

```csharp
static Task<string> AnalyzeAsync(string pcapPath, AppSettings settings, CancellationToken)
```

- tshark komutları: `-z conv,ip`, `-z io,stat,1`, `-z io,phs`, `-z endpoints,ip`, `-z http,tree`, `-z dns,tree`.
- Her çıktı max 50 satıra kırpılır; toplam payload ≤ ~30KB.
- `AiYerelIpMaskele=true` ise private IP 3. oktet → `x` (`192.168.1.42` → `192.168.x.42`).
- **Process cleanup:** `WaitForExitAsync(ct)` sonrası finally bloğunda `Kill(entireProcessTree: true)` + `WaitForExitAsync(CancellationToken.None)`. stdout/stderr paralel drainlenir (buffer dolma blokajı önlenir).

## AiDeviceAnalyzer.cs

Cihaz Tara sonuçlarını AI'ya gönderir.

```csharp
sealed record CihazDto(Ip, Ad, Tur, Marka, Model, Ping, Portlar, Kesif, Mac, Uretici, Servis, Guven)
static Task<string> AnalyzeAsync(IReadOnlyList<CihazDto> cihazlar, string talep, AppSettings settings, CancellationToken)
static readonly IReadOnlyList<Preset> Presetler  // 5 hazır preset
```

Preset'ler: 🛡️ Güvenlik riski, 📷 Kamera/NVR/DVR, 📡 AP/Router/Switch, ❓ Bilinmeyen cihaz, 🔍 Sonraki tarama önerisi.

Max 50 cihaz JSON; fazlası `"...ve N daha"` notu ile.

## UI Entegrasyonu

- **Chatbot:** `Partials/MainWindow.Ai.cs > AiSoruGonderAsync` — DockPanel altta `AiInputBox`. Detay: [ui.md > AI Modu](ui.md).
- **Pcap AI:** `YakalamaKartiOlustur > Tamamla` delegesi → "✨ AI ile analiz et" butonu → `AiPcapAnalyzer.AnalyzeAsync` → `MesajEkle("sonuc", ...)` + `HistoryService.Kaydet("AI ANALIZ", ...)`.
- **Cihaz AI:** `KameraAiBtn` → `AiDeviceReportWindow` modal — preset chip'ler, `_cts` ile iptal yönetimi, IP tespiti varsa yeniden tarama callback'i.
- **F12 AI önerisi:** Ctrl+Tab → `_aiOneriCts` ile mevcut isteği iptal edip yenisini başlat; Esc tümünü iptal eder.

## AppSettings AI Alanları

```csharp
bool   AiEnabled            = true;
string AiSaglayici          = "OpenRouter";              // OpenRouter|Google|OpenAI|Custom
string AiBaseUrl            = "https://openrouter.ai/api/v1";
string AiModel              = "deepseek/deepseek-v4-flash";
int    AiGunlukTokenLimiti  = 200_000;
int    AiAylikTokenLimiti   = 5_000_000;
bool   AiYerelIpMaskele     = false;
// API anahtarı ASLA AppSettings'e yazılmaz — sadece AiKeyVault.
```
