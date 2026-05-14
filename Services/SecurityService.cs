using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace AgTarama.Services;

public static class SecurityService
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    // Reverse engineering araçlarının process adları
    private static readonly string[] BlockedTools =
    [
        "dnspy", "ilspy", "de4dot", "x64dbg", "x32dbg",
        "ollydbg", "windbg", "ida", "ida64", "radare2",
        "ghidra", "dotpeek", "justdecompile", "reflector"
    ];

    /// <summary>
    /// Release build'de çağrılır. Tehdit tespit edilirse uygulamayı sonlandırır.
    /// </summary>
    public static void Dogrula()
    {
#if DEBUG
        return; // Geliştirme ortamında kontrol yapma
#endif
        if (DebuggerVarMi())
            Sonlandir("Hata kodu: 0xE_DBG");

        if (AnalizAraciVarMi())
            Sonlandir("Hata kodu: 0xE_ENV");
    }

    // ─── Çağrılabilir yardımcılar ────────────────────────────────────────────

    public static bool DebuggerVarMi()
    {
        // .NET managed debugger
        if (Debugger.IsAttached) return true;

        // Windows API — kernel düzeyi debugger
        if (IsDebuggerPresent()) return true;

        // Uzak debugger
        bool remote = false;
        try
        {
            CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote);
        }
        catch { }

        return remote;
    }

    public static bool AnalizAraciVarMi()
    {
        try
        {
            var running = Process.GetProcesses()
                .Select(p => { try { return p.ProcessName.ToLowerInvariant(); } catch { return ""; } });

            return running.Any(name =>
                BlockedTools.Any(tool => name.Contains(tool)));
        }
        catch
        {
            return false;
        }
    }

    // ─── Private ─────────────────────────────────────────────────────────────

    private static void Sonlandir(string kod)
    {
        MessageBox.Show(
            $"Uygulama beklenmedik bir ortamda çalıştığı için kapatılıyor.\n{kod}",
            "Güvenlik Hatası",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Environment.FailFast(kod);
    }
}
