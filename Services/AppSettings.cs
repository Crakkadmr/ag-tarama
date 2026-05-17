namespace AgTarama.Services;

public class AppSettings
{
    public int HedefMB { get; set; } = 16;
    public int TestSuresiSn { get; set; } = 2;
    public int PingTimeoutMs { get; set; } = 2000;
    public int PortTaramaConcurrency { get; set; } = 50;
    public int PortTaramaTimeoutMs { get; set; } = 1000;
    public int WlanAutoRefreshSeconds { get; set; } = 10;
    public int EvilTwinSinyalEsigi { get; set; } = 75; // 50-90 arasi gecerli

    public bool AiEnabled { get; set; } = true;
    public string AiSaglayici { get; set; } = "OpenRouter"; // OpenRouter|Google|OpenAI|Custom
    public string AiBaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string AiModel { get; set; } = "minimax/minimax-m2.5";
    public int AiGunlukTokenLimiti { get; set; } = 200_000;
    public int AiAylikTokenLimiti { get; set; } = 5_000_000;
    public bool AiYerelIpMaskele { get; set; } = false;

    public bool SesAcik { get; set; } = true;
    public bool ToastAcik { get; set; } = true;
}
