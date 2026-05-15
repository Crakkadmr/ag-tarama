using System;
using System.Collections.Generic;

namespace AgTarama.Services;

/// <summary>
/// MAC OUI prefix → üretici eşlemesi. Advanced IP Scanner DB'si yoksa veya
/// MAC bulunamadığında devreye girer. ~100 yaygın OUI.
/// </summary>
internal static class OuiVendorLookup
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Apple
        ["3C:22:FB"] = "Apple",  ["A4:83:E7"] = "Apple",  ["04:E5:36"] = "Apple",
        ["00:1B:63"] = "Apple",  ["A8:51:5B"] = "Apple",  ["BC:52:B7"] = "Apple",
        ["F0:B4:79"] = "Apple",  ["DC:2B:2A"] = "Apple",
        // Samsung
        ["00:12:FB"] = "Samsung", ["38:01:97"] = "Samsung", ["88:32:9B"] = "Samsung",
        ["3C:8B:FE"] = "Samsung", ["E8:50:8B"] = "Samsung", ["A0:0B:BA"] = "Samsung",
        ["C8:14:79"] = "Samsung", ["BC:8C:CD"] = "Samsung",
        // Xiaomi
        ["28:6C:07"] = "Xiaomi", ["64:CC:2E"] = "Xiaomi", ["68:DF:DD"] = "Xiaomi",
        ["FC:64:BA"] = "Xiaomi", ["50:8F:4C"] = "Xiaomi",
        // Huawei
        ["00:E0:FC"] = "Huawei", ["28:6E:D4"] = "Huawei", ["80:71:1F"] = "Huawei",
        ["C8:14:51"] = "Huawei",
        // Google
        ["F4:F5:D8"] = "Google", ["F4:F5:E8"] = "Google", ["38:8B:59"] = "Google",
        ["3C:5A:B4"] = "Google",
        // Hikvision
        ["00:40:48"] = "Hikvision", ["28:57:BE"] = "Hikvision", ["44:19:B6"] = "Hikvision",
        ["BC:AD:28"] = "Hikvision", ["C0:51:7E"] = "Hikvision", ["F4:B7:E2"] = "Hikvision",
        // Dahua
        ["3C:EF:8C"] = "Dahua", ["4C:11:BF"] = "Dahua", ["A0:BD:1D"] = "Dahua",
        ["E0:50:8B"] = "Dahua", ["08:ED:ED"] = "Dahua",
        // Axis Communications
        ["00:40:8C"] = "Axis", ["AC:CC:8E"] = "Axis", ["00:0E:8E"] = "Axis",
        // Ubiquiti
        ["00:27:22"] = "Ubiquiti", ["04:18:D6"] = "Ubiquiti", ["24:5A:4C"] = "Ubiquiti",
        ["44:D9:E7"] = "Ubiquiti", ["68:72:51"] = "Ubiquiti", ["74:83:C2"] = "Ubiquiti",
        ["80:2A:A8"] = "Ubiquiti", ["F0:9F:C2"] = "Ubiquiti", ["FC:EC:DA"] = "Ubiquiti",
        ["DC:9F:DB"] = "Ubiquiti",
        // MikroTik
        ["00:0C:42"] = "MikroTik", ["4C:5E:0C"] = "MikroTik", ["6C:3B:6B"] = "MikroTik",
        ["B8:69:F4"] = "MikroTik", ["C4:AD:34"] = "MikroTik", ["E4:8D:8C"] = "MikroTik",
        ["DC:2C:6E"] = "MikroTik", ["08:55:31"] = "MikroTik",
        // TP-Link
        ["00:14:78"] = "TP-Link", ["A4:2B:B0"] = "TP-Link", ["50:C7:BF"] = "TP-Link",
        ["F4:F2:6D"] = "TP-Link", ["C0:25:E9"] = "TP-Link", ["98:DA:C4"] = "TP-Link",
        // Cisco
        ["00:0A:41"] = "Cisco", ["00:1C:0F"] = "Cisco", ["54:78:1A"] = "Cisco",
        ["B0:8B:CF"] = "Cisco", ["D0:D0:FD"] = "Cisco",
        // NETGEAR
        ["00:14:6C"] = "NETGEAR", ["20:E5:2A"] = "NETGEAR", ["44:94:FC"] = "NETGEAR",
        ["A0:04:60"] = "NETGEAR",
        // ASUS
        ["00:1F:C6"] = "ASUS", ["1C:B7:2C"] = "ASUS", ["AC:9E:17"] = "ASUS",
        ["D8:50:E6"] = "ASUS", ["50:46:5D"] = "ASUS",
        // D-Link
        ["00:13:46"] = "D-Link", ["00:24:01"] = "D-Link", ["F0:7D:68"] = "D-Link",
        // Synology
        ["00:11:32"] = "Synology",
        // QNAP
        ["24:5E:BE"] = "QNAP", ["00:08:9B"] = "QNAP",
        // Espressif (ESP32 / ESP8266 — IoT cihazlar)
        ["24:0A:C4"] = "Espressif", ["30:AE:A4"] = "Espressif", ["3C:71:BF"] = "Espressif",
        ["94:B9:7E"] = "Espressif", ["A0:20:A6"] = "Espressif", ["BC:DD:C2"] = "Espressif",
        ["EC:FA:BC"] = "Espressif", ["7C:DF:A1"] = "Espressif",
        // Raspberry Pi Foundation
        ["B8:27:EB"] = "Raspberry Pi", ["DC:A6:32"] = "Raspberry Pi", ["E4:5F:01"] = "Raspberry Pi",
        ["28:CD:C1"] = "Raspberry Pi", ["D8:3A:DD"] = "Raspberry Pi",
        // Sonos
        ["00:0E:58"] = "Sonos", ["B8:E9:37"] = "Sonos",
        // Amazon
        ["AC:63:BE"] = "Amazon", ["44:65:0D"] = "Amazon", ["F0:D2:F1"] = "Amazon",
        ["F0:27:2D"] = "Amazon",
        // Reolink
        ["EC:71:DB"] = "Reolink",
        // Ezviz
        ["3C:46:D8"] = "EZVIZ",
        // Tuya (IoT)
        ["10:52:1C"] = "Tuya", ["50:02:91"] = "Tuya", ["DC:4F:22"] = "Tuya",
        // Sony
        ["00:24:BE"] = "Sony", ["FC:0F:E6"] = "Sony",
        // LG
        ["00:1F:6B"] = "LG", ["A0:39:F7"] = "LG", ["3C:CD:93"] = "LG",
        // VMware (sanal)
        ["00:50:56"] = "VMware", ["00:0C:29"] = "VMware",
    };

    public static string? Bul(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var temiz = mac.Replace("-", ":").Trim().ToUpperInvariant();
        if (temiz.Length < 8) return null;
        var prefix = temiz[..8]; // "AA:BB:CC"
        return Map.TryGetValue(prefix, out var marka) ? marka : null;
    }
}
