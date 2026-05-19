using System;
using System.Collections.Generic;
using System.IO;

namespace AgTarama.Services;

/// <summary>
/// MAC OUI prefix → üretici eşlemesi. Önce Req/oui.csv (IEEE dökümü ~30k giriş)
/// lazy yüklenir; başarısız olursa BuiltInFallback (~100 yaygın OUI) devreye girer.
/// BulDetay ek olarak vendor için kaba türsel ipuçları (Kamera/Yazıcı/Mobil/...) döndürür.
/// </summary>
internal static class OuiVendorLookup
{
    public sealed record OuiBilgi(string Vendor, string? TurIpucu, bool Mobil);

    private static readonly Lazy<Dictionary<string, string>> _csv = new(YukleCsv);

    private static Dictionary<string, string> YukleCsv()
    {
        var sonuc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var yol = Path.Combine(AppContext.BaseDirectory, "Req", "oui.csv");
            if (!File.Exists(yol)) return sonuc;
            using var sr = new StreamReader(yol);
            string? satir = sr.ReadLine(); // başlık
            while ((satir = sr.ReadLine()) != null)
            {
                if (satir.Length < 10) continue;
                var alanlar = CsvSatirAyir(satir);
                if (alanlar.Count < 3) continue;
                var hex = alanlar[1].Trim();
                if (hex.Length != 6) continue;
                var prefix = $"{hex.Substring(0, 2)}:{hex.Substring(2, 2)}:{hex.Substring(4, 2)}".ToUpperInvariant();
                var vendor = alanlar[2].Trim();
                if (vendor.Length == 0) continue;
                sonuc[prefix] = KisaltVendor(vendor);
            }
        }
        catch { /* CSV bozuksa fallback kullan */ }
        return sonuc;
    }

    private static List<string> CsvSatirAyir(string satir)
    {
        var alanlar = new List<string>();
        var buf = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (var c in satir)
        {
            if (c == '"') { quoted = !quoted; continue; }
            if (c == ',' && !quoted) { alanlar.Add(buf.ToString()); buf.Clear(); continue; }
            buf.Append(c);
        }
        alanlar.Add(buf.ToString());
        return alanlar;
    }

    /// <summary>IEEE'deki resmi şirket adından tarama UI'sinde kullanılan kısa adı çıkar.</summary>
    private static string KisaltVendor(string s)
    {
        var v = s;
        // Yaygın ekleri at
        string[] kes = {
            ", Ltd.", ", Ltd", " Ltd.", " Ltd",
            " Innovation Limited", " Innovation",  // e.g. "Reolink Innovation Limited"
            " Limited",                              // e.g. "Some Company Limited"
            " Foundation",                           // e.g. "Raspberry Pi Foundation"
            ", Inc.", ", Inc", " Inc.", " Inc",
            ", LLC", " LLC",
            ", Co., Ltd.", " Co., Ltd.", " Co.,Ltd.", " Co.Ltd.", " Co. Ltd.", " Co Ltd", " Co.",
            " Corporation", " Corp.", " Corp",
            " GmbH & Co. KG", " GmbH", " AG", " S.A.", " S.p.A.", " B.V.", " N.V.",
            " Technology", " Technologies", " Electronics", " Electric", " Networks", " Network",
            " Communications", " Communication", " Systems", " System", " Solutions",
            " International", " (Shenzhen)", " (Shanghai)", " (Beijing)", " (HK)",
        };
        foreach (var k in kes)
        {
            int i = v.IndexOf(k, StringComparison.OrdinalIgnoreCase);
            if (i > 0) v = v.Substring(0, i);
        }
        v = v.Trim().Trim(',').Trim();
        if (v.Length > 32) v = v.Substring(0, 32).Trim();
        return v;
    }

    // BuiltInFallback (CSV yüklenmezse) — yaygın vendor'lar
    private static readonly Dictionary<string, string> Fallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["3C:22:FB"] = "Apple", ["A4:83:E7"] = "Apple", ["04:E5:36"] = "Apple",
        ["00:1B:63"] = "Apple", ["A8:51:5B"] = "Apple", ["BC:52:B7"] = "Apple",
        ["F0:B4:79"] = "Apple", ["DC:2B:2A"] = "Apple",
        ["00:12:FB"] = "Samsung", ["38:01:97"] = "Samsung", ["88:32:9B"] = "Samsung",
        ["3C:8B:FE"] = "Samsung", ["E8:50:8B"] = "Samsung", ["A0:0B:BA"] = "Samsung",
        ["C8:14:79"] = "Samsung", ["BC:8C:CD"] = "Samsung",
        ["28:6C:07"] = "Xiaomi", ["64:CC:2E"] = "Xiaomi", ["68:DF:DD"] = "Xiaomi",
        ["FC:64:BA"] = "Xiaomi", ["50:8F:4C"] = "Xiaomi",
        ["00:E0:FC"] = "Huawei", ["28:6E:D4"] = "Huawei", ["80:71:1F"] = "Huawei",
        ["C8:14:51"] = "Huawei",
        ["F4:F5:D8"] = "Google", ["F4:F5:E8"] = "Google", ["38:8B:59"] = "Google",
        ["3C:5A:B4"] = "Google",
        ["00:40:48"] = "Hikvision", ["28:57:BE"] = "Hikvision", ["44:19:B6"] = "Hikvision",
        ["BC:AD:28"] = "Hikvision", ["C0:51:7E"] = "Hikvision", ["F4:B7:E2"] = "Hikvision",
        ["3C:EF:8C"] = "Dahua", ["4C:11:BF"] = "Dahua", ["A0:BD:1D"] = "Dahua",
        ["E0:50:8B"] = "Dahua", ["08:ED:ED"] = "Dahua",
        ["00:40:8C"] = "Axis", ["AC:CC:8E"] = "Axis", ["00:0E:8E"] = "Axis",
        ["00:27:22"] = "Ubiquiti", ["04:18:D6"] = "Ubiquiti", ["24:5A:4C"] = "Ubiquiti",
        ["44:D9:E7"] = "Ubiquiti", ["68:72:51"] = "Ubiquiti", ["74:83:C2"] = "Ubiquiti",
        ["80:2A:A8"] = "Ubiquiti", ["F0:9F:C2"] = "Ubiquiti", ["FC:EC:DA"] = "Ubiquiti",
        ["DC:9F:DB"] = "Ubiquiti",
        ["00:0C:42"] = "MikroTik", ["4C:5E:0C"] = "MikroTik", ["6C:3B:6B"] = "MikroTik",
        ["B8:69:F4"] = "MikroTik", ["C4:AD:34"] = "MikroTik", ["E4:8D:8C"] = "MikroTik",
        ["DC:2C:6E"] = "MikroTik", ["08:55:31"] = "MikroTik",
        ["00:14:78"] = "TP-Link", ["A4:2B:B0"] = "TP-Link", ["50:C7:BF"] = "TP-Link",
        ["F4:F2:6D"] = "TP-Link", ["C0:25:E9"] = "TP-Link", ["98:DA:C4"] = "TP-Link",
        ["00:0A:41"] = "Cisco", ["00:1C:0F"] = "Cisco", ["54:78:1A"] = "Cisco",
        ["B0:8B:CF"] = "Cisco", ["D0:D0:FD"] = "Cisco",
        ["00:14:6C"] = "NETGEAR", ["20:E5:2A"] = "NETGEAR", ["44:94:FC"] = "NETGEAR",
        ["A0:04:60"] = "NETGEAR",
        ["00:1F:C6"] = "ASUS", ["1C:B7:2C"] = "ASUS", ["AC:9E:17"] = "ASUS",
        ["D8:50:E6"] = "ASUS", ["50:46:5D"] = "ASUS",
        ["00:13:46"] = "D-Link", ["00:24:01"] = "D-Link", ["F0:7D:68"] = "D-Link",
        ["00:11:32"] = "Synology",
        ["24:5E:BE"] = "QNAP", ["00:08:9B"] = "QNAP",
        ["24:0A:C4"] = "Espressif", ["30:AE:A4"] = "Espressif", ["3C:71:BF"] = "Espressif",
        ["94:B9:7E"] = "Espressif", ["A0:20:A6"] = "Espressif", ["BC:DD:C2"] = "Espressif",
        ["EC:FA:BC"] = "Espressif", ["7C:DF:A1"] = "Espressif",
        ["B8:27:EB"] = "Raspberry Pi", ["DC:A6:32"] = "Raspberry Pi", ["E4:5F:01"] = "Raspberry Pi",
        ["28:CD:C1"] = "Raspberry Pi", ["D8:3A:DD"] = "Raspberry Pi",
        ["00:0E:58"] = "Sonos", ["B8:E9:37"] = "Sonos",
        ["AC:63:BE"] = "Amazon", ["44:65:0D"] = "Amazon", ["F0:D2:F1"] = "Amazon",
        ["F0:27:2D"] = "Amazon",
        ["EC:71:DB"] = "Reolink",
        ["3C:46:D8"] = "TP-Link",  // IEEE assigns this to TP-LINK, not EZVIZ
        ["10:52:1C"] = "Tuya", ["50:02:91"] = "Tuya", ["DC:4F:22"] = "Tuya",
        ["00:24:BE"] = "Sony", ["FC:0F:E6"] = "Sony",
        ["00:1F:6B"] = "LG", ["A0:39:F7"] = "LG", ["3C:CD:93"] = "LG",
        ["00:50:56"] = "VMware", ["00:0C:29"] = "VMware",
    };

    public static string? Bul(string? mac)
    {
        if (!MacUtils.IsValidUnicast(mac)) return null; // rejects 00:00:00, broadcast, multicast
        var prefix = MacUtils.OuiPrefix(mac);
        if (prefix == null) return null;
        var db = _csv.Value;
        if (db.Count > 0 && db.TryGetValue(prefix, out var v)) return v;
        return Fallback.TryGetValue(prefix, out var f) ? f : null;
    }

    /// <summary>
    /// Vendor + kaba tür ipucu döndürür. Tür ipucu, vendor adında veya
    /// bilinen mobile/kamera/yazıcı tablosunda eşleşme bulunursa set edilir.
    /// </summary>
    public static OuiBilgi? BulDetay(string? mac)
    {
        var vendor = Bul(mac);
        if (vendor == null) return null;

        // IEEE'de bazı OUI'ler marka adı yerine ticari/eski isimle kayıtlı — normalize et.
        if (vendor.Contains("Routerboard", StringComparison.OrdinalIgnoreCase) ||
            vendor.Contains("Mikrotikls",  StringComparison.OrdinalIgnoreCase))
            vendor = "MikroTik";

        var v = vendor.ToLowerInvariant();
        string? tur = null;
        bool mobil = false;

        // Mobil markalar — gerçek belirleyici port pattern; biz yalnız flag set ederiz.
        if (v.Contains("apple") || v.Contains("samsung") || v.Contains("xiaomi") ||
            v.Contains("huawei") || v.Contains("oneplus") || v.Contains("oppo") ||
            v.Contains("vivo") || v.Contains("realme") || v.Contains("motorola") ||
            v.Contains("honor") || v.Contains("nokia mobile") || v.Contains("sony mobile"))
            mobil = true;

        if (v.Contains("hikvision") || v.Contains("dahua") || v.Contains("axis communication") ||
            v.Contains("uniview") || v.Contains("reolink") || v.Contains("ezviz") ||
            v.Contains("hanwha") || v.Contains("vivotek") || v.Contains("pelco") ||
            v.Contains("amcrest") || v.Contains("annke") || v.Contains("vstarcam"))
            tur = "Kamera";
        else if (v.Contains("hewlett") || v.Contains("hp inc") || v.Contains("epson") ||
                 v.Contains("brother industries") || v.Contains("canon") || v.Contains("kyocera") ||
                 v.Contains("xerox") || v.Contains("lexmark") || v.Contains("ricoh") ||
                 v.Contains("zebra technologies"))
            tur = "Yazıcı";
        else if (v.Contains("ubiquiti") || v.Contains("mikrotik") || v.Contains("tp-link") ||
                 v.Contains("netgear") || v.Contains("d-link") || v.Contains("zyxel") ||
                 v.Contains("ruijie") || v.Contains("h3c") || v.Contains("cisco") ||
                 v.Contains("juniper") || v.Contains("aruba") || v.Contains("fortinet"))
            tur = "Router/AP";
        else if (v.Contains("synology") || v.Contains("qnap") || v.Contains("asustor"))
            tur = "NAS";
        else if (v.Contains("sonos"))
            tur = "Hoparlör";
        else if (v.Contains("espressif") || v.Contains("tuya") || v.Contains("shelly") ||
                 v.Contains("tasmota"))
            tur = "Akıllı Cihaz";
        else if (v.Contains("raspberry"))
            tur = "Linux IoT";
        else if (v.Contains("vmware") || v.Contains("microsoft") || v.Contains("parallels") ||
                 v.Contains("virtualbox"))
            tur = "Bilgisayar";
        else if (mobil)
            tur = "Telefon";

        return new OuiBilgi(vendor, tur, mobil);
    }

}
