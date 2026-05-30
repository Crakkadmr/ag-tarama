namespace AgTarama.Services.Discovery;

internal sealed class ScanOptions
{
    public bool DeepScan              { get; set; } = false;
    public bool LiveMode              { get; set; } = false;
    public bool SkipDeadHosts         { get; set; } = true;   // ARP ile bulunmayan host'ları TCP'de atla
    public int[] Ports                { get; set; } = DefaultPorts;
    public int ConcurrencyLimit       { get; set; } = 80;
    public int PingTimeoutMs          { get; set; } = 600;    // 1000 → 600 ms
    public int PortTimeoutMs          { get; set; } = 450;    // 800 → 450 ms
    public int ArpTimeoutMs           { get; set; } = 3000;
    public int ListenerDurationMs     { get; set; } = 8000;
    public int LiveRefreshIntervalMs  { get; set; } = 30_000;
    public int LiveOfflineThresholdMs { get; set; } = 90_000;

    public static readonly int[] DefaultPorts =
    {
        21,   22,   23,   53,   80,   135,  139,  443,  445,  515,
        554,  631,  1883, 1900, 3389, 5000, 5060, 5357, 7547, 8000,
        8080, 8443, 9000, 9100, 34567, 37777,
    };
}
