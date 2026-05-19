using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgTarama.Services;
using AgTarama.Services.Discovery.Classification;
using AgTarama.Services.Discovery.Models;

namespace AgTarama;

/// <summary>
/// Kanıt tabanlı + ağırlıklı skorlu cihaz sınıflandırıcı.
/// </summary>
public partial class MainWindow
{
    // ── Vendor adı normalizasyonu ────────────────────────────────────────
    private static string MarkaNormalize(string marka)
    {
        var m = marka.Trim();
        if (m.Length == 0) return "Bilinmiyor";
        var lower = m.ToLowerInvariant();
        if (lower.Contains("hikvision"))     return "Hikvision";
        if (lower.Contains("dahua"))         return "Dahua";
        if (lower.Contains("axis"))          return "Axis";
        if (lower.Contains("reolink"))       return "Reolink";
        if (lower.Contains("ezviz"))         return "EZVIZ";
        if (lower.Contains("uniview"))       return "Uniview";
        if (lower.Contains("hanwha") || lower.Contains("samsung techwin")) return "Hanwha";
        if (lower.Contains("ubiquiti") || lower == "ubnt" || lower.Contains("unifi") || lower.Contains("airos"))
                                              return "Ubiquiti";
        if (lower.Contains("mikrotik") || lower.Contains("routeros") || lower.Contains("routerboard") || lower.Contains("mikrotikls")) return "MikroTik";
        if (lower.Contains("tp-link") || lower == "tplink") return "TP-Link";
        if (lower.Contains("d-link") || lower == "dlink")   return "D-Link";
        if (lower.Contains("netgear"))       return "NETGEAR";
        if (lower.Contains("asus"))          return "ASUS";
        if (lower.Contains("cisco"))         return "Cisco";
        if (lower.Contains("aruba"))         return "Aruba";
        if (lower.Contains("hewlett") || lower.Contains("hp inc") ||
            lower.Contains("hp http") || lower.Contains("laserjet") ||
            lower.Contains("jetdirect"))     return "HP";
        if (lower.Contains("epson"))         return "Epson";
        if (lower.Contains("brother"))       return "Brother";
        if (lower.Contains("canon"))         return "Canon";
        if (lower.Contains("kyocera"))       return "Kyocera";
        if (lower.Contains("xerox"))         return "Xerox";
        if (lower.Contains("apple"))         return "Apple";
        if (lower.Contains("samsung"))       return "Samsung";
        if (lower.Contains("xiaomi"))        return "Xiaomi";
        if (lower.Contains("huawei"))        return "Huawei";
        if (lower.Contains("google"))        return "Google";
        if (lower.Contains("amazon"))        return "Amazon";
        if (lower.Contains("synology"))      return "Synology";
        if (lower.Contains("qnap"))          return "QNAP";
        if (lower.Contains("sonos"))         return "Sonos";
        if (lower.Contains("raspberry"))     return "Raspberry Pi";
        if (lower.Contains("espressif"))     return "Espressif";
        if (lower.Contains("vmware"))        return "VMware";
        if (lower.Contains("microsoft") || lower.Contains("iis") || lower.Contains("windows"))
                                              return "Windows";
        return m;
    }

    // ── Ana sınıflandırma ────────────────────────────────────────────────
    private static CihazKimlik KimlikBelirleV2(DeviceInfo b)
    {
        var turL   = new List<TurAdayi>();
        var markaL = new List<MarkaAdayi>();

        KanitTopla_HttpFp(b, turL, markaL);
        KanitTopla_Ubiquiti(b, turL, markaL);
        KanitTopla_MikroTik(b, turL, markaL);
        KanitTopla_Snmp(b, turL, markaL);
        KanitTopla_Onvif(b, turL, markaL);
        KanitTopla_Wsd(b, turL, markaL);
        KanitTopla_Ssdp(b, turL, markaL);
        KanitTopla_Mdns(b, turL, markaL);
        KanitTopla_Netbios(b, turL, markaL);
        KanitTopla_Llmnr(b, turL, markaL);
        KanitTopla_Smb(b, turL, markaL);
        KanitTopla_Ssh(b, turL, markaL);
        KanitTopla_OuiMac(b, turL, markaL);
        KanitTopla_PortPattern(b, turL, markaL);
        KanitTopla_Banner(b, turL, markaL);
        KanitTopla_Ttl(b, turL, markaL);
        KanitTopla_AdHostname(b, turL, markaL);

        var turGruplu = turL
            .GroupBy(x => (x.Tur, x.Kaynak))
            .Select(g => g.OrderByDescending(x => x.Agirlik).First())
            .ToList();
        var markaGruplu = markaL
            .GroupBy(x => (MarkaNormalize(x.Marka), x.Kaynak))
            .Select(g => g.OrderByDescending(x => x.Agirlik).First())
            .ToList();

        var turSira = turGruplu
            .GroupBy(x => x.Tur)
            .Select(g => (Tur: g.Key, Skor: g.Sum(x => x.Agirlik)))
            .OrderByDescending(x => x.Skor).ToList();
        var markaSira = markaGruplu
            .GroupBy(x => MarkaNormalize(x.Marka))
            .Select(g => (Marka: g.Key, Skor: g.Sum(x => x.Agirlik)))
            .OrderByDescending(x => x.Skor).ToList();

        var k = new CihazKimlik();
        if (turSira.Count > 0 && turSira[0].Skor >= KanitAgirlik.MinKararEsigi)
            k.Tur = turSira[0].Tur;
        if (markaSira.Count > 0 && markaSira[0].Skor >= KanitAgirlik.MinKararEsigi)
            k.Marka = markaSira[0].Marka;

        k.Model   = ModelSec(b);
        k.TurIkon = TurIkonSec(k.Tur);

        b.KararIzi = new KimlikKararIzi(
            turGruplu, markaGruplu,
            turSira.Select(x => (x.Tur, x.Skor)).ToList(),
            markaSira.Select(x => (x.Marka, x.Skor)).ToList());

        return k;
    }

    private static string TurIkonSec(string tur) => tur switch
    {
        "Kamera"           => "◎",
        "NVR/DVR"          => "▣",
        "Bilgisayar"       => "▢",
        "Linux IoT"        => "▣",
        "NAS"              => "▦",
        "Sunucu"           => "▤",
        "Güvenlik Duvarı"  => "⊞",
        "Erişim Noktası"   => "⊛",
        "Router"           => "⊛",
        "Router/AP"        => "⊛",
        "Router/Switch"    => "⊛",
        "Switch/AP"        => "◫",
        "Switch"           => "◫",
        "Telefon"          => "⊡",
        "Tablet"           => "▭",
        "Yazıcı"           => "▤",
        "Tarayıcı"         => "▤",
        "Akıllı TV"        => "▣",
        "Apple TV"         => "▣",
        "Akıllı Cihaz"     => "◈",
        "Hoparlör"         => "◐",
        "Müzik Cihazı"     => "◐",
        _                  => "◈",
    };

    internal static string KararIziOzetle(KimlikKararIzi? iz)
    {
        if (iz == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append("Tür: ");
        sb.Append(string.Join(", ", iz.TurSiralama.Take(3).Select(x => $"{x.Tur}({x.Skor})")));
        sb.Append(" | Marka: ");
        sb.Append(string.Join(", ", iz.MarkaSiralama.Take(3).Select(x => $"{x.Marka}({x.Skor})")));
        sb.Append(" | Kanıt: ");
        var turKanit   = iz.TurAdaylari.Select(t => (Skor: t.Agirlik, Yazim: $"+{t.Agirlik} {t.Kaynak}={t.Tur}"));
        var markaKanit = iz.MarkaAdaylari.Select(m => (Skor: m.Agirlik, Yazim: $"+{m.Agirlik} {m.Kaynak}→{m.Marka}"));
        var enYuksek = turKanit.Concat(markaKanit)
            .OrderByDescending(x => x.Skor).Take(5).Select(x => x.Yazim);
        sb.Append(string.Join("; ", enYuksek));
        return sb.ToString();
    }

    private static string? ModelSec(DeviceInfo b)
        => IlkDolu(
            b.HttpFpModel,
            b.UbntPlatform,
            b.MikroTikBoard,
            b.SsdpModelName,
            b.SsdpModelNumber,
            b.OnvifHardware,
            AnlamliSayfaBasligi(b.SayfaBasligi));

    // ── Kanıt toplayıcıları ──────────────────────────────────────────────

    private static void KanitTopla_HttpFp(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.HttpFpMarka) && string.IsNullOrWhiteSpace(b.HttpFpTur)) return;
        int skor = !string.IsNullOrWhiteSpace(b.HttpFpModel)
            ? KanitAgirlik.HttpFpVendorWithModel
            : KanitAgirlik.HttpFpProbeOnly;
        var detay = $"{b.HttpFpMarka}/{b.HttpFpTur} {b.HttpFpModel}".Trim();
        if (!string.IsNullOrWhiteSpace(b.HttpFpTur))
            turL.Add(new TurAdayi(b.HttpFpTur, skor, KanitKaynak.HttpFp, detay));
        if (!string.IsNullOrWhiteSpace(b.HttpFpMarka))
            markaL.Add(new MarkaAdayi(b.HttpFpMarka, skor, KanitKaynak.HttpFp, detay));
    }

    private static void KanitTopla_Ubiquiti(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.UbntPlatform) && string.IsNullOrWhiteSpace(b.UbntHostname)) return;
        markaL.Add(new MarkaAdayi("Ubiquiti", KanitAgirlik.UbiquitiMarka, KanitKaynak.Ubiquiti,
            b.UbntPlatform ?? b.UbntHostname!));
        var platform = (b.UbntPlatform ?? "").ToLowerInvariant();
        string tur = "Erişim Noktası";
        if (platform.StartsWith("er") || platform.Contains("edgerouter") || platform.Contains("edgeswitch"))
            tur = "Router/AP";
        else if (platform.Contains("usw") || platform.Contains("switch"))
            tur = "Switch";
        turL.Add(new TurAdayi(tur, KanitAgirlik.Ubiquiti, KanitKaynak.Ubiquiti, b.UbntPlatform ?? ""));
    }

    private static void KanitTopla_MikroTik(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.MikroTikBoard) && string.IsNullOrWhiteSpace(b.MikroTikIdentity)) return;
        markaL.Add(new MarkaAdayi("MikroTik", KanitAgirlik.MikroTikMarka, KanitKaynak.MikroTik,
            b.MikroTikBoard ?? b.MikroTikIdentity!));
        turL.Add(new TurAdayi("Router/AP", KanitAgirlik.MikroTik, KanitKaynak.MikroTik, b.MikroTikBoard ?? ""));
    }

    private static readonly (Regex Pattern, string? Marka, string Tur)[] SnmpImzalari =
    {
        (new Regex(@"\bRouterOS\b",            RegexOptions.IgnoreCase), "MikroTik",     "Router/AP"),
        (new Regex(@"\bCisco IOS\b",           RegexOptions.IgnoreCase), "Cisco",        "Switch"),
        (new Regex(@"\bCatalyst\b",            RegexOptions.IgnoreCase), "Cisco",        "Switch"),
        (new Regex(@"\bArubaOS\b",             RegexOptions.IgnoreCase), "Aruba",        "Switch/AP"),
        (new Regex(@"\bJUNOS\b",               RegexOptions.IgnoreCase), "Juniper",      "Switch"),
        (new Regex(@"\bMikroTik\b",            RegexOptions.IgnoreCase), "MikroTik",     "Router/AP"),
        (new Regex(@"\bFortiGate\b",           RegexOptions.IgnoreCase), "Fortinet",     "Güvenlik Duvarı"),
        (new Regex(@"\bSonicWall\b",           RegexOptions.IgnoreCase), "SonicWall",    "Güvenlik Duvarı"),
        (new Regex(@"\bpfSense\b",             RegexOptions.IgnoreCase), "pfSense",      "Güvenlik Duvarı"),
        (new Regex(@"\bOPNsense\b",            RegexOptions.IgnoreCase), "OPNsense",     "Güvenlik Duvarı"),
        (new Regex(@"\bOpenWrt\b",             RegexOptions.IgnoreCase), "OpenWrt",      "Router/AP"),
        (new Regex(@"\bDD-?WRT\b",             RegexOptions.IgnoreCase), "DD-WRT",       "Router/AP"),
        (new Regex(@"HP\s+ETHERNET",           RegexOptions.IgnoreCase), "HP",           "Yazıcı"),
        (new Regex(@"\bJetDirect\b",           RegexOptions.IgnoreCase), "HP",           "Yazıcı"),
        (new Regex(@"\bLaserJet\b",            RegexOptions.IgnoreCase), "HP",           "Yazıcı"),
        (new Regex(@"\b(EPSON|Brother|Canon|Kyocera|Lexmark|Ricoh|Xerox)\b", RegexOptions.IgnoreCase), null, "Yazıcı"),
        (new Regex(@"\bDahua\b.{0,40}\b(NVR|DVR|XVR)\b|\b(NVR|DVR|XVR)\b.{0,40}\bDahua\b", RegexOptions.IgnoreCase), "Dahua",     "NVR/DVR"),
        (new Regex(@"\b(NVR|DVR|XVR|Video\s+Recorder)\b",                                RegexOptions.IgnoreCase), null,        "NVR/DVR"),
        (new Regex(@"\bAXIS\b",                RegexOptions.IgnoreCase), "Axis",         "Kamera"),
        (new Regex(@"\bHikvision\b",           RegexOptions.IgnoreCase), "Hikvision",    "Kamera"),
        (new Regex(@"\bDahua\b",               RegexOptions.IgnoreCase), "Dahua",        "Kamera"),
        (new Regex(@"\bSynology\b",            RegexOptions.IgnoreCase), "Synology",     "NAS"),
        (new Regex(@"\bQNAP\b",                RegexOptions.IgnoreCase), "QNAP",         "NAS"),
        (new Regex(@"\bLinux\b",               RegexOptions.IgnoreCase), null,           "Linux IoT"),
        (new Regex(@"Hardware:.*Windows",      RegexOptions.IgnoreCase), "Windows",      "Bilgisayar"),
        (new Regex(@"\bVxWorks\b",             RegexOptions.IgnoreCase), null,           "Akıllı Cihaz"),
    };

    private static void KanitTopla_Snmp(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var s = b.SnmpSysDescr;
        if (string.IsNullOrWhiteSpace(s)) return;
        foreach (var (pat, marka, tur) in SnmpImzalari)
        {
            if (!pat.IsMatch(s)) continue;
            var det = s.Length > 60 ? s[..60] : s;
            turL.Add(new TurAdayi(tur, KanitAgirlik.SnmpTur, KanitKaynak.Snmp, det));
            if (marka != null)
                markaL.Add(new MarkaAdayi(marka, KanitAgirlik.SnmpMarka, KanitKaynak.Snmp, det));
            return;
        }
    }

    private static void KanitTopla_Onvif(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (!b.OnvifBulundu) return;
        turL.Add(new TurAdayi("Kamera", KanitAgirlik.OnvifTur, KanitKaynak.Onvif,
            $"{b.OnvifAdi} {b.OnvifHardware}".Trim()));
        var hw = (b.OnvifHardware ?? "").ToLowerInvariant();
        if (hw.Length > 0)
        {
            foreach (var (anahtar, marka) in OnvifHwMarkaIpuclari)
            {
                if (!hw.Contains(anahtar)) continue;
                markaL.Add(new MarkaAdayi(marka, KanitAgirlik.OnvifMarka, KanitKaynak.Onvif, hw));
                break;
            }
        }
    }

    private static readonly (string Anahtar, string Marka)[] OnvifHwMarkaIpuclari =
    {
        ("hikvision", "Hikvision"), ("ds-", "Hikvision"),
        ("dahua", "Dahua"), ("dh-", "Dahua"),
        ("axis", "Axis"),
        ("reolink", "Reolink"),
        ("uniview", "Uniview"),
        ("bosch", "Bosch"),
        ("hanwha", "Hanwha"), ("samsung techwin", "Hanwha"),
        ("vivotek", "Vivotek"),
        ("ezviz", "EZVIZ"),
        ("amcrest", "Amcrest"),
    };

    private static void KanitTopla_Wsd(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var t = b.WsdTipi;
        if (string.IsNullOrWhiteSpace(t)) return;
        if (t is "Yazıcı" or "Tarayıcı" or "Bilgisayar")
            turL.Add(new TurAdayi(t, KanitAgirlik.WsdTur, KanitKaynak.Wsd, t));
    }

    private static void KanitTopla_Ssdp(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var mfr  = (b.SsdpManufacturer ?? "").Trim();
        var fn   = (b.SsdpFriendlyName ?? "").Trim();
        var mdl  = (b.SsdpModelName ?? "").Trim();
        var srv  = (b.SsdpSunucu ?? "").Trim();
        var blob = $"{mfr} {fn} {mdl} {srv}".ToLowerInvariant();
        if (blob.Trim().Length == 0) return;

        if (mfr.Length > 0)
            markaL.Add(new MarkaAdayi(mfr, KanitAgirlik.SsdpMarka, KanitKaynak.Ssdp, mfr));
        else
        {
            foreach (var (anahtar, marka, _) in MarkaIpuclari)
            {
                if (!blob.Contains(anahtar)) continue;
                markaL.Add(new MarkaAdayi(marka, KanitAgirlik.SsdpMarka, KanitKaynak.Ssdp, anahtar));
                break;
            }
        }

        string? tur = null;
        if (blob.Contains("urn:schemas-upnp-org:device:mediarenderer") ||
            blob.Contains("sonos") || blob.Contains("airplay")) tur = "Hoparlör";
        else if (blob.Contains("internetgatewaydevice") || blob.Contains("wfadevice")) tur = "Router/AP";
        else if (blob.Contains("printer") || blob.Contains("printerservice")) tur = "Yazıcı";
        else if (blob.Contains("scanner")) tur = "Tarayıcı";
        else if (blob.Contains("camera") || blob.Contains("ipcamera")) tur = "Kamera";
        else if (blob.Contains("nas") || blob.Contains("storage"))
        {
            // NVRs advertise "storage" because they store video — don't misclassify as NAS
            bool isNvr = blob.Contains("nvr") || blob.Contains("dvr") || blob.Contains("xvr") ||
                         blob.Contains("recorder") || blob.Contains("dahua") ||
                         blob.Contains("hikvision") || blob.Contains("xmeye");
            tur = isNvr ? "NVR/DVR" : "NAS";
        }
        else if (blob.Contains("smartthings") || blob.Contains("smarttv") || blob.Contains("tv")) tur = "Akıllı TV";
        if (tur != null)
            turL.Add(new TurAdayi(tur, KanitAgirlik.SsdpTur, KanitKaynak.Ssdp,
                blob.Length > 60 ? blob[..60] : blob));
        else if (b.SsdpBulundu)
            turL.Add(new TurAdayi("Akıllı Cihaz", 8, KanitKaynak.Ssdp, "ssdp-only"));
    }

    private static void KanitTopla_Mdns(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (!string.IsNullOrEmpty(b.MdnsTur))
            turL.Add(new TurAdayi(b.MdnsTur, KanitAgirlik.MdnsTur, KanitKaynak.Mdns, b.MdnsTur));
        if (!string.IsNullOrEmpty(b.MdnsMarka))
            markaL.Add(new MarkaAdayi(b.MdnsMarka, KanitAgirlik.MdnsMarka, KanitKaynak.Mdns, b.MdnsMarka));
    }

    private static void KanitTopla_Netbios(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.NetbiosCihazAdi)) return;
        turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.NetbiosTur, KanitKaynak.Netbios, b.NetbiosCihazAdi));
        markaL.Add(new MarkaAdayi("NetBIOS", KanitAgirlik.NetbiosMarka, KanitKaynak.Netbios, b.NetbiosCihazAdi));
    }

    // Printer hostname prefixes: EPSON0SE587, BRN001A2B3C4D, CANON12345…
    private static readonly (string Prefix, string Brand)[] _printerHostnamePrefixes =
    [
        ("epson",   "Epson"),
        ("brother", "Brother"),
        ("canon",   "Canon"),
        ("kyocera", "Kyocera"),
        ("ricoh",   "Ricoh"),
        ("lexmark", "Lexmark"),
        ("xerox",   "Xerox"),
        ("brn",     "Brother"),  // BRN = Brother Network printer
        ("npi",     "HP"),       // NPI = HP JetDirect hostname
    ];

    private static void KanitTopla_Llmnr(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.LlmnrHostname)) return;
        var h = b.LlmnrHostname.ToLowerInvariant();

        foreach (var (prefix, brand) in _printerHostnamePrefixes)
        {
            if (!h.StartsWith(prefix, StringComparison.Ordinal)) continue;
            turL.Add(new TurAdayi("Yazıcı", KanitAgirlik.LlmnrHostname, KanitKaynak.Llmnr, h));
            markaL.Add(new MarkaAdayi(brand, KanitAgirlik.LlmnrHostname, KanitKaynak.Llmnr, h));
            return;
        }

        turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.LlmnrHostname, KanitKaynak.Llmnr, h));
    }

    private static void KanitTopla_Smb(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (!string.IsNullOrWhiteSpace(b.SmbComputerName))
            turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.SmbComputerName, KanitKaynak.Smb, b.SmbComputerName));
        if (!string.IsNullOrWhiteSpace(b.SmbOs))
        {
            var os = b.SmbOs.ToLowerInvariant();
            if (os.Contains("windows"))
                turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.SmbComputerName, KanitKaynak.Smb, b.SmbOs));
        }
    }

    private static void KanitTopla_Ssh(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var banner = b.SshBanner;
        if (string.IsNullOrWhiteSpace(banner)) return;
        var low = banner.ToLowerInvariant();
        if (low.Contains("routeros") || low.Contains("mikrotik"))
        {
            markaL.Add(new MarkaAdayi("MikroTik", KanitAgirlik.SshBanner, KanitKaynak.Ssh, banner));
            turL.Add(new TurAdayi("Router/AP", KanitAgirlik.SshBanner, KanitKaynak.Ssh, banner));
        }
        else if (low.Contains("cisco"))
        {
            markaL.Add(new MarkaAdayi("Cisco", KanitAgirlik.SshBanner, KanitKaynak.Ssh, banner));
            turL.Add(new TurAdayi("Switch", KanitAgirlik.SshBanner, KanitKaynak.Ssh, banner));
        }
        else
        {
            turL.Add(new TurAdayi("Linux IoT", KanitAgirlik.SshBanner / 2, KanitKaynak.Ssh, banner));
        }
    }

    private static void KanitTopla_OuiMac(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var bilgi = OuiVendorLookup.BulDetay(b.MacAdresi);
        if (bilgi == null) return;
        // ARP ile aktif yanıt geldi mi? Daha güçlü kanıt.
        int agirlik = b.KesifKaynaklari.Contains("ARP")
            ? KanitAgirlik.ArpMacOuiActive
            : KanitAgirlik.OuiMarka;
        markaL.Add(new MarkaAdayi(bilgi.Vendor, agirlik, KanitKaynak.OuiMac, $"OUI {b.MacAdresi}"));
        if (!string.IsNullOrWhiteSpace(bilgi.TurIpucu))
            turL.Add(new TurAdayi(bilgi.TurIpucu!, KanitAgirlik.OuiTur, KanitKaynak.OuiMac, $"OUI {bilgi.Vendor}"));
    }

    private static void KanitTopla_PortPattern(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        List<int> p;
        lock (b.AcikPortlar) p = [..b.AcikPortlar];
        if (p.Count == 0) return;

        if (p.Contains(34567))
        {
            turL.Add(new TurAdayi("NVR/DVR", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/34567"));
            markaL.Add(new MarkaAdayi("XMeye", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/34567"));
        }
        if (p.Contains(37777))
        {
            turL.Add(new TurAdayi(p.Contains(554) ? "Kamera" : "NVR/DVR", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/37777"));
            markaL.Add(new MarkaAdayi("Dahua", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/37777"));
        }
        if (p.Contains(8000) && p.Contains(554))
        {
            turL.Add(new TurAdayi("Kamera", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/8000+554"));
            markaL.Add(new MarkaAdayi("Hikvision", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/8000+554"));
        }
        bool printerPortu = p.Contains(9100) || p.Contains(515) || p.Contains(631);
        if (printerPortu)
            turL.Add(new TurAdayi("Yazıcı", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern,
                "tcp/" + string.Join(",", new[] { 9100, 515, 631 }.Where(p.Contains))));
        if (p.Contains(3389))
            turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/3389"));
        if ((p.Contains(445) || p.Contains(139)) && !p.Contains(554) && !printerPortu)
            turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.PortPatternWeak, KanitKaynak.PortPattern, "tcp/445"));
        if (p.Contains(23) && (p.Contains(53) || p.Contains(67)))
            turL.Add(new TurAdayi("Router/AP", KanitAgirlik.PortPatternStrong, KanitKaynak.PortPattern, "tcp/23+udp/53"));
        else if (p.Contains(23))
            turL.Add(new TurAdayi("Router/Switch", KanitAgirlik.PortPatternWeak, KanitKaynak.PortPattern, "tcp/23"));
        if (p.Contains(554) && p.Count <= 3)
            turL.Add(new TurAdayi("Kamera", KanitAgirlik.PortPatternWeak, KanitKaynak.PortPattern, "tcp/554"));
        if (p.Contains(22) && p.Count <= 2 && b.PingTtl is >= 60 and <= 70)
            turL.Add(new TurAdayi("Linux IoT", KanitAgirlik.PortPatternWeak, KanitKaynak.PortPattern, "tcp/22"));
        if ((p.Contains(80) || p.Contains(443)) && p.Count <= 2 && !p.Contains(554) && !printerPortu)
            turL.Add(new TurAdayi("Akıllı Cihaz", 6, KanitKaynak.PortPattern, "tcp/80"));
    }

    private static readonly (string Anahtar, string Marka, string? Tur)[] MarkaIpuclari =
    {
        ("hikvision",        "Hikvision",      "Kamera"),
        ("dahua",            "Dahua",          "Kamera"),
        ("axis",             "Axis",           "Kamera"),
        ("reolink",          "Reolink",        "Kamera"),
        ("bosch",            "Bosch",          "Kamera"),
        ("hanwha",           "Hanwha",         "Kamera"),
        ("samsung techwin",  "Hanwha",         "Kamera"),
        ("vivotek",          "Vivotek",        "Kamera"),
        ("pelco",            "Pelco",          "Kamera"),
        ("uniview",          "Uniview",        "Kamera"),
        ("amcrest",          "Amcrest",        "Kamera"),
        ("annke",            "ANNKE",          "Kamera"),
        ("vstarcam",         "VStarCam",       "Kamera"),
        ("ezviz",            "EZVIZ",          "Kamera"),
        ("ubiquiti",         "Ubiquiti",       "Erişim Noktası"),
        ("ubnt",             "Ubiquiti",       "Erişim Noktası"),
        ("unifi",            "Ubiquiti",       "Erişim Noktası"),
        ("mikrotik",         "MikroTik",       "Router/AP"),
        ("routeros",         "MikroTik",       "Router/AP"),
        ("tp-link",          "TP-Link",        "Switch/AP"),
        ("cisco",            "Cisco",          "Switch"),
        ("d-link",           "D-Link",         "Switch/AP"),
        ("netgear",          "NETGEAR",        "Switch/AP"),
        ("zyxel",            "ZyXEL",          "Switch/AP"),
        ("tenda",            "Tenda",          "Switch/AP"),
        ("huawei",           "Huawei",         "Switch/AP"),
        ("aruba",            "Aruba",          "Switch/AP"),
        ("juniper",          "Juniper",        "Switch"),
        ("fortigate",        "Fortinet",       "Güvenlik Duvarı"),
        ("fortinet",         "Fortinet",       "Güvenlik Duvarı"),
        ("sonicwall",        "SonicWall",      "Güvenlik Duvarı"),
        ("pfsense",          "pfSense",        "Güvenlik Duvarı"),
        ("opnsense",         "OPNsense",       "Güvenlik Duvarı"),
        ("hp laserjet",      "HP",             "Yazıcı"),
        ("laserjet",         "HP",             "Yazıcı"),
        ("jetdirect",        "HP",             "Yazıcı"),
        ("hewlett packard",  "HP",             "Yazıcı"),
        ("seiko epson",      "Epson",          "Yazıcı"),
        ("epson",            "Epson",          "Yazıcı"),
        ("canon printer",    "Canon",          "Yazıcı"),
        ("brother",          "Brother",        "Yazıcı"),
        ("xerox",            "Xerox",          "Yazıcı"),
        ("kyocera",          "Kyocera",        "Yazıcı"),
        ("ricoh",            "Ricoh",          "Yazıcı"),
        ("lexmark",          "Lexmark",        "Yazıcı"),
        ("synology",         "Synology",       "NAS"),
        ("qnap",             "QNAP",           "NAS"),
        ("truenas",          "TrueNAS",        "NAS"),
        ("freenas",          "FreeNAS",        "NAS"),
        ("unraid",           "Unraid",         "NAS"),
        ("asustor",          "Asustor",        "NAS"),
        ("openwrt",          "OpenWrt",        "Router/AP"),
        ("dd-wrt",           "DD-WRT",         "Router/AP"),
        ("xmeye",            "XMeye",          "NVR/DVR"),
        ("homeassistant",    "Home Assistant", "Akıllı Cihaz"),
        ("home-assistant",   "Home Assistant", "Akıllı Cihaz"),
        ("tasmota",          "Tasmota",        "Akıllı Cihaz"),
        ("shelly",           "Shelly",         "Akıllı Cihaz"),
        ("tuya",             "Tuya",           "Akıllı Cihaz"),
        ("espressif",        "Espressif",      "Akıllı Cihaz"),
        ("apple tv",         "Apple",          "Apple TV"),
        ("chromecast",       "Google",         "Akıllı TV"),
        ("webos",            "LG",             "Akıllı TV"),
        ("tizen",            "Samsung",        "Akıllı TV"),
        ("microsoft-iis",    "Windows",        "Bilgisayar"),
        ("iis/",             "Windows",        "Bilgisayar"),
    };

    private static readonly (Regex Pattern, string? Marka, string? Tur)[] MarkaIpuclariRegex =
        MarkaIpuclari
            .Select(x => (
                new Regex($@"\b{Regex.Escape(x.Anahtar)}\b", RegexOptions.IgnoreCase),
                (string?)x.Marka,
                x.Tur))
            .ToArray();

    private static void KanitTopla_Banner(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var metin = $"{b.SunucuBasligi} {b.SayfaBasligi}";
        if (string.IsNullOrWhiteSpace(metin)) return;

        foreach (var (pat, marka, tur) in MarkaIpuclariRegex)
        {
            if (!pat.IsMatch(metin)) continue;
            if (marka != null)
                markaL.Add(new MarkaAdayi(marka, KanitAgirlik.BannerMarka, KanitKaynak.Banner, pat.ToString()));
            if (tur != null)
                turL.Add(new TurAdayi(tur, KanitAgirlik.BannerTur, KanitKaynak.Banner, pat.ToString()));
            break;
        }

        if (Regex.IsMatch(metin, @"\b(network|digital|hybrid)\s+video\s+recorder\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(metin, @"\b(nvr|dvr|xvr)\b", RegexOptions.IgnoreCase))
            turL.Add(new TurAdayi("NVR/DVR", KanitAgirlik.BannerTur, KanitKaynak.Banner, "nvr/dvr regex"));
    }

    private static void KanitTopla_Ttl(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (!b.PingYanit || b.PingTtl <= 0) return;
        if (b.PingTtl is >= 120 and <= 128)
            turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.TtlTur, KanitKaynak.Ttl, $"ttl={b.PingTtl}"));
        else if (b.PingTtl >= 250)
            turL.Add(new TurAdayi("Router/Switch", KanitAgirlik.TtlTur, KanitKaynak.Ttl, $"ttl={b.PingTtl}"));
    }

    private static void KanitTopla_AdHostname(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        var ad = $"{b.DnsAdi} {b.PingAdi} {b.LlmnrHostname} {b.SsdpFriendlyName} {b.NetbiosCihazAdi}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ad)) return;

        if (Regex.IsMatch(ad, @"\biphone[-\w]*"))
        {
            markaL.Add(new MarkaAdayi("Apple", KanitAgirlik.AdHostnameMarka, KanitKaynak.AdHostname, "iphone-"));
            turL.Add(new TurAdayi("Telefon", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "iphone-"));
        }
        else if (Regex.IsMatch(ad, @"\bipad[-\w]*"))
        {
            markaL.Add(new MarkaAdayi("Apple", KanitAgirlik.AdHostnameMarka, KanitKaynak.AdHostname, "ipad-"));
            turL.Add(new TurAdayi("Tablet", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "ipad-"));
        }
        else if (Regex.IsMatch(ad, @"\bandroid[-_][\w]*"))
            turL.Add(new TurAdayi("Telefon", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "android-"));
        else if (Regex.IsMatch(ad, @"\bgalaxy[-\w]*"))
        {
            markaL.Add(new MarkaAdayi("Samsung", KanitAgirlik.AdHostnameMarka, KanitKaynak.AdHostname, "galaxy"));
            turL.Add(new TurAdayi("Telefon", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "galaxy"));
        }
        else if (Regex.IsMatch(ad, @"\b(redmi|xiaomi|poco|mi[- ])"))
        {
            markaL.Add(new MarkaAdayi("Xiaomi", KanitAgirlik.AdHostnameMarka, KanitKaynak.AdHostname, "xiaomi"));
            turL.Add(new TurAdayi("Telefon", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "xiaomi"));
        }
        else if (Regex.IsMatch(ad, @"\bpixel[-\w]*"))
        {
            markaL.Add(new MarkaAdayi("Google", KanitAgirlik.AdHostnameMarka, KanitKaynak.AdHostname, "pixel"));
            turL.Add(new TurAdayi("Telefon", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "pixel"));
        }
        else if (Regex.IsMatch(ad, @"\b(desktop|laptop)-\w+"))
            turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.AdHostnameTur, KanitKaynak.AdHostname, "desktop-/laptop-"));
        else if (Regex.IsMatch(ad, @"\b(pc|win)-\w+"))
            turL.Add(new TurAdayi("Bilgisayar", KanitAgirlik.AdHostnameTur - 10, KanitKaynak.AdHostname, "pc-/win-"));
    }
}
