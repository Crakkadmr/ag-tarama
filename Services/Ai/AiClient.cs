using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgTarama.Services.Ai;

public sealed record AiChatMessage(string Role, string Content);
public sealed record AiTestResult(bool Success, string Message, int StatusCode = 0, long LatencyMs = 0);

public static class AiClient
{
    private const string FixedBaseUrl = "https://openrouter.ai/api/v1";
    private const string FixedModel = "minimax/minimax-m2.5";
    private static readonly HttpClient Http = CreateClient();

    public static async Task<AiTestResult> TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var apiKey = AiKeyVault.Load();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiTestResult(false, "API anahtari bulunamadi.");

        var messages = new List<AiChatMessage>
        {
            new("system", "Kisa cevap ver."),
            new("user", "ping")
        };

        var sw = Stopwatch.StartNew();
        var result = await SendChatRequestAsync(settings, apiKey, messages, maxTokens: 8, cancellationToken);
        sw.Stop();

        if (result.IsSuccess)
        {
            var modelInfo = string.IsNullOrWhiteSpace(result.Model) ? FixedModel : result.Model;
            return new AiTestResult(true, $"model: {modelInfo}", result.StatusCode, sw.ElapsedMilliseconds);
        }

        return new AiTestResult(false, result.ErrorMessage ?? "Bilinmeyen hata.", result.StatusCode, sw.ElapsedMilliseconds);
    }

    public static async Task<string> AskAsync(
        AppSettings settings,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<AiChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new AiChatMessage("system", systemPrompt));
        messages.Add(new AiChatMessage("user", userPrompt));

        var response = await ChatAsync(settings, messages, cancellationToken);
        return response;
    }

    public static async Task<string> ChatAsync(
        AppSettings settings,
        IReadOnlyList<AiChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var apiKey = AiKeyVault.Load();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API anahtari bulunamadi.");

        var result = await SendChatRequestAsync(settings, apiKey, messages, maxTokens: null, cancellationToken);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Content))
            throw new InvalidOperationException(result.ErrorMessage ?? "AI yaniti bos geldi.");

        if (result.Usage is not null)
            AiUsageMeter.AddUsage(result.Usage.PromptTokens, result.Usage.CompletionTokens);

        return result.Content;
    }

    private static async Task<AiRawResponse> SendChatRequestAsync(
        AppSettings settings,
        string apiKey,
        IReadOnlyList<AiChatMessage> messages,
        int? maxTokens,
        CancellationToken cancellationToken)
    {
        var url = BuildChatEndpoint(FixedBaseUrl);
        if (string.IsNullOrWhiteSpace(url))
            return AiRawResponse.Fail("Gecersiz AI base URL.");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = FixedModel,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            ["temperature"] = 0.2
        };

        if (maxTokens.HasValue)
            payload["max_tokens"] = maxTokens.Value;

        var json = JsonSerializer.Serialize(payload);
        const int maxRetry = 2;

        for (var attempt = 0; attempt <= maxRetry; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // OpenRouter icin tavsiye edilen basliklar.
            req.Headers.TryAddWithoutValidation("HTTP-Referer", "https://agtarama.local");
            req.Headers.TryAddWithoutValidation("X-Title", "AgTarama");

            try
            {
                using var resp = await Http.SendAsync(req, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                var status = (int)resp.StatusCode;

                if (resp.IsSuccessStatusCode)
                    return ParseSuccess(body, status);

                if (ShouldRetry(resp.StatusCode) && attempt < maxRetry)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(700 * (attempt + 1)), cancellationToken);
                    continue;
                }

                return AiRawResponse.Fail(FormatErrorMessage(status, body, apiKey), status);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxRetry)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), cancellationToken);
            }
            catch (Exception ex)
            {
                LogService.Hata("AiClient.SendChatRequestAsync", ex);
                return AiRawResponse.Fail($"AI istegi basarisiz: {ex.Message}");
            }
        }

        return AiRawResponse.Fail("AI istegi zaman asimina ugradi.");
    }

    private static AiRawResponse ParseSuccess(string body, int statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var model = root.TryGetProperty("model", out var modelEl) ? modelEl.GetString() : null;

            var content = "";
            if (root.TryGetProperty("choices", out var choicesEl) && choicesEl.ValueKind == JsonValueKind.Array && choicesEl.GetArrayLength() > 0)
            {
                var first = choicesEl[0];
                if (first.TryGetProperty("message", out var msgEl) &&
                    msgEl.TryGetProperty("content", out var contentEl))
                {
                    content = contentEl.GetString() ?? "";
                }
            }

            AiUsageData? usage = null;
            if (root.TryGetProperty("usage", out var usageEl))
            {
                usage = new AiUsageData
                {
                    PromptTokens = ReadInt(usageEl, "prompt_tokens"),
                    CompletionTokens = ReadInt(usageEl, "completion_tokens"),
                    TotalTokens = ReadInt(usageEl, "total_tokens")
                };
            }

            return new AiRawResponse(true, statusCode, content, model, null, usage);
        }
        catch (Exception ex)
        {
            LogService.Hata("AiClient.ParseSuccess", ex);
            return AiRawResponse.Fail("AI yaniti cozumlenemedi.", statusCode);
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static string BuildChatEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        return $"{baseUrl.Trim().TrimEnd('/')}/chat/completions";
    }

    private static int ReadInt(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var val) && val.TryGetInt32(out var i))
            return i;
        return 0;
    }

    private static string FormatErrorMessage(int statusCode, string body, string apiKey)
    {
        var cleaned = body.Replace("\r", " ").Replace("\n", " ").Trim();
        if (cleaned.Length > 180) cleaned = cleaned[..180] + "...";
        var masked = MaskApiKey(apiKey);
        return $"HTTP {statusCode} (key: {masked}) - {cleaned}";
    }

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "***";
        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 6) return "***";
        return $"{trimmed[..6]}***{trimmed[^4..]}";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AgTarama-AI/0.3.0");
        return client;
    }

    private sealed class AiRawResponse
    {
        public bool IsSuccess { get; }
        public int StatusCode { get; }
        public string Content { get; }
        public string? Model { get; }
        public string? ErrorMessage { get; }
        public AiUsageData? Usage { get; }

        public AiRawResponse(bool isSuccess, int statusCode, string content, string? model, string? errorMessage, AiUsageData? usage)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Content = content;
            Model = model;
            ErrorMessage = errorMessage;
            Usage = usage;
        }

        public static AiRawResponse Fail(string message, int statusCode = 0)
            => new(false, statusCode, "", null, message, null);
    }

    private sealed class AiUsageData
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
