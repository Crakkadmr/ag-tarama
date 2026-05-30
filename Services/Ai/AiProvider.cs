namespace AgTarama.Services.Ai;

public sealed record AiProviderPreset(
    string Id,
    string DisplayName,
    string BaseUrl,
    string DefaultModel);

public static class AiProvider
{
    public const string OpenRouter = "OpenRouter";
    public const string Google = "Google";
    public const string OpenAi = "OpenAI";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<AiProviderPreset> Presets =
    [
        new(OpenRouter, "OpenRouter", "https://openrouter.ai/api/v1", "deepseek/deepseek-v4-flash"),
        new(Google, "Google AI", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-2.0-flash"),
        new(OpenAi, "OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
        new(Custom, "Ozel", "", "")
    ];

    public static AiProviderPreset GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Presets[0];

        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        return Presets[0];
    }
}
