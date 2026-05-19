using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AgTarama.Services;

internal static class MacUtils
{
    public static string? Normalize(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;

        // Cisco dot notation (e.g. 0000.0000.0000) → strip dots, treat as hex
        var cleaned = Regex.Replace(mac.Trim(), @"[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (cleaned.Length != 12) return mac.Trim();

        // Format as XX:XX:XX:XX:XX:XX
        var sb = new StringBuilder(17);
        for (int i = 0; i < 12; i += 2)
        {
            if (i > 0) sb.Append(':');
            sb.Append(cleaned[i]);
            sb.Append(cleaned[i + 1]);
        }
        return sb.ToString();
    }

    public static string? OuiPrefix(string? mac)
    {
        var n = Normalize(mac);
        return n is { Length: >= 8 } ? n[..8] : null; // XX:XX:XX
    }

    /// <summary>
    /// Returns true only for globally-administered unicast MACs.
    /// Rejects: null/empty, all-zero, broadcast (FF:FF:FF:FF:FF:FF),
    /// multicast (LSB of first octet = 1), locally-administered (bit 1 of first octet = 1).
    /// </summary>
    public static bool IsValidUnicast(string? mac)
    {
        var n = Normalize(mac);
        if (n == null || n.Length != 17) return false;
        if (n == "00:00:00:00:00:00") return false;
        if (!byte.TryParse(n[..2], NumberStyles.HexNumber, null, out var b0)) return false;
        // bit 0 (multicast) or bit 1 (locally-administered) → reject
        if ((b0 & 0x03) != 0) return false;
        return true;
    }
}
