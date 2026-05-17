namespace AgTarama.Services;

public class AppSettings
{
    public int    HedefMB               { get; set; } = 16;
    public int    TestSuresiSn          { get; set; } = 2;
    public int    PingTimeoutMs         { get; set; } = 2000;
    public int    PortTaramaConcurrency { get; set; } = 50;
    public int    PortTaramaTimeoutMs   { get; set; } = 1000;
    public int    WlanAutoRefreshSeconds { get; set; } = 10;
    public int    EvilTwinSinyalEsigi   { get; set; } = 75; // 50-90 arasi geçerli
    public bool   SesAcik               { get; set; } = true;
    public bool   ToastAcik             { get; set; } = true;
}
