using System;
using System.IO;

namespace AgTarama;

internal static class Paths
{
    public static readonly string AppBase =
        Path.GetDirectoryName(Environment.ProcessPath)
        ?? AppDomain.CurrentDomain.BaseDirectory;

    public static readonly string NpcapInstaller       = Path.Combine(AppBase, "Req", "npcap-1.88.exe");
    public static readonly string TsharkExe            = Path.Combine(AppBase, "tools", "WiresharkPortable64", "App", "Wireshark", "tshark.exe");
    public static readonly string WiresharkPortableExe = Path.Combine(AppBase, "tools", "WiresharkPortable64", "WiresharkPortable64.exe");
    public static readonly string SadpExe              = Path.Combine(AppBase, "tools", "sadp", "sadptool.exe");
    public static readonly string IpScannerExe         = Path.Combine(AppBase, "tools", "Ip_Scanner", "advanced_ip_scanner.exe");
    public static readonly string CapturesKlasor       = Path.Combine(AppBase, "captures");

    public static readonly string LogKlasor =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgTarama", "logs");
}
