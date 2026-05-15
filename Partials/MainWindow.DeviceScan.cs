using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ClosedXML.Excel;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── 7. Cihaz Tarayıcı ───────────────────────────────────────────

    private sealed class KameraBilgi
    {
        public string    Ip               { get; init; } = "";
        public List<int> AcikPortlar      { get; } = new();
        public bool      OnvifBulundu     { get; set; }
        public bool      SsdpBulundu      { get; set; }
        public string?   OnvifServisUrl   { get; set; }
        public string?   OnvifAdi         { get; set; }
        public string?   OnvifHardware    { get; set; }
        public string?   OnvifKonum       { get; set; }
        public string?   RtspDurum        { get; set; }
        public string?   SunucuBasligi    { get; set; }
        public string?   SayfaBasligi     { get; set; }
        public string?   NetbiosCihazAdi  { get; set; }
        public string?   NetbiosGrupAdi   { get; set; }
        public string?   DnsAdi           { get; set; }
        public string?   PingAdi          { get; set; }
        public string?   SsdpLocation     { get; set; }
        public string?   SsdpSunucu       { get; set; }
        public string?   SsdpFriendlyName { get; set; }
        public string?   SsdpManufacturer { get; set; }
        public string?   SsdpModelName    { get; set; }
        public string?   SsdpModelNumber  { get; set; }
        public string?   MacAdresi        { get; set; }
        public string?   Uretici          { get; set; }
        public string?   AdvancedScannerAdi      { get; set; }
        public string?   AdvancedScannerServisler { get; set; }
        public Dictionary<int, string> ServisDetaylari { get; } = new();
        public bool      PingYanit  { get; set; }
        public int       PingMs     { get; set; }
        public int       PingTtl    { get; set; }
        public string    MdnsMarka  { get; set; } = "";
        public string    MdnsTur    { get; set; } = "";
        // Yeni: vendor-specific discovery sonuçları
        public string?   UbntPlatform { get; set; }
        public string?   UbntFirmware { get; set; }
        public string?   UbntHostname { get; set; }
        public string?   MikroTikBoard    { get; set; }
        public string?   MikroTikVersion  { get; set; }
        public string?   MikroTikIdentity { get; set; }
        public string?   SnmpSysDescr     { get; set; }
        public string?   SnmpSysName      { get; set; }
        public string?   HttpFpMarka      { get; set; }
        public string?   HttpFpTur        { get; set; }
        public string?   HttpFpModel      { get; set; }
        public string?   WsdTipi          { get; set; }
        public HashSet<string> KesifKaynaklari { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CihazKimlik
    {
        public string  Marka   { get; set; } = "Bilinmiyor";
        public string? Model   { get; set; }
        public string  Tur     { get; set; } = "Cihaz";
        public string  TurIkon { get; set; } = "◈";
    }

    private static readonly int[] KameraPorts = { 554, 8000, 8080, 37777, 80, 8443, 22, 23, 139, 443, 445, 3389, 9000, 34567 };

    private static readonly (string Anahtar, string Marka, string Tur)[] MarkaTablosu =
    {
        ("hikvision",        "Hikvision",    "Kamera"),
        ("cross web server", "Dahua",        "Kamera"),
        ("dahua",            "Dahua",        "Kamera"),
        ("axis",             "Axis",         "Kamera"),
        ("reolink",          "Reolink",      "Kamera"),
        ("bosch",            "Bosch",        "Kamera"),
        ("hanwha",           "Hanwha",       "Kamera"),
        ("samsung techwin",  "Hanwha",       "Kamera"),
        ("vivotek",          "Vivotek",      "Kamera"),
        ("pelco",            "Pelco",        "Kamera"),
        ("flir",             "FLIR",         "Kamera"),
        ("uniview",          "Uniview",      "Kamera"),
        ("goahead-webs",     "IP Kamera",    "Kamera"),
        ("mini_httpd",       "IP Kamera",    "Kamera"),
        ("ubiquiti",         "Ubiquiti",     "Erişim Noktası"),
        ("unifi",            "Ubiquiti",     "Erişim Noktası"),
        ("airos",            "Ubiquiti",     "Erişim Noktası"),
        ("ubnt",             "Ubiquiti",     "Erişim Noktası"),
        ("mikrotik",         "MikroTik",     "Router/AP"),
        ("routeros",         "MikroTik",     "Router/AP"),
        ("tp-link",          "TP-Link",      "Switch/AP"),
        ("tplink",           "TP-Link",      "Switch/AP"),
        ("cisco",            "Cisco",        "Switch"),
        ("d-link",           "D-Link",       "Switch/AP"),
        ("dlink",            "D-Link",       "Switch/AP"),
        ("netgear",          "NETGEAR",      "Switch/AP"),
        ("zyxel",            "ZyXEL",        "Switch/AP"),
        ("tenda",            "Tenda",        "Switch/AP"),
        ("huawei",           "Huawei",       "Switch/AP"),
        ("h3c",              "H3C",          "Switch"),
        ("ruijie",           "Ruijie",       "Switch"),
        ("asus",             "ASUS",         "Router/AP"),
        ("witek",            "Witek",        "Kamera"),
        ("ttec",             "TTEC",         "Kamera"),
        ("stonet",           "Stonet",       "Kamera"),
        ("vstarcam",         "VStarCam",     "Kamera"),
        ("annke",            "ANNKE",        "Kamera"),
        ("amcrest",          "Amcrest",      "Kamera"),
        ("xmeye",            "XMeye",        "NVR/DVR"),
        ("web service root", "XMeye",        "NVR/DVR"),
        ("nvr",              "NVR",          "NVR/DVR"),
        ("synology",         "Synology",     "NAS"),
        ("qnap",             "QNAP",         "NAS"),
        ("mycloud",          "WD",           "NAS"),
        ("wd my",            "WD",           "NAS"),
        ("asustor",          "Asustor",      "NAS"),
        // Telefon / mobil
        ("android",          "Android",      "Telefon"),
        ("miui",             "Xiaomi",       "Telefon"),
        ("iphone",           "Apple",        "Telefon"),
        ("ipad",             "Apple",        "Tablet"),
        ("oneplus",          "OnePlus",      "Telefon"),
        ("oppo",             "OPPO",         "Telefon"),
        ("vivo ",            "Vivo",         "Telefon"),
        // Bilgisayar
        ("microsoft",        "Windows",      "Bilgisayar"),
        ("iis",              "Windows/IIS",  "Bilgisayar"),
        ("openwrt",          "OpenWrt",      "Router/AP"),
        ("dd-wrt",           "DD-WRT",       "Router/AP"),
        ("pfsense",          "pfSense",      "Güvenlik Duvarı"),
        ("fortinet",         "Fortinet",     "Güvenlik Duvarı"),
        ("fortigate",        "Fortinet",     "Güvenlik Duvarı"),
        ("sonicwall",        "SonicWall",    "Güvenlik Duvarı"),
        ("aruba",            "Aruba",        "Switch/AP"),
        ("juniper",          "Juniper",      "Switch"),
        ("procurve",         "HP ProCurve",  "Switch"),
        ("hpe",              "HPE",          "Switch"),
        ("hp laserjet",      "HP",           "Yazıcı"),
        ("laserjet",         "HP",           "Yazıcı"),
        ("hewlett packard",  "HP",           "Yazıcı"),
        ("seiko epson",      "Epson",        "Yazıcı"),
        ("epson",            "Epson",        "Yazıcı"),
        ("canon printer",    "Canon",        "Yazıcı"),
        ("brother",          "Brother",      "Yazıcı"),
        ("xerox",            "Xerox",        "Yazıcı"),
        ("kyocera",          "Kyocera",      "Yazıcı"),
        // Ek NAS / sunucu
        ("truenas",          "TrueNAS",      "NAS"),
        ("freenas",           "FreeNAS",      "NAS"),
        ("unraid",           "Unraid",       "NAS"),
        ("proxmox",          "Proxmox",      "Sunucu"),
        ("homeassistant",    "Home Assistant","Akıllı Cihaz"),
        ("home-assistant",   "Home Assistant","Akıllı Cihaz"),
        // Ek güvenlik duvarı / router
        ("opnsense",         "OPNsense",     "Güvenlik Duvarı"),
        ("meraki",           "Meraki",       "Switch/AP"),
        ("omada",            "TP-Link Omada","Switch/AP"),
        ("eero",             "Eero",         "Router/AP"),
        ("deco",             "TP-Link Deco", "Router/AP"),
        ("orbi",             "Netgear Orbi", "Router/AP"),
        ("velop",            "Linksys Velop","Router/AP"),
        ("linksys",          "Linksys",      "Router/AP"),
        // Ek IoT / akıllı cihaz
        ("tasmota",          "Tasmota",      "Akıllı Cihaz"),
        ("shelly",           "Shelly",       "Akıllı Cihaz"),
        ("tuya",             "Tuya",         "Akıllı Cihaz"),
        ("espressif",        "ESP",          "Akıllı Cihaz"),
        ("esp-",             "ESP",          "Akıllı Cihaz"),
        ("nest",             "Google Nest",  "Akıllı Cihaz"),
        ("ring",             "Ring",         "Akıllı Cihaz"),
        ("wyze",             "Wyze",         "Kamera"),
        ("ezviz",            "EZVIZ",        "Kamera"),
        ("tp-link kasa",     "TP-Link Kasa", "Akıllı Cihaz"),
        ("philips hue",      "Philips Hue",  "Akıllı Cihaz"),
        ("roborock",         "Roborock",     "Akıllı Cihaz"),
        ("dyson",            "Dyson",        "Akıllı Cihaz"),
        // Ek akıllı TV
        ("lg webos",         "LG",           "Akıllı TV"),
        ("webos",            "LG",           "Akıllı TV"),
        ("samsung tizen",    "Samsung",      "Akıllı TV"),
        ("tizen",            "Samsung",      "Akıllı TV"),
        ("vizio",            "Vizio",        "Akıllı TV"),
        ("tcl ",             "TCL",          "Akıllı TV"),
        ("chromecast",       "Google",       "Akıllı TV"),
        // Sunucu yazılımları
        ("nginx",            "",             "Sunucu"),
        ("apache",           "",             "Sunucu"),
        ("ubuntu",           "Ubuntu",       "Sunucu"),
        ("debian",           "Debian",       "Sunucu"),
        ("centos",           "CentOS",       "Sunucu"),
        ("raspberry",        "Raspberry Pi", "Linux IoT"),
        ("raspbian",         "Raspberry Pi", "Linux IoT"),
        // Ek Yazıcı
        ("ricoh",            "Ricoh",        "Yazıcı"),
        ("oki",              "OKI",          "Yazıcı"),
        ("lexmark",          "Lexmark",      "Yazıcı"),
        ("sharp",            "Sharp",        "Yazıcı"),
        ("toshiba tec",      "Toshiba",      "Yazıcı"),
    };

    private static CihazKimlik KimlikBelirle(KameraBilgi b)
    {
        var k      = new CihazKimlik();
        var birles = $"{b.SunucuBasligi} {b.SayfaBasligi} {b.OnvifAdi} {b.OnvifHardware} {b.SsdpFriendlyName} {b.SsdpManufacturer} {b.SsdpModelName} {b.SsdpSunucu} {b.Uretici} {b.AdvancedScannerAdi} {b.SnmpSysDescr} {b.SnmpSysName} {b.UbntPlatform} {b.UbntHostname} {b.MikroTikBoard} {b.MikroTikIdentity} {b.HttpFpMarka} {b.HttpFpModel}".ToLowerInvariant();
        var kayitCihazi    = KayitCihaziIpuclariVar(birles, b.AcikPortlar);
        var yazici         = YaziciIpuclariVar(birles, b.AcikPortlar) ||
                             b.WsdTipi == "Yazıcı" ||
                             (b.SnmpSysDescr?.Contains("printer", StringComparison.OrdinalIgnoreCase) == true) ||
                             (b.SnmpSysDescr?.Contains("laserjet", StringComparison.OrdinalIgnoreCase) == true);
        var bilgisayarIpuclari =
            !string.IsNullOrWhiteSpace(b.NetbiosCihazAdi) ||
            b.AcikPortlar.Contains(3389) ||
            ((b.AcikPortlar.Contains(139) || b.AcikPortlar.Contains(445)) && CihazAdiBilgisayarGibi(b));

        // Vendor-specific yüksek güven kaynakları
        if (!string.IsNullOrWhiteSpace(b.UbntPlatform) || !string.IsNullOrWhiteSpace(b.UbntHostname))
        {
            k.Marka = "Ubiquiti";
            k.Tur = (b.UbntPlatform ?? "").Contains("ER", StringComparison.OrdinalIgnoreCase) ? "Router/AP" : "Erişim Noktası";
        }
        if (!string.IsNullOrWhiteSpace(b.MikroTikBoard) || !string.IsNullOrWhiteSpace(b.MikroTikIdentity))
        {
            k.Marka = "MikroTik";
            k.Tur = "Router/AP";
        }
        if (!string.IsNullOrWhiteSpace(b.HttpFpMarka))
        {
            k.Marka = b.HttpFpMarka;
            if (!string.IsNullOrWhiteSpace(b.HttpFpTur)) k.Tur = b.HttpFpTur;
        }

        // mDNS güvenilir kaynak — Ubiquiti/MikroTik/HTTP-FP yoksa uygula
        if (!string.IsNullOrEmpty(b.MdnsTur) && k.Tur == "Cihaz")
        {
            k.Tur = b.MdnsTur;
            if (!string.IsNullOrEmpty(b.MdnsMarka) && k.Marka == "Bilinmiyor") k.Marka = b.MdnsMarka;
        }

        if (yazici)
        {
            k.Tur = "Yazıcı";
            if (k.Marka == "Bilinmiyor")
            {
                if (birles.Contains("epson")) k.Marka = "Epson";
                else if (birles.Contains("hewlett packard") || birles.Contains("laserjet") || Regex.IsMatch(birles, @"\bhp\b")) k.Marka = "HP";
                else if (birles.Contains("canon"))   k.Marka = "Canon";
                else if (birles.Contains("brother")) k.Marka = "Brother";
                else if (birles.Contains("xerox"))   k.Marka = "Xerox";
                else if (birles.Contains("kyocera")) k.Marka = "Kyocera";
            }
        }

        if (kayitCihazi && !yazici)
        {
            k.Tur = "NVR/DVR";
            if (birles.Contains("xmeye")) k.Marka = "XMeye";
        }

        if (bilgisayarIpuclari && !kayitCihazi && !yazici)
        {
            k.Tur = "Bilgisayar";
            if (k.Marka == "Bilinmiyor" && !string.IsNullOrWhiteSpace(b.NetbiosCihazAdi))
                k.Marka = "NetBIOS";
        }

        if (k.Tur == "Cihaz")
        {
            foreach (var (anahtar, marka, tur) in MarkaTablosu)
            {
                if (!birles.Contains(anahtar)) continue;
                k.Marka = marka; k.Tur = tur; break;
            }
        }
        else
        {
            foreach (var (anahtar, marka, _) in MarkaTablosu)
            {
                if (!birles.Contains(anahtar)) continue;
                if (k.Marka == "Bilinmiyor") k.Marka = marka;
                break;
            }
        }

        if (kayitCihazi && !yazici && k.Marka == "Bilinmiyor")
        {
            if (birles.Contains("hikvision"))    k.Marka = "Hikvision";
            else if (birles.Contains("dahua"))   k.Marka = "Dahua";
            else if (birles.Contains("uniview")) k.Marka = "Uniview";
        }

        if (k.Marka == "Bilinmiyor" && k.Tur == "Cihaz")
        {
            if (b.AcikPortlar.Contains(34567))                                        { k.Marka = "XMeye";     k.Tur = "NVR/DVR"; }
            else if (b.AcikPortlar.Contains(9000) && b.AcikPortlar.Contains(554))    {                         k.Tur = "NVR/DVR"; }
            else if (b.AcikPortlar.Contains(37777))                                   { k.Marka = "Dahua";     k.Tur = kayitCihazi ? "NVR/DVR" : "Kamera"; }
            else if (b.AcikPortlar.Contains(8000) && b.AcikPortlar.Contains(554))    { k.Marka = "Hikvision"; k.Tur = kayitCihazi ? "NVR/DVR" : "Kamera"; }
            else if (b.AcikPortlar.Contains(554))                                     {                         k.Tur = "Kamera"; }
            else if (!string.IsNullOrWhiteSpace(b.NetbiosCihazAdi) && !b.AcikPortlar.Contains(3389) && !b.AcikPortlar.Contains(445))
                                                                                      { k.Marka = "NetBIOS";   k.Tur = "Bilgisayar"; }
            else if (!string.IsNullOrWhiteSpace(b.NetbiosCihazAdi))                   { k.Marka = "NetBIOS";   k.Tur = "Bilgisayar"; }
            // SSH-only + Linux TTL → Linux IoT/Sunucu (Raspberry Pi vb.)
            else if (b.AcikPortlar.Contains(22) && b.AcikPortlar.Count <= 2 && b.PingTtl is >= 60 and <= 70 &&
                     string.IsNullOrWhiteSpace(b.NetbiosCihazAdi))
                                                                                      {                         k.Tur = "Linux IoT"; }
            else if (!string.IsNullOrWhiteSpace(b.DnsAdi) || !string.IsNullOrWhiteSpace(b.PingAdi)) {          k.Tur = "Bilgisayar"; }
            else if (b.AcikPortlar.Contains(445) || b.AcikPortlar.Contains(3389))    {                         k.Tur = "Bilgisayar"; }
            else if (b.AcikPortlar.Contains(23) && (b.AcikPortlar.Contains(53) || b.AcikPortlar.Contains(67)))
                                                                                      {                         k.Tur = "Router"; }
            else if (b.AcikPortlar.Contains(23))                                      {                         k.Tur = "Router/Switch"; }
            // Sadece web portu açık + küçük cihaz IP olabilir
            else if ((b.AcikPortlar.Contains(80) || b.AcikPortlar.Contains(443)) && b.AcikPortlar.Count <= 2)
                                                                                      {                         k.Tur = "Akıllı Cihaz"; }
        }

        if (k.Tur == "Cihaz" && b.PingYanit && b.PingTtl > 0)
        {
            if (b.PingTtl >= 120 && b.PingTtl <= 128) k.Tur = "Bilgisayar";
            else if (b.PingTtl >= 250)                 k.Tur = "Router/Switch";
        }

        if (k.Tur is "Cihaz" or "Bilgisayar")
        {
            var adlar = $"{b.DnsAdi} {b.PingAdi} {b.AdvancedScannerAdi} {b.SsdpFriendlyName}".ToLowerInvariant();
            if (adlar.Contains("iphone"))                                            { k.Marka = "Apple";   k.Tur = "Telefon"; }
            else if (adlar.Contains("ipad"))                                         { k.Marka = "Apple";   k.Tur = "Tablet"; }
            else if (adlar.Contains("android-") || adlar.Contains("android_"))      {                       k.Tur = "Telefon"; }
            else if (adlar.Contains("galaxy"))                                       { k.Marka = "Samsung"; k.Tur = "Telefon"; }
            else if (adlar.Contains("redmi") || adlar.Contains("xiaomi") ||
                     adlar.Contains("poco"))                                         { k.Marka = "Xiaomi";  k.Tur = "Telefon"; }
            else if (adlar.Contains("pixel"))                                        { k.Marka = "Google";  k.Tur = "Telefon"; }
        }

        if (k.Tur == "Cihaz" && !string.IsNullOrEmpty(b.Uretici))
        {
            var ureticiKucuk = b.Uretici.ToLowerInvariant();
            bool mobil = ureticiKucuk.Contains("apple") || ureticiKucuk.Contains("samsung") ||
                         ureticiKucuk.Contains("huawei") || ureticiKucuk.Contains("xiaomi") ||
                         ureticiKucuk.Contains("oneplus") || ureticiKucuk.Contains("oppo") ||
                         ureticiKucuk.Contains("vivo") || ureticiKucuk.Contains("realme") ||
                         ureticiKucuk.Contains("google") || ureticiKucuk.Contains("motorola") ||
                         ureticiKucuk.Contains("nokia") || ureticiKucuk.Contains("sony mobile") ||
                         ureticiKucuk.Contains("honor");
            bool sunucuPortuYok = !b.AcikPortlar.Any(p => p is 22 or 80 or 443 or 445 or 554 or 3389 or 8080 or 8443 or 8000);
            if (mobil && sunucuPortuYok)
            {
                k.Tur = "Telefon";
                if (k.Marka == "Bilinmiyor")
                {
                    if (ureticiKucuk.Contains("samsung"))      k.Marka = "Samsung";
                    else if (ureticiKucuk.Contains("apple"))   k.Marka = "Apple";
                    else if (ureticiKucuk.Contains("huawei"))  k.Marka = "Huawei";
                    else if (ureticiKucuk.Contains("xiaomi"))  k.Marka = "Xiaomi";
                    else if (ureticiKucuk.Contains("google"))  k.Marka = "Google";
                    else if (ureticiKucuk.Contains("motorola")) k.Marka = "Motorola";
                    else if (ureticiKucuk.Contains("honor"))   k.Marka = "Honor";
                    else                                        k.Marka = b.Uretici;
                }
            }
        }

        k.TurIkon = k.Tur switch
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

        k.Model = IlkDolu(
            b.HttpFpModel,
            b.UbntPlatform,
            b.MikroTikBoard,
            b.SsdpModelName,
            b.SsdpModelNumber,
            b.OnvifHardware,
            AnlamliSayfaBasligi(b.SayfaBasligi));

        return k;
    }

    /// <summary>Tanıma güvenilirliğini 0-100 arası bir skora dönüştürür.</summary>
    private static int GuvenSkoru(KameraBilgi b, CihazKimlik k)
    {
        int skor = 0;
        // Yüksek güvenli vendor-specific kaynaklar
        if (!string.IsNullOrWhiteSpace(b.UbntPlatform) || !string.IsNullOrWhiteSpace(b.UbntHostname)) skor += 35;
        if (!string.IsNullOrWhiteSpace(b.MikroTikBoard) || !string.IsNullOrWhiteSpace(b.MikroTikIdentity)) skor += 35;
        if (!string.IsNullOrWhiteSpace(b.HttpFpMarka)) skor += 30;
        if (!string.IsNullOrWhiteSpace(b.SnmpSysDescr)) skor += 25;
        if (b.OnvifBulundu) skor += 20;
        if (!string.IsNullOrEmpty(b.MdnsTur)) skor += 20;
        if (!string.IsNullOrWhiteSpace(b.WsdTipi)) skor += 15;
        // Orta güvenli
        if (b.SsdpBulundu) skor += 15;
        if (!string.IsNullOrWhiteSpace(b.NetbiosCihazAdi)) skor += 12;
        if (!string.IsNullOrWhiteSpace(b.MacAdresi) && !string.IsNullOrWhiteSpace(b.Uretici)) skor += 10;
        if (b.AcikPortlar.Count > 0) skor += Math.Min(10, b.AcikPortlar.Count * 2);
        // Yalnızca tür/marka bilgisi varsa baz puan
        if (k.Marka != "Bilinmiyor" && skor == 0) skor += 5;
        return Math.Min(100, skor);
    }

    private static bool CihazAdiBilgisayarGibi(KameraBilgi b)
    {
        var ad = $"{b.NetbiosCihazAdi} {b.DnsAdi} {b.PingAdi} {b.AdvancedScannerAdi}".ToLowerInvariant();
        return ad.Contains("desktop-") ||
               ad.Contains("laptop-") ||
               Regex.IsMatch(ad, @"(^|\s)pc[-\w]*") ||
               Regex.IsMatch(ad, @"(^|\s)win[-\w]*");
    }

    private static bool KayitCihaziIpuclariVar(string metin, ICollection<int> acikPortlar)
    {
        if (Regex.IsMatch(metin, @"(^|[^a-z0-9])(xvr|nvr|dvr)[a-z0-9-]*", RegexOptions.IgnoreCase)) return true;
        if (metin.Contains("network video recorder") || metin.Contains("digital video recorder")) return true;
        if (metin.Contains("hybrid video recorder") || metin.Contains("video recorder")) return true;
        if (Regex.IsMatch(metin, @"\b(ds-|dh-).*(xvr|nvr|dvr|ni|hghi|hqhi|huhi|ht)", RegexOptions.IgnoreCase)) return true;
        return acikPortlar.Contains(34567) ||
               (acikPortlar.Contains(9000) && acikPortlar.Contains(554));
    }

    private static bool YaziciIpuclariVar(string metin, ICollection<int> acikPortlar)
    {
        if (metin.Contains("laserjet") || metin.Contains("hewlett packard")) return true;
        if (metin.Contains("seiko epson") || metin.Contains("epson")) return true;
        if (metin.Contains("canon printer") || metin.Contains("brother") || metin.Contains("xerox") || metin.Contains("kyocera")) return true;
        if (metin.Contains("printer") || metin.Contains("multifunction") || metin.Contains("mfp")) return true;
        return acikPortlar.Contains(9100) || acikPortlar.Contains(515) || acikPortlar.Contains(631);
    }

    private static string? CihazAdiSec(KameraBilgi b)
        => IlkDolu(
            b.NetbiosCihazAdi,
            KisaHostAdi(b.DnsAdi),
            KisaHostAdi(b.PingAdi),
            b.OnvifAdi,
            b.SsdpFriendlyName,
            b.AdvancedScannerAdi);

    private static string? IlkDolu(params string?[] degerler)
    {
        foreach (var deger in degerler)
        {
            var temiz = TemizKimlikMetni(deger);
            if (temiz != null) return temiz;
        }
        return null;
    }

    private static string? KisaHostAdi(string? ad)
    {
        var temiz = TemizKimlikMetni(ad);
        if (temiz == null) return null;
        var nokta = temiz.IndexOf('.');
        return nokta > 0 ? temiz[..nokta] : temiz;
    }

    private static string? AnlamliSayfaBasligi(string? baslik)
    {
        var temiz = TemizKimlikMetni(baslik);
        if (temiz == null) return null;
        var lower = temiz.ToLowerInvariant();
        if (lower is "login" or "index" or "web service" or "web service root" or "document") return null;
        if (lower.Contains("login page")) return null;
        return temiz;
    }

    private static string? TemizKimlikMetni(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        var temiz = WebUtility.HtmlDecode(metin).Trim();
        temiz = Regex.Replace(temiz, @"\s+", " ");
        temiz = temiz.Trim('-', '_', '.', ' ');
        return string.IsNullOrWhiteSpace(temiz) ? null : temiz;
    }

    private sealed class TaramaSubneti
    {
        public string Prefix { get; init; } = "";
        public string Cidr => $"{Prefix}.0/24";
    }

    private static List<string> YerelSubnetleriBul()
    {
        var sonuc = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
            if (SanalAdaptorMu(ni)) continue;

            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))
                    sonuc.Add($"{b[0]}.{b[1]}.{b[2]}");
            }
        }
        return sonuc.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private sealed record NicSubneti(string Prefix, string NicAdi, string Tip, long Hiz);

    private static List<NicSubneti> YerelNicSubnetleriniBul()
    {
        var sonuc = new Dictionary<string, NicSubneti>(StringComparer.Ordinal);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
            if (SanalAdaptorMu(ni)) continue;

            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (!(b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))) continue;
                var prefix = $"{b[0]}.{b[1]}.{b[2]}";
                if (sonuc.ContainsKey(prefix)) continue;
                var tip = ni.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                    NetworkInterfaceType.Ethernet      => "Ethernet",
                    NetworkInterfaceType.GigabitEthernet => "Ethernet",
                    _ => ni.NetworkInterfaceType.ToString(),
                };
                sonuc[prefix] = new NicSubneti(prefix, ni.Name, tip, ni.Speed);
            }
        }
        return sonuc.Values.OrderBy(x => x.Prefix, StringComparer.Ordinal).ToList();
    }

    private static string? YerelSubnetiBul()
        => YerelSubnetleriBul().FirstOrDefault();

    private static bool SanalAdaptorMu(NetworkInterface ni)
    {
        var ad = $"{ni.Name} {ni.Description}".ToLowerInvariant();
        return ad.Contains("virtual")
            || ad.Contains("vmware")
            || ad.Contains("hyper-v")
            || ad.Contains("vbox")
            || ad.Contains("vpn")
            || ad.Contains("wireguard")
            || ad.Contains("loopback")
            || ad.Contains("tunnel")
            || ad.Contains("tap")
            || ad.Contains("miniport");
    }

    private static List<TaramaSubneti> SubnetGirdisiniCoz(string giris)
    {
        var list = new List<TaramaSubneti>();
        var tekiller = new HashSet<string>(StringComparer.Ordinal);
        var parcalar = giris.Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var parca in parcalar)
        {
            var token = parca.Trim();
            string? prefix = null;

            var cidr = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})/(?<m>\d{1,2})$");
            if (cidr.Success)
            {
                var mask = int.Parse(cidr.Groups["m"].Value);
                if (mask < 16 || mask > 30) continue;
                prefix = $"{cidr.Groups["a"].Value}.{cidr.Groups["b"].Value}.{cidr.Groups["c"].Value}";
            }
            else
            {
                var p3 = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})$");
                if (p3.Success)
                    prefix = $"{p3.Groups["a"].Value}.{p3.Groups["b"].Value}.{p3.Groups["c"].Value}";
                else
                {
                    var p4 = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})$");
                    if (p4.Success)
                        prefix = $"{p4.Groups["a"].Value}.{p4.Groups["b"].Value}.{p4.Groups["c"].Value}";
                }
            }

            if (prefix is null) continue;

            var oktetler = prefix.Split('.');
            if (oktetler.Length != 3) continue;
            if (!oktetler.All(x => int.TryParse(x, out var n) && n is >= 0 and <= 255)) continue;

            if (tekiller.Add(prefix))
                list.Add(new TaramaSubneti { Prefix = prefix });
        }

        return list;
    }

    private readonly ObservableCollection<KameraSatir> _kameraSatirlari = new();
    private readonly Dictionary<string, KameraSatir>   _kameraSatirlar  = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KameraBilgi>   _kameraBilgileri = new(StringComparer.Ordinal);
    private ICollectionView? _kameraSatirView;

    private bool _subnetBoxChipSenkronu;

    private void BtnKamera_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabCihazTara;
        if (KameraSubnetChips.Children.Count == 0)
            KameraNicChipleriniYenile(seciliVarsayilan: true);
    }

    private void KameraNicYenileBtn_Click(object sender, RoutedEventArgs e)
        => KameraNicChipleriniYenile(seciliVarsayilan: false);

    public void KameraNicChipleriniYenile(bool seciliVarsayilan)
    {
        var mevcutSecili = KameraSubnetChips.Children
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true)
            .Select(t => t.Tag as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.Ordinal);

        KameraSubnetChips.Children.Clear();

        var nicler = YerelNicSubnetleriniBul();
        if (nicler.Count == 0)
        {
            var bos = new TextBlock
            {
                Text = "Aktif ağ arayüzü bulunamadı",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(2, 6, 0, 0),
            };
            KameraSubnetChips.Children.Add(bos);
            return;
        }

        for (int i = 0; i < nicler.Count; i++)
        {
            var nic = nicler[i];
            var chip = KameraChipOlustur(nic);
            bool secili = mevcutSecili.Contains(nic.Prefix) ||
                          (mevcutSecili.Count == 0 && seciliVarsayilan && i == 0);
            chip.IsChecked = secili;
            KameraSubnetChips.Children.Add(chip);
        }

        KameraChipleriSenkronizeEt();
    }

    private System.Windows.Controls.Primitives.ToggleButton KameraChipOlustur(NicSubneti nic)
    {
        var icerik = new StackPanel { Orientation = Orientation.Horizontal };
        icerik.Children.Add(new TextBlock
        {
            Text = $"{nic.Prefix}.0/24",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        icerik.Children.Add(new TextBlock
        {
            Text = $"  ({nic.Tip})",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var chip = new System.Windows.Controls.Primitives.ToggleButton
        {
            Tag = nic.Prefix,
            Content = icerik,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 6),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
            ToolTip = $"{nic.NicAdi}\nTür: {nic.Tip}" +
                      (nic.Hiz > 0 ? $"\nHız: {nic.Hiz / 1_000_000} Mbps" : ""),
            FocusVisualStyle = null,
            Cursor = Cursors.Hand,
        };
        chip.Checked += KameraChipDegisti;
        chip.Unchecked += KameraChipDegisti;
        return chip;
    }

    private void KameraChipDegisti(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton tb)
        {
            tb.Background = tb.IsChecked == true
                ? new SolidColorBrush(Color.FromRgb(0x0F, 0x37, 0x6D))
                : new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
            tb.BorderBrush = tb.IsChecked == true
                ? new SolidColorBrush(Color.FromRgb(0x2F, 0x81, 0xF7))
                : new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
        }
        KameraChipleriSenkronizeEt();
    }

    private void KameraChipleriSenkronizeEt()
    {
        var prefixler = KameraSubnetChips.Children
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true)
            .Select(t => t.Tag as string ?? "")
            .Where(s => s.Length > 0)
            .ToList();
        if (prefixler.Count == 0) return;
        _subnetBoxChipSenkronu = true;
        try { KameraSubnetBox.Text = string.Join(",", prefixler); }
        finally { _subnetBoxChipSenkronu = false; }
    }

    private void KameraPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _kameraCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void KameraSubnetBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        KameraSubnetPlaceholder.Visibility = string.IsNullOrEmpty(KameraSubnetBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (_subnetBoxChipSenkronu) return;

        // Kullanıcı manuel yazarken metnine uymayan chip'leri kaldır
        var metin = KameraSubnetBox.Text ?? "";
        foreach (var tb in KameraSubnetChips.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>())
        {
            if (tb.IsChecked == true && tb.Tag is string p && !metin.Contains(p, StringComparison.Ordinal))
                tb.IsChecked = false;
        }
    }

    private void KameraKolonFiltre_TextChanged(object sender, TextChangedEventArgs e)
    {
        KameraIpFiltrePlaceholder.Visibility    = string.IsNullOrWhiteSpace(KameraIpFiltreBox.Text)    ? Visibility.Visible : Visibility.Collapsed;
        KameraAdFiltrePlaceholder.Visibility    = string.IsNullOrWhiteSpace(KameraAdFiltreBox.Text)    ? Visibility.Visible : Visibility.Collapsed;
        KameraMarkaFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraMarkaFiltreBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        KameraPortFiltrePlaceholder.Visibility  = string.IsNullOrWhiteSpace(KameraPortFiltreBox.Text)  ? Visibility.Visible : Visibility.Collapsed;
        KameraMacFiltrePlaceholder.Visibility   = string.IsNullOrWhiteSpace(KameraMacFiltreBox.Text)   ? Visibility.Visible : Visibility.Collapsed;
        KameraFiltreleriUygula();
    }

    private void KameraTurFiltreDegisti(object sender, SelectionChangedEventArgs e)
        => KameraFiltreleriUygula();

    private void KameraFiltreTemizle_Click(object sender, RoutedEventArgs e)
    {
        KameraIpFiltreBox.Clear();
        KameraAdFiltreBox.Clear();
        KameraMarkaFiltreBox.Clear();
        KameraPortFiltreBox.Clear();
        KameraMacFiltreBox.Clear();
        KameraTurFiltreBox.SelectedIndex = 0;
        KameraFiltreleriUygula();
    }

    private void KameraDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (KameraDataGrid.SelectedItem is not KameraSatir satir) return;
        KameraWebArayuzunuAc(satir);
    }

    private void KameraDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = UstOgeBul<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is null) return;
        row.IsSelected = true;
        KameraDataGrid.SelectedItem = row.Item;
    }

    private void KameraMenuWeb_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is { } satir)
            KameraWebArayuzunuAc(satir);
    }

    private void KameraMenuYenidenTara_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        _ = TekIpTaraAsync(satir.Ip);
    }

    private async Task TekIpTaraAsync(string ip)
    {
        if (_kameraCts is { IsCancellationRequested: false })
        {
            ToastGoster("Devam eden tarama sırasında tekil tarama yapılamaz", hata: true);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = cts.Token;

        var bilgi = _kameraBilgileri.TryGetValue(ip, out var mevcut)
            ? mevcut
            : new KameraBilgi { Ip = ip };

        KameraIlerlemeText.Text = $"{ip} yeniden taranıyor…";

        try
        {
            var acik = new List<int>();
            foreach (var port in KameraPorts)
            {
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
                    linked.CancelAfter(800);
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(ip, port, linked.Token);
                    acik.Add(port);
                }
                catch { }
            }

            lock (bilgi.AcikPortlar)
            {
                bilgi.AcikPortlar.Clear();
                bilgi.AcikPortlar.AddRange(acik);
            }

            if (acik.Contains(554))
                bilgi.RtspDurum = await RtspHizliKontrol(ip, 554, token);

            await ServisDetaylariniGuncelleAsync(ip, bilgi, acik, token);

            foreach (var hp in new[] { 80, 8080, 8443, 443, 9000 })
            {
                if (!acik.Contains(hp)) continue;
                var (sunucu, baslik) = await HttpBannerOku(ip, hp, token);
                bilgi.SunucuBasligi = sunucu;
                bilgi.SayfaBasligi = baslik;
                break;
            }

            // Tek-IP'de de derin tara probe'larını çalıştır
            var httpFpPort = new[] { 80, 8080, 443, 8443 }.FirstOrDefault(p => acik.Contains(p));
            if (httpFpPort != 0)
            {
                var fp = await HttpFingerprintService.ProbeAsync(ip, httpFpPort, token);
                if (fp is not null)
                {
                    bilgi.HttpFpMarka = fp.Marka;
                    bilgi.HttpFpTur = fp.Tur;
                    bilgi.HttpFpModel = fp.Model;
                    bilgi.KesifKaynaklari.Add("HTTP-FP");
                }
            }
            var snmpDescr = await SnmpFingerprintService.SysDescrAsync(ip, token);
            if (!string.IsNullOrWhiteSpace(snmpDescr))
            {
                bilgi.SnmpSysDescr = snmpDescr;
                bilgi.KesifKaynaklari.Add("SNMP");
            }

            var netbiosSem = new SemaphoreSlim(1);
            var denenenler = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            var loglar     = new ConcurrentBag<string>();
            await NetbiosBilgileriniGuncelleAsync(ip, bilgi, denenenler, loglar, netbiosSem, token);

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(ip, 1000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    bilgi.PingYanit = true;
                    bilgi.PingMs    = (int)reply.RoundtripTime;
                    bilgi.PingTtl   = reply.Options?.Ttl ?? 0;
                }
            }
            catch { }

            await Dispatcher.InvokeAsync(() =>
            {
                KameraKartEkleVeyaGuncelle(bilgi);
                KameraIlerlemeText.Text = $"{ip} yeniden tarandı";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            KameraIlerlemeText.Text = $"Hata: {ex.Message}";
        }
    }

    private void KameraMenuPing_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabPing;
        PingIpBox.Text = satir.Ip;
        _ = PingBaslat(satir.Ip);
    }

    private void KameraMenuPort_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabPort;
        PortIpBox.Text     = satir.Ip;
        PortAralikBox.Text = "21,22,23,53,80,139,443,445,554,8000,8080,8443,9000,34567,37777";
        _ = PortTaraBaslat(satir.Ip, PortScanService.Parse(PortAralikBox.Text));
    }

    private void KameraMenuTrace_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabTrace;
        TraceHedefBox.Text = satir.Ip;
        _ = TracerouteBaslat(satir.Ip);
    }

    private void KameraMenuDns_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabDns;
        DnsHedefBox.Text = satir.Ip;
        _ = DnsLookupBaslat(satir.Ip);
    }

    private void KameraMenuIpKopyala_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        Clipboard.SetText(satir.Ip);
        ToastGoster($"IP kopyalandı: {satir.Ip}");
    }

    private void KameraMenuFavoriEkle_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        bool eklendi = FavoriService.Ekle(satir.Ip);
        FavoriChipleriniYenile();
        FavorilerPanelGuncelle();
        ToastGoster(eklendi ? $"★ Favoriye eklendi: {satir.Ip}" : $"Zaten favoride: {satir.Ip}", hata: !eklendi);
    }

    private void KameraExportExcel_Click(object sender, RoutedEventArgs e) => KameraDisariAktar(KameraExportFormat.Excel);
    private void KameraExportPdf_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Pdf);
    private void KameraExportTxt_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Txt);
    private void KameraExportCsv_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Csv);
    private void KameraExportJson_Click(object sender, RoutedEventArgs e)  => KameraDisariAktar(KameraExportFormat.Json);

    private KameraSatir? SeciliKameraSatiri()
        => KameraDataGrid.SelectedItem as KameraSatir;

    private void KameraWebArayuzunuAc(KameraSatir satir)
    {
        var url = string.IsNullOrWhiteSpace(satir.WebUrl) ? $"http://{satir.Ip}/" : satir.WebUrl;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static T? UstOgeBul<T>(DependencyObject? baslangic) where T : DependencyObject
    {
        while (baslangic is not null)
        {
            if (baslangic is T hedef) return hedef;
            baslangic = VisualTreeHelper.GetParent(baslangic);
        }
        return null;
    }

    private enum KameraExportFormat { Excel, Pdf, Txt, Csv, Json }

    private void KameraDisariAktar(KameraExportFormat format)
    {
        var satirlar = KameraGorunenSatirlariAl();
        if (satirlar.Count == 0)
        {
            ToastGoster("Dışa aktarılacak cihaz yok", hata: true);
            return;
        }

        var (filter, ext) = format switch
        {
            KameraExportFormat.Excel => ("Excel Dosyası (*.xlsx)|*.xlsx", "xlsx"),
            KameraExportFormat.Pdf   => ("PDF Raporu (*.pdf)|*.pdf", "pdf"),
            KameraExportFormat.Txt   => ("Metin Raporu (*.txt)|*.txt", "txt"),
            KameraExportFormat.Csv   => ("CSV Dosyası (*.csv)|*.csv", "csv"),
            KameraExportFormat.Json  => ("JSON Dosyası (*.json)|*.json", "json"),
            _                        => ("Rapor (*.*)|*.*", "txt"),
        };

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title       = "Cihaz Tara Sonuçlarını Dışa Aktar",
            Filter      = filter,
            DefaultExt  = ext,
            AddExtension = true,
            FileName    = $"Cihaz_Tara_Raporu_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            switch (format)
            {
                case KameraExportFormat.Excel:
                    File.WriteAllBytes(dlg.FileName, KameraExcelXlsxOlustur(satirlar));
                    break;
                case KameraExportFormat.Pdf:
                    File.WriteAllBytes(dlg.FileName, KameraPdfQuestOlustur(satirlar));
                    break;
                case KameraExportFormat.Txt:
                    File.WriteAllText(dlg.FileName, KameraTxtOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Csv:
                    File.WriteAllText(dlg.FileName, KameraCsvOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Json:
                    File.WriteAllText(dlg.FileName, KameraJsonOlustur(satirlar), new UTF8Encoding(true));
                    break;
            }

            MesajEkle("sonuc", $"✔ Cihaz Tara raporu kaydedildi: {Path.GetFileName(dlg.FileName)}");
            ToastGoster($"Dışa aktarıldı: {Path.GetFileName(dlg.FileName)}");
            DisariAktarilanDosyayiAc(dlg.FileName);
        }
        catch (Exception ex)
        {
            HataBildir("Cihaz Tara dışa aktarma hatası", ex);
        }
    }

    private void DisariAktarilanDosyayiAc(string dosyaYolu)
    {
        try { Process.Start(new ProcessStartInfo(dosyaYolu) { UseShellExecute = true }); }
        catch (Exception ex) { HataBildir("Rapor kaydedildi ancak dosya otomatik açılamadı", ex); }
    }

    private List<KameraSatir> KameraGorunenSatirlariAl()
        => (_kameraSatirView?.Cast<object>().OfType<KameraSatir>().ToList() ?? _kameraSatirlari.ToList())
           .OrderBy(s => IpSiralamaAnahtari(s.Ip))
           .ThenBy(s => s.Ip, StringComparer.Ordinal)
           .ToList();

    private static long IpSiralamaAnahtari(string ip)
    {
        var parcalar = ip.Split('.');
        if (parcalar.Length != 4) return long.MaxValue;
        long sonuc = 0;
        foreach (var parca in parcalar)
        {
            if (!byte.TryParse(parca, out var b)) return long.MaxValue;
            sonuc = (sonuc << 8) + b;
        }
        return sonuc;
    }

    private static IEnumerable<string[]> KameraExportSatirlari(IEnumerable<KameraSatir> satirlar)
    {
        foreach (var s in satirlar)
            yield return new[] { s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis };
    }

    private static readonly string[] KameraExportBasliklari =
        { "IP", "Ad", "Tur", "Marka", "Model", "Ping", "Portlar", "Kesif", "MAC", "Uretici", "Servis" };

    private static string KameraCsvOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", KameraExportBasliklari.Select(CsvHucre)));
        foreach (var row in KameraExportSatirlari(satirlar))
            sb.AppendLine(string.Join(";", row.Select(CsvHucre)));
        return sb.ToString();
    }

    private static string KameraJsonOlustur(List<KameraSatir> satirlar)
    {
        var veri = new
        {
            Uygulama = "AgTarama",
            Tur = "Cihaz Tara",
            OlusturmaTarihi = DateTimeOffset.Now,
            Toplam = satirlar.Count,
            Cihazlar = satirlar.Select(s => new
            {
                s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Ping, s.PingMs,
                s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis, s.WebUrl,
            }).ToList(),
        };
        return System.Text.Json.JsonSerializer.Serialize(veri, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string CsvHucre(string? metin)
    {
        metin ??= "";
        metin = metin.Replace("\r", " ").Replace("\n", " ");
        return $"\"{metin.Replace("\"", "\"\"")}\"";
    }

    private static string KameraTxtOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NETWORK SNIFFER - CIHAZ TARA RAPORU");
        sb.AppendLine($"Tarih : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Cihaz : {satirlar.Count}");
        sb.AppendLine(new string('=', 110));
        foreach (var s in satirlar)
        {
            sb.AppendLine($"{s.Ip,-15}  {MetniKirp(s.Tur, 14),-14}  {MetniKirp(s.Marka, 16),-16}  {MetniKirp(s.Model, 34)}");
            sb.AppendLine($"  Ad      : {s.Ad}");
            sb.AppendLine($"  Ping    : {s.Ping}");
            sb.AppendLine($"  Portlar : {s.Portlar}");
            sb.AppendLine($"  Keşif   : {s.Kesif}");
            sb.AppendLine($"  MAC     : {s.Mac}  {s.Uretici}");
            sb.AppendLine($"  Servis  : {s.Servis}");
            sb.AppendLine(new string('-', 110));
        }
        return sb.ToString();
    }

    private static byte[] KameraExcelXlsxOlustur(List<KameraSatir> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Cihaz Tara");

        for (int i = 0; i < KameraExportBasliklari.Length; i++)
            ws.Cell(1, i + 1).Value = KameraExportBasliklari[i];

        var headerRange = ws.Range(1, 1, 1, KameraExportBasliklari.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D3B66");
        headerRange.Style.Font.Bold            = true;
        headerRange.Style.Font.FontColor       = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        int row = 2;
        foreach (var s in satirlar)
        {
            var vals = new[] { s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis };
            for (int i = 0; i < vals.Length; i++)
                ws.Cell(row, i + 1).Value = vals[i];
            if (row % 2 == 0)
                ws.Range(row, 1, row, KameraExportBasliklari.Length)
                  .Style.Fill.BackgroundColor = XLColor.FromHtml("#101722");
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] KameraPdfQuestOlustur(List<KameraSatir> satirlar)
    {
        var rows = satirlar.Select(s => new DeviceScanRow(
            s.Ip, s.Ad, s.Tur, s.Marka, s.Model,
            s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis)).ToList();
        return PdfReportService.GenerateDeviceScanReport(rows, new ReportMetadata());
    }

    private static string MetniKirp(string? metin, int uzunluk)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "";
        metin = Regex.Replace(metin.Trim(), @"\s+", " ");
        return metin.Length <= uzunluk ? metin : metin[..Math.Max(0, uzunluk - 1)] + "…";
    }

    private void KameraBaslatBtn_Click(object sender, RoutedEventArgs e) => _ = KameraTaramaBaslat();
    private void KameraDurdurBtn_Click(object sender, RoutedEventArgs e) => _kameraCts?.Cancel();

    private async Task NetbiosBilgileriniGuncelleAsync(
        string ip, KameraBilgi bilgi,
        ConcurrentDictionary<string, byte> denenenler,
        ConcurrentBag<string> logSatirlari,
        SemaphoreSlim netbiosSem,
        CancellationToken token)
    {
        if (!denenenler.TryAdd(ip, 0)) return;
        await netbiosSem.WaitAsync(token);
        try
        {
            var netbios = await NetbiosService.SorgulaAsync(ip, token);
            if (netbios is null) return;
            bilgi.NetbiosCihazAdi = netbios.NetbiosAdi;
            bilgi.NetbiosGrupAdi  = netbios.GrupAdi;
            bilgi.DnsAdi          = netbios.DnsAdi;
            bilgi.PingAdi         = netbios.PingAdi;
            bilgi.KesifKaynaklari.Add("NetBIOS");
            var ozet = string.Join(" / ", new[] { CihazAdiSec(bilgi), netbios.GrupAdi }.Where(x => !string.IsNullOrWhiteSpace(x)));
            logSatirlari.Add($"{ip} NetBIOS: {ozet}");
            await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
        }
        finally { netbiosSem.Release(); }
    }

    private async Task NetbiosSweepAsync(
        string subnet,
        ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var sem   = new SemaphoreSlim(64);
        var tasks = Enumerable.Range(1, 254).Select(i =>
        {
            var ip = $"{subnet}.{i}";
            return Task.Run(async () =>
            {
                await sem.WaitAsync(token);
                try
                {
                    var netbios = await NetbiosService.NodeStatusAsync(ip, token);
                    if (netbios is null || string.IsNullOrWhiteSpace(netbios.NetbiosAdi)) return;
                    var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                    bilgi.NetbiosCihazAdi ??= netbios.NetbiosAdi;
                    bilgi.NetbiosGrupAdi  ??= netbios.GrupAdi;
                    logSatirlari.Add($"{ip} NetBIOS UDP: {netbios.NetbiosAdi} {netbios.GrupAdi}");
                    await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                }
                catch { }
                finally { sem.Release(); }
            }, token);
        });
        await Task.WhenAll(tasks);
    }

    // ─── mDNS / Bonjour Sweep ────────────────────────────────────────────────

    private static readonly (string Servis, string Marka, string Tur)[] MdnsServisler =
    {
        ("_apple-mobdev2._tcp.local",   "Apple",   "Telefon"),
        ("_apple-mobdev._tcp.local",    "Apple",   "Telefon"),
        ("_airplay._tcp.local",         "Apple",   "Apple TV"),
        ("_raop._tcp.local",            "Apple",   "Apple TV"),
        ("_home-sharing._tcp.local",    "Apple",   "Bilgisayar"),
        ("_companion-link._tcp.local",  "Apple",   "Bilgisayar"),
        ("_hap._tcp.local",             "Apple",   "Akıllı Cihaz"),     // HomeKit
        ("_googlecast._tcp.local",      "Google",  "Akıllı TV"),
        ("_androidtvremote._tcp.local", "Google",  "Akıllı TV"),
        ("_hue._tcp.local",             "Philips", "Akıllı Cihaz"),     // Hue Bridge
        ("_sonos._tcp.local",           "Sonos",   "Hoparlör"),
        ("_spotify-connect._tcp.local", "",        "Müzik Cihazı"),
        ("_amzn-wplay._tcp.local",      "Amazon",  "Akıllı TV"),
        ("_axis-video._tcp.local",      "Axis",    "Kamera"),
        ("_ipp._tcp.local",             "",        "Yazıcı"),
        ("_ipps._tcp.local",            "",        "Yazıcı"),
        ("_printer._tcp.local",         "",        "Yazıcı"),
        ("_pdl-datastream._tcp.local",  "",        "Yazıcı"),
        ("_smb._tcp.local",             "",        "Bilgisayar"),
        ("_workstation._tcp.local",     "",        "Bilgisayar"),
        ("_ssh._tcp.local",             "",        "Bilgisayar"),
        ("_http._tcp.local",            "",        "Cihaz"),
        ("_https._tcp.local",           "",        "Cihaz"),
        ("_device-info._tcp.local",     "",        "Cihaz"),
    };

    private async Task MdnsSweepAsync(
        string subnet,
        ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var multicast = System.Net.IPAddress.Parse("224.0.0.251");
        const int mdnsPort = 5353;

        var anahtarlar = MdnsServisler
            .Select(s => (Anahtar: s.Servis.Split('.')[0].ToLowerInvariant(), s.Marka, s.Tur))
            .ToArray();

        try
        {
            using var udp = new System.Net.Sockets.UdpClient(System.Net.Sockets.AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                                       System.Net.Sockets.SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;
            udp.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, mdnsPort));
            udp.JoinMulticastGroup(multicast);
            udp.MulticastLoopback = false;

            var hedefEp = new System.Net.IPEndPoint(multicast, mdnsPort);
            foreach (var (servis, _, _) in MdnsServisler)
            {
                try
                {
                    var sorgu = OlusturMdnsSorgusu(servis);
                    await udp.SendAsync(sorgu, sorgu.Length, hedefEp);
                    await Task.Delay(20, token);
                }
                catch { }
            }

            var bitis = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < bitis && !token.IsCancellationRequested)
            {
                try
                {
                    using var zaman = CancellationTokenSource.CreateLinkedTokenSource(token);
                    zaman.CancelAfter(500);
                    var alindi     = await udp.ReceiveAsync(zaman.Token);
                    var kaynakIp   = alindi.RemoteEndPoint.Address.ToString();
                    if (!kaynakIp.StartsWith(subnet + ".")) continue;
                    var (marka, tur) = MdnsPaketCoz(alindi.Buffer, anahtarlar);
                    if (tur == null) continue;
                    var bilgi = bulunanlar.GetOrAdd(kaynakIp, new KameraBilgi { Ip = kaynakIp });
                    if (string.IsNullOrEmpty(bilgi.MdnsTur))
                    {
                        bilgi.MdnsTur   = tur;
                        bilgi.MdnsMarka = marka;
                        bilgi.KesifKaynaklari.Add("mDNS");
                        logSatirlari.Add($"mDNS: {kaynakIp} → {tur} ({marka})");
                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }
        catch (Exception ex) { logSatirlari.Add($"mDNS hata: {ex.Message}"); }
    }

    private static byte[] OlusturMdnsSorgusu(string servisAdi)
    {
        var adBytes = new List<byte>();
        foreach (var etiket in servisAdi.Split('.'))
        {
            var b = Encoding.ASCII.GetBytes(etiket);
            adBytes.Add((byte)b.Length);
            adBytes.AddRange(b);
        }
        adBytes.Add(0);
        var paket = new byte[12 + adBytes.Count + 4];
        paket[5] = 1;
        adBytes.CopyTo(0, paket, 12, adBytes.Count);
        paket[12 + adBytes.Count + 1] = 0x0C;
        paket[12 + adBytes.Count + 3] = 0x01;
        return paket;
    }

    private static (string Marka, string? Tur) MdnsPaketCoz(
        byte[] veri,
        (string Anahtar, string Marka, string Tur)[] anahtarlar)
    {
        var str = Encoding.Latin1.GetString(veri).ToLowerInvariant();
        foreach (var (anahtar, marka, tur) in anahtarlar)
            if (str.Contains(anahtar))
                return (marka, tur);
        return ("", null);
    }

    private async Task KameraTaramaBaslat()
    {
        var giris = KameraSubnetBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(giris))
        {
            var otomatik = YerelSubnetleriBul();
            giris = string.Join(",", otomatik);
            KameraSubnetBox.Text = giris;
        }

        var subnetler = SubnetGirdisiniCoz(giris);
        if (subnetler.Count == 0)
        {
            KameraKutucugaYaz("Gecerli subnet/CIDR bulunamadi. Ornek: 192.168.1 veya 192.168.1.0/24", "#F85149");
            return;
        }

        _kameraSatirlari.Clear();
        _kameraSatirlar.Clear();
        _kameraBilgileri.Clear();
        KameraFiltreSayacText.Text = "0 cihaz";
        KameraResultBorder.Visibility = Visibility.Visible;
        KameraIlerlemeText.Visibility = Visibility.Visible;
        KameraBaslatBtn.IsEnabled = false;
        KameraDurdurBtn.Visibility = Visibility.Visible;
        KameraIlerlemeText.Text = "Baslatiliyor...";

        bool derinTara = KameraDerinTaraCheck?.IsChecked == true;

        _kameraCts?.Cancel();
        _kameraCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = _kameraCts.Token;

        var bulunanlar = new ConcurrentDictionary<string, KameraBilgi>(StringComparer.Ordinal);
        var netbiosDenenenler = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var logSatirlari = new ConcurrentBag<string>();
        using var netbiosSem = new SemaphoreSlim(16);
        int taranan = 0;
        int toplamHost = subnetler.Count * 254;

        KameraKutucugaYaz($"Hedef: {string.Join(", ", subnetler.Select(x => x.Cidr))}", "#8B949E");
        KameraKutucugaYaz($"Portlar: {string.Join(", ", KameraPorts)}", "#484F58");
        KameraKutucugaYaz(derinTara
            ? "Kaynak: ICMP + TCP port + DNS + NetBIOS + ONVIF + WSD + SSDP + mDNS + ARP + IPScanner + Ubiquiti + MikroTik + SNMP + HTTP-FP"
            : "Kaynak: ICMP + TCP port + DNS + NetBIOS + ONVIF + WSD + SSDP + mDNS + ARP + IPScanner", "#484F58");
        if (derinTara) KameraKutucugaYaz("Derin tara aktif — ek protokoller çalışıyor", "#3FB950");
        KameraKutucugaYaz("─────────────────────────", "#30363D");

        try
        {
            foreach (var subnet in subnetler)
            {
                token.ThrowIfCancellationRequested();
                KameraKutucugaYaz($"Taranan subnet: {subnet.Cidr}", "#8B949E");
                await TaramaYurutAsync(subnet.Prefix);
            }

            await ArpBilgileriniTopluGuncelleAsync(bulunanlar, logSatirlari, token);

            var sonuc = token.IsCancellationRequested
                ? $"Durduruldu - {bulunanlar.Count} cihaz bulundu"
                : $"Tamamlandi - {bulunanlar.Count} cihaz bulundu";
            KameraKutucugaYaz("─────────────────────────", "#30363D");
            KameraKutucugaYaz(sonuc, bulunanlar.Count > 0 ? "#3FB950" : "#D29922");
            KameraIlerlemeText.Text = sonuc;
            if (!token.IsCancellationRequested)
                ToastGoster($"Cihaz Tara tamamlandı — {bulunanlar.Count} cihaz bulundu");

            var loglar = logSatirlari.ToList();
            var hedefCidr = string.Join(",", subnetler.Select(x => x.Cidr));
            LogService.Kaydet("CİHAZ TARA", hedefCidr, loglar);

            var cihazlar = bulunanlar.Values
                .Select(KameraSatirOlustur)
                .OrderBy(s => IpSiralamaAnahtari(s.Ip))
                .ThenBy(s => s.Ip, StringComparer.Ordinal)
                .ToList();

            if (!_gecmisdenCalistiriliyor)
            {
                HistoryService.Kaydet("CİHAZ TARA", hedefCidr, sonuc, loglar,
                    new Dictionary<string, string>
                    {
                        ["Subnet"] = string.Join(",", subnetler.Select(x => x.Prefix)),
                        ["SubnetInput"] = string.Join(",", subnetler.Select(x => x.Cidr)),
                        ["CihazSayisi"] = bulunanlar.Count.ToString(),
                        ["CihazlarJson"] = KameraJsonOlustur(cihazlar),
                    });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
            }
            _gecmisdenCalistiriliyor = false;
        }
        catch (OperationCanceledException)
        {
            KameraIlerlemeText.Text = "Tarama durduruldu.";
        }
        catch (Exception ex)
        {
            KameraKutucugaYaz($"Hata: {ex.Message}", "#F85149");
        }
        finally
        {
            KameraBaslatBtn.IsEnabled = true;
            KameraDurdurBtn.Visibility = Visibility.Collapsed;
        }

        async Task TaramaYurutAsync(string subnet)
        {
            var portTask = Task.Run(async () =>
            {
                var sem = new SemaphoreSlim(80);
                var tasks = Enumerable.Range(1, 254).Select(i =>
                {
                    var ip = $"{subnet}.{i}";
                    return Task.Run(async () =>
                    {
                        await sem.WaitAsync(token);
                        try
                        {
                            var acik = new List<int>();
                            foreach (var port in KameraPorts)
                            {
                                if (token.IsCancellationRequested) break;
                                try
                                {
                                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
                                    linked.CancelAfter(800);
                                    using var tcp = new TcpClient();
                                    await tcp.ConnectAsync(ip, port, linked.Token);
                                    acik.Add(port);
                                }
                                catch { }
                            }

                            if (acik.Count > 0)
                            {
                                var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                                lock (bilgi.AcikPortlar) bilgi.AcikPortlar.AddRange(acik.Except(bilgi.AcikPortlar));
                                bilgi.KesifKaynaklari.Add("Port");
                                if (acik.Contains(554))
                                    bilgi.RtspDurum = await RtspHizliKontrol(ip, 554, token);
                                await ServisDetaylariniGuncelleAsync(ip, bilgi, acik, token);
                                foreach (var hp in new[] { 80, 8080, 8443, 443, 9000 })
                                {
                                    if (!acik.Contains(hp)) continue;
                                    var (sunucu, baslik) = await HttpBannerOku(ip, hp, token);
                                    bilgi.SunucuBasligi = sunucu;
                                    bilgi.SayfaBasligi = baslik;
                                    break;
                                }
                                if (derinTara)
                                {
                                    var httpFpPort = new[] { 80, 8080, 443, 8443 }.FirstOrDefault(p => acik.Contains(p));
                                    if (httpFpPort != 0)
                                    {
                                        var fp = await HttpFingerprintService.ProbeAsync(ip, httpFpPort, token);
                                        if (fp is not null)
                                        {
                                            bilgi.HttpFpMarka = fp.Marka;
                                            bilgi.HttpFpTur = fp.Tur;
                                            bilgi.HttpFpModel = fp.Model;
                                            bilgi.KesifKaynaklari.Add("HTTP-FP");
                                            logSatirlari.Add($"{ip} HTTP-FP: {fp.Marka}/{fp.Tur} {fp.Model} ({fp.Kaynak})");
                                        }
                                    }
                                }
                                if (acik.Any(p => p is 139 or 445 or 3389))
                                    await NetbiosBilgileriniGuncelleAsync(ip, bilgi, netbiosDenenenler, logSatirlari, netbiosSem, token);
                                logSatirlari.Add($"{ip}: port={string.Join(",", bilgi.AcikPortlar)} marka={KimlikBelirle(bilgi).Marka}");
                                await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                            }
                        }
                        finally
                        {
                            sem.Release();
                            int n = Interlocked.Increment(ref taranan);
                            if (n % 32 == 0)
                                await Dispatcher.InvokeAsync(() => KameraIlerlemeText.Text = $"Cihaz tarama: {n}/{toplamHost} kontrol edildi...");
                        }
                    }, token);
                });
                await Task.WhenAll(tasks);
            }, token);

            var onvifTask = Task.Run(async () =>
            {
                try
                {
                    string onvifProbe = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Envelope xmlns=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:tns=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\" xmlns:wsa=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\"><Header><wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action><wsa:MessageID>uuid:{Guid.NewGuid()}</wsa:MessageID><wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To></Header><Body><tns:Probe><tns:Types>dn:NetworkVideoTransmitter</tns:Types></tns:Probe></Body></Envelope>";
                    // WSD Probe (Windows-discoverable: yazıcı/tarayıcı/PC). wsdp:Device — Devices Profile for Web Services.
                    string wsdProbe = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:wsa=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\" xmlns:wsd=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\" xmlns:wsdp=\"http://schemas.xmlsoap.org/ws/2006/02/devprof\"><soap:Header><wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To><wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action><wsa:MessageID>uuid:{Guid.NewGuid()}</wsa:MessageID></soap:Header><soap:Body><wsd:Probe><wsd:Types>wsdp:Device</wsd:Types></wsd:Probe></soap:Body></soap:Envelope>";

                    using var udp = new UdpClient();
                    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    var hedef = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702);
                    var onvifBytes = Encoding.UTF8.GetBytes(onvifProbe);
                    var wsdBytes   = Encoding.UTF8.GetBytes(wsdProbe);
                    await udp.SendAsync(onvifBytes, onvifBytes.Length, hedef);
                    await udp.SendAsync(wsdBytes,   wsdBytes.Length,   hedef);

                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts2.CancelAfter(5000); // Hem ONVIF hem WSD yanıtları için biraz daha uzun
                    while (!cts2.Token.IsCancellationRequested)
                    {
                        var res = await udp.ReceiveAsync(cts2.Token);
                        var xml = Encoding.UTF8.GetString(res.Buffer);
                        if (!xml.Contains("ProbeMatch")) continue;
                        var ip = res.RemoteEndPoint.Address.ToString();
                        if (!ip.StartsWith(subnet + ".", StringComparison.Ordinal)) continue;

                        var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                        bool onvif = xml.Contains("NetworkVideoTransmitter", StringComparison.OrdinalIgnoreCase) ||
                                     xml.Contains("onvif://", StringComparison.OrdinalIgnoreCase);
                        bool wsd = xml.Contains("wsdp:", StringComparison.OrdinalIgnoreCase) ||
                                   xml.Contains("PrintDeviceType", StringComparison.OrdinalIgnoreCase) ||
                                   xml.Contains("NetworkPrinter", StringComparison.OrdinalIgnoreCase) ||
                                   xml.Contains("schemas.microsoft.com/windows/pnpx", StringComparison.OrdinalIgnoreCase);

                        if (onvif)
                        {
                            var xM = Regex.Match(xml, @"<[^>]*XAddrs[^>]*>([^<]+)</[^>]*XAddrs>");
                            var scopes = Regex.Matches(xml, @"onvif://www\.onvif\.org/(\w+)/([^<\s""]+)");
                            bilgi.OnvifBulundu = true;
                            bilgi.OnvifServisUrl = xM.Success ? xM.Groups[1].Value.Trim().Split(' ')[0] : null;
                            var scopeDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (Match m in scopes) scopeDict[m.Groups[1].Value] = Uri.UnescapeDataString(m.Groups[2].Value);
                            if (scopeDict.TryGetValue("hardware", out var hw)) bilgi.OnvifHardware = TemizKimlikMetni(hw);
                            if (scopeDict.TryGetValue("name", out var nm)) bilgi.OnvifAdi = TemizKimlikMetni(nm);
                            if (scopeDict.TryGetValue("location", out var loc)) bilgi.OnvifKonum = TemizKimlikMetni(loc);
                            bilgi.KesifKaynaklari.Add("ONVIF");
                            if (!string.IsNullOrWhiteSpace(bilgi.OnvifAdi) || !string.IsNullOrWhiteSpace(bilgi.OnvifHardware))
                                logSatirlari.Add($"{ip} ONVIF: {bilgi.OnvifAdi} {bilgi.OnvifHardware}");
                        }
                        if (wsd)
                        {
                            // WSD Types alanından cihaz türünü çıkar
                            if (xml.Contains("PrintDeviceType", StringComparison.OrdinalIgnoreCase) ||
                                xml.Contains("NetworkPrinter", StringComparison.OrdinalIgnoreCase))
                                bilgi.WsdTipi = "Yazıcı";
                            else if (xml.Contains("ScanDeviceType", StringComparison.OrdinalIgnoreCase))
                                bilgi.WsdTipi = "Tarayıcı";
                            else if (xml.Contains("Computer", StringComparison.OrdinalIgnoreCase))
                                bilgi.WsdTipi = "Bilgisayar";
                            else
                                bilgi.WsdTipi = "WSD";
                            bilgi.KesifKaynaklari.Add("WSD");
                            logSatirlari.Add($"{ip} WSD: {bilgi.WsdTipi}");
                        }
                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, token);

            var ssdpTask = Task.Run(async () =>
            {
                try
                {
                    var bytes = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n");
                    using var udp = new UdpClient();
                    udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
                    using var cts3 = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts3.CancelAfter(3000);
                    while (!cts3.Token.IsCancellationRequested)
                    {
                        var res = await udp.ReceiveAsync(cts3.Token);
                        var resp = Encoding.UTF8.GetString(res.Buffer);
                        var ip = res.RemoteEndPoint.Address.ToString();
                        if (!ip.StartsWith(subnet + ".", StringComparison.Ordinal)) continue;

                        var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                        bilgi.SsdpBulundu = true;
                        bilgi.KesifKaynaklari.Add("SSDP");
                        var headers = HttpBasliklariniParse(resp);
                        if (headers.TryGetValue("SERVER", out var ssdpServer)) bilgi.SsdpSunucu = TemizKimlikMetni(ssdpServer);
                        if (headers.TryGetValue("LOCATION", out var location))
                        {
                            bilgi.SsdpLocation = location.Trim();
                            var ssdpDetay = await SsdpDetayOku(bilgi.SsdpLocation, token);
                            bilgi.SsdpFriendlyName = ssdpDetay.FriendlyName ?? bilgi.SsdpFriendlyName;
                            bilgi.SsdpManufacturer = ssdpDetay.Manufacturer ?? bilgi.SsdpManufacturer;
                            bilgi.SsdpModelName = ssdpDetay.ModelName ?? bilgi.SsdpModelName;
                            bilgi.SsdpModelNumber = ssdpDetay.ModelNumber ?? bilgi.SsdpModelNumber;
                        }
                        logSatirlari.Add($"{ip} UPnP/SSDP: {IlkDolu(bilgi.SsdpFriendlyName, bilgi.SsdpManufacturer, bilgi.SsdpModelName, bilgi.SsdpSunucu)}");
                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, token);

            var pingSweepTask = Task.Run(async () =>
            {
                var sem = new SemaphoreSlim(64);
                var tasks = Enumerable.Range(1, 254).Select(i =>
                {
                    var ip = $"{subnet}.{i}";
                    return Task.Run(async () =>
                    {
                        await sem.WaitAsync(token);
                        try
                        {
                            using var ping = new System.Net.NetworkInformation.Ping();
                            var reply = await ping.SendPingAsync(ip, 1000);
                            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                                bilgi.PingYanit = true;
                                bilgi.PingMs = (int)reply.RoundtripTime;
                                bilgi.PingTtl = reply.Options?.Ttl ?? 0;
                                bilgi.KesifKaynaklari.Add("Ping");
                                await NetbiosBilgileriniGuncelleAsync(ip, bilgi, netbiosDenenenler, logSatirlari, netbiosSem, token);
                                await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                            }
                        }
                        catch { }
                        finally { sem.Release(); }
                    }, token);
                });
                await Task.WhenAll(tasks);
            }, token);

            var mdnsTask = Task.Run(() => MdnsSweepAsync(subnet, bulunanlar, logSatirlari, token), token);
            var advancedScannerTask = Task.Run(() => AdvancedScannerKayitlariniIsleAsync(subnet, bulunanlar, logSatirlari, token), token);
            var netbiosSweepTask = Task.Run(() => NetbiosSweepAsync(subnet, bulunanlar, logSatirlari, token), token);

            var ekTasks = new List<Task>();
            if (derinTara)
            {
                ekTasks.Add(Task.Run(() => UbiquitiSweepAsync(subnet, bulunanlar, logSatirlari, token), token));
                ekTasks.Add(Task.Run(() => MndpSweepAsync(subnet, bulunanlar, logSatirlari, token), token));
                ekTasks.Add(Task.Run(() => SnmpSweepAsync(subnet, bulunanlar, logSatirlari, token), token));
            }

            await Task.WhenAll(new[] { portTask, onvifTask, ssdpTask, pingSweepTask, mdnsTask, advancedScannerTask, netbiosSweepTask }
                .Concat(ekTasks));
        }
    }

    private async Task UbiquitiSweepAsync(string subnet, ConcurrentDictionary<string, KameraBilgi> bulunanlar,
                                          ConcurrentBag<string> logSatirlari, CancellationToken token)
    {
        try
        {
            var kayitlar = await UbiquitiDiscoveryService.TaraAsync(subnet, token).ConfigureAwait(false);
            foreach (var k in kayitlar)
            {
                var bilgi = bulunanlar.GetOrAdd(k.Ip, new KameraBilgi { Ip = k.Ip });
                if (k.Mac != null) bilgi.MacAdresi ??= k.Mac;
                bilgi.UbntPlatform = k.Platform ?? k.ModelKodu;
                bilgi.UbntFirmware = k.Firmware;
                bilgi.UbntHostname = k.Hostname;
                bilgi.Uretici ??= "Ubiquiti";
                bilgi.KesifKaynaklari.Add("Ubiquiti");
                logSatirlari.Add($"{k.Ip} Ubiquiti: {k.Platform ?? k.ModelKodu} ({k.Hostname})");
                await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
            }
        }
        catch (Exception ex) { logSatirlari.Add($"Ubiquiti hata: {ex.Message}"); }
    }

    private async Task MndpSweepAsync(string subnet, ConcurrentDictionary<string, KameraBilgi> bulunanlar,
                                      ConcurrentBag<string> logSatirlari, CancellationToken token)
    {
        try
        {
            var kayitlar = await MndpDiscoveryService.TaraAsync(subnet, token).ConfigureAwait(false);
            foreach (var k in kayitlar)
            {
                var bilgi = bulunanlar.GetOrAdd(k.Ip, new KameraBilgi { Ip = k.Ip });
                if (k.Mac != null) bilgi.MacAdresi ??= k.Mac;
                bilgi.MikroTikBoard = k.Board;
                bilgi.MikroTikVersion = k.Version;
                bilgi.MikroTikIdentity = k.Identity;
                bilgi.Uretici ??= "MikroTik";
                bilgi.KesifKaynaklari.Add("MNDP");
                logSatirlari.Add($"{k.Ip} MikroTik: {k.Board} v{k.Version} ({k.Identity})");
                await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
            }
        }
        catch (Exception ex) { logSatirlari.Add($"MNDP hata: {ex.Message}"); }
    }

    private async Task SnmpSweepAsync(string subnet, ConcurrentDictionary<string, KameraBilgi> bulunanlar,
                                      ConcurrentBag<string> logSatirlari, CancellationToken token)
    {
        try
        {
            using var sem = new SemaphoreSlim(32);
            var tasks = Enumerable.Range(1, 254).Select(i =>
            {
                var ip = $"{subnet}.{i}";
                return Task.Run(async () =>
                {
                    await sem.WaitAsync(token);
                    try
                    {
                        var sysDescr = await SnmpFingerprintService.SysDescrAsync(ip, token).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(sysDescr)) return;
                        var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                        bilgi.SnmpSysDescr = sysDescr;
                        var sysName = await SnmpFingerprintService.SysNameAsync(ip, token).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(sysName)) bilgi.SnmpSysName = sysName;
                        bilgi.KesifKaynaklari.Add("SNMP");
                        logSatirlari.Add($"{ip} SNMP: {sysDescr}");
                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                    catch { }
                    finally { sem.Release(); }
                }, token);
            });
            await Task.WhenAll(tasks);
        }
        catch (Exception ex) { logSatirlari.Add($"SNMP hata: {ex.Message}"); }
    }

    private static async Task<(string? Sunucu, string? Baslik)> HttpBannerOku(string ip, int port, CancellationToken token)
    {
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2500);
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2500;
            var req = Encoding.ASCII.GetBytes($"GET / HTTP/1.0\r\nHost: {ip}\r\nUser-Agent: AgTarama/1.0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(req, cts.Token);
            var buf = new byte[4096];
            int n   = await stream.ReadAsync(buf, cts.Token);
            var resp = Encoding.UTF8.GetString(buf, 0, n);
            if (!resp.StartsWith("HTTP/", StringComparison.Ordinal) ||
                !resp.Split('\n')[0].Contains("200", StringComparison.Ordinal))
                return (null, null);
            string? sunucu = null, baslik = null;
            foreach (var line in resp.Split('\n'))
            {
                var l = line.Trim();
                if (l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))
                    sunucu = l[7..].Trim();
            }
            var tm = Regex.Match(resp, @"<title[^>]*>([^<]{1,80})</title>", RegexOptions.IgnoreCase);
            if (tm.Success) baslik = tm.Groups[1].Value.Trim();
            return (sunucu, baslik);
        }
        catch { return (null, null); }
    }

    private static async Task ServisDetaylariniGuncelleAsync(string ip, KameraBilgi bilgi, IEnumerable<int> acikPortlar, CancellationToken token)
    {
        foreach (var port in acikPortlar)
        {
            if (!BilindikPortlar.TryGetValue(port, out var servis)) servis = "Bilinmeyen";
            var banner = await PortBannerOku(ip, port, token);
            var detay  = banner == null ? servis : $"{servis} - {banner}";
            lock (bilgi.ServisDetaylari) bilgi.ServisDetaylari[port] = detay;
        }
    }

    private static async Task<string?> PortBannerOku(string ip, int port, CancellationToken token)
    {
        if (port is 80 or 8080 or 8000 or 9000)
        {
            var (sunucu, baslik) = await HttpBannerOku(ip, port, token);
            return IlkDolu(sunucu, AnlamliSayfaBasligi(baslik));
        }
        if (port == 554) return await RtspHizliKontrol(ip, port, token);
        if (port is 443 or 8443 or 445 or 3389) return null;
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(1200);
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 1200;
            if (port is 23 or 21 or 22 or 25 or 110 or 143)
            {
                var buf = new byte[256];
                int n   = await stream.ReadAsync(buf, cts.Token);
                return BannerTemizle(Encoding.ASCII.GetString(buf, 0, n));
            }
        }
        catch { }
        return null;
    }

    private static string? BannerTemizle(string? banner)
    {
        if (string.IsNullOrWhiteSpace(banner)) return null;
        var temiz = Regex.Replace(banner, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", " ");
        temiz = Regex.Replace(temiz, @"\s+", " ").Trim();
        return temiz.Length > 90 ? temiz[..90] : temiz;
    }

    private static async Task<string?> RtspHizliKontrol(string ip, int port, CancellationToken token)
    {
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2000);
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2000;
            var req = Encoding.ASCII.GetBytes($"DESCRIBE rtsp://{ip}:{port}/ RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: AgTarama/1.0\r\n\r\n");
            await stream.WriteAsync(req, cts.Token);
            var buf = new byte[256];
            int n   = await stream.ReadAsync(buf, cts.Token);
            var first = Encoding.ASCII.GetString(buf, 0, n).Split('\n')[0].Trim();
            return first.Length > 9 ? first[9..] : first;
        }
        catch { return null; }
    }

    private static Dictionary<string, string> HttpBasliklariniParse(string yanit)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in yanit.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            var idx  = line.IndexOf(':');
            if (idx <= 0) continue;
            headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return headers;
    }

    private static async Task<(string? FriendlyName, string? Manufacturer, string? ModelName, string? ModelNumber)> SsdpDetayOku(string location, CancellationToken token)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri)) return default;
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2200);
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(2200) };
            var xml = await client.GetStringAsync(uri, cts.Token);
            return (
                XmlEtiketiOku(xml, "friendlyName"),
                XmlEtiketiOku(xml, "manufacturer"),
                XmlEtiketiOku(xml, "modelName"),
                XmlEtiketiOku(xml, "modelNumber"));
        }
        catch { return default; }
    }

    private static string? XmlEtiketiOku(string xml, string etiket)
    {
        var match = Regex.Match(xml, $@"<{Regex.Escape(etiket)}(?:\s[^>]*)?>(?<v>.*?)</{Regex.Escape(etiket)}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return match.Success ? TemizKimlikMetni(match.Groups["v"].Value) : null;
    }

    private async Task AdvancedScannerKayitlariniIsleAsync(
        string subnet,
        ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var kayitlar = await AdvancedIpScannerService.TaraAsync(subnet, token);
        foreach (var kayit in kayitlar)
        {
            if (token.IsCancellationRequested) break;
            var bilgi = bulunanlar.GetOrAdd(kayit.Ip, new KameraBilgi { Ip = kayit.Ip });
            bilgi.AdvancedScannerAdi      = TemizKimlikMetni(kayit.Ad);
            bilgi.AdvancedScannerServisler = TemizKimlikMetni(kayit.Servisler);
            if (!string.IsNullOrWhiteSpace(kayit.Mac)) bilgi.MacAdresi = MacFormatla(kayit.Mac);
            bilgi.Uretici = IlkDolu(kayit.Uretici, bilgi.Uretici, UreticiAra(bilgi.MacAdresi));
            logSatirlari.Add($"{kayit.Ip} Advanced IP Scanner: {CihazAdiSec(bilgi)} {bilgi.MacAdresi} {bilgi.Uretici}");
            await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
        }
    }

    private async Task ArpBilgileriniTopluGuncelleAsync(
        ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var arp = await ArpTablosuOkuAsync(token);
        foreach (var (ip, bilgi) in bulunanlar)
        {
            if (arp.TryGetValue(ip, out var mac))
            {
                bilgi.MacAdresi = MacFormatla(mac);
                bilgi.KesifKaynaklari.Add("ARP");
            }
            // MAC bilgisi varsa, üreticiyi sırayla: önce mevcut, sonra OUI tablosu, sonra dahili fallback
            if (!string.IsNullOrWhiteSpace(bilgi.MacAdresi))
                bilgi.Uretici = IlkDolu(bilgi.Uretici, UreticiAra(bilgi.MacAdresi), OuiVendorLookup.Bul(bilgi.MacAdresi));
            if (!string.IsNullOrWhiteSpace(bilgi.MacAdresi))
                logSatirlari.Add($"{ip} ARP: {bilgi.MacAdresi} {bilgi.Uretici}");
            await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
        }
    }

    private static async Task<Dictionary<string, string>> ArpTablosuOkuAsync(CancellationToken token)
    {
        var sonuc = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "arp",
                    Arguments              = "-a",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                },
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            foreach (Match m in Regex.Matches(output, @"(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9A-Fa-f]{2}(?:[-:][0-9A-Fa-f]{2}){5})"))
                sonuc[m.Groups["ip"].Value] = MacFormatla(m.Groups["mac"].Value) ?? m.Groups["mac"].Value;
        }
        catch { }
        return sonuc;
    }

    private static readonly object MacDbLock = new();
    private static Dictionary<string, string>? _ipScannerMacDb;

    private static string? UreticiAra(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var yerel = OuiAra(mac);
        if (!string.IsNullOrWhiteSpace(yerel)) return yerel;
        var prefix = Regex.Replace(mac, @"[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (prefix.Length < 6) return null;
        prefix = prefix[..6];
        lock (MacDbLock)
        {
            _ipScannerMacDb ??= IpScannerMacDbYukle();
            return _ipScannerMacDb.TryGetValue(prefix, out var uretici) ? uretici : null;
        }
    }

    private static Dictionary<string, string> IpScannerMacDbYukle()
    {
        var db = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(Paths.IpScannerMacDb)) return db;
            foreach (var line in File.ReadLines(Paths.IpScannerMacDb))
            {
                var match = Regex.Match(line, @"^(?<hex>[0-9A-Fa-f]{12})\s+(?<vendor>.+)$");
                if (!match.Success) continue;
                var hex = match.Groups["hex"].Value.ToUpperInvariant();
                if (!hex.EndsWith("FFFFFF", StringComparison.Ordinal)) continue;
                var prefix = hex[..6];
                db.TryAdd(prefix, TemizKimlikMetni(match.Groups["vendor"].Value) ?? match.Groups["vendor"].Value.Trim());
            }
        }
        catch { }
        return db;
    }

    private static string? MacFormatla(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var hex = Regex.Replace(mac, @"[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (hex.Length != 12) return mac.Trim();
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }

    private void KameraKartEkleVeyaGuncelle(KameraBilgi bilgi)
    {
        _kameraBilgileri[bilgi.Ip] = bilgi;
        var satir = KameraSatirOlustur(bilgi);
        if (_kameraSatirlar.TryGetValue(bilgi.Ip, out var mevcut))
            mevcut.Kopyala(satir);
        else
        {
            _kameraSatirlar[bilgi.Ip] = satir;
            _kameraSatirlari.Add(satir);
        }
        KameraFiltreleriUygula();
    }

    private KameraSatir KameraSatirOlustur(KameraBilgi bilgi)
    {
        var kim      = KimlikBelirle(bilgi);
        var cihazAdi = CihazAdiSec(bilgi) ?? "";

        List<int> portlar;
        lock (bilgi.AcikPortlar) portlar = bilgi.AcikPortlar.Order().ToList();

        List<string> servisler;
        lock (bilgi.ServisDetaylari)
            servisler = bilgi.ServisDetaylari.OrderBy(x => x.Key).Select(x => $"{x.Key}/{x.Value}").ToList();

        // Keşif kaynakları: önceden tutulan KesifKaynaklari + bayraklar
        var kesifSet = new HashSet<string>(bilgi.KesifKaynaklari, StringComparer.OrdinalIgnoreCase);
        if (bilgi.OnvifBulundu) kesifSet.Add("ONVIF");
        if (bilgi.SsdpBulundu)  kesifSet.Add("UPnP");
        var kesifler = kesifSet
            .OrderBy(s => KesifSira(s))
            .ToList();

        return new KameraSatir
        {
            Ip      = bilgi.Ip,
            Ad      = cihazAdi,
            Tur     = kim.Tur,
            Marka   = kim.Marka == "Bilinmiyor" ? "" : kim.Marka,
            Model   = kim.Model ?? "",
            Ping    = bilgi.PingYanit ? $"{bilgi.PingMs} ms" : "",
            PingMs  = bilgi.PingYanit ? bilgi.PingMs : int.MaxValue,
            Portlar = string.Join(", ", portlar),
            Kesif   = string.Join(", ", kesifler),
            Mac     = bilgi.MacAdresi ?? "",
            Uretici = bilgi.Uretici ?? "",
            Servis  = string.Join(" | ", servisler.DefaultIfEmpty(IlkDolu(bilgi.AdvancedScannerServisler, bilgi.SunucuBasligi, bilgi.SayfaBasligi, bilgi.RtspDurum) ?? "")),
            WebUrl  = KameraWebUrlSec(bilgi),
            Guven   = GuvenSkoru(bilgi, kim),
        };
    }

    private static int KesifSira(string kaynak) => kaynak.ToUpperInvariant() switch
    {
        "UBIQUITI" => 0,
        "MNDP"     => 1,
        "ONVIF"    => 2,
        "WSD"      => 3,
        "UPNP"     => 4,
        "SSDP"     => 4,
        "MDNS"     => 5,
        "SNMP"     => 6,
        "HTTP-FP"  => 7,
        "NETBIOS"  => 8,
        "PORT"     => 9,
        "PING"     => 10,
        "ARP"      => 11,
        _          => 99,
    };

    private static string? KameraWebUrlSec(KameraBilgi bilgi)
    {
        List<int> portlar;
        lock (bilgi.AcikPortlar) portlar = [..bilgi.AcikPortlar];
        foreach (var (port, scheme) in new (int, string)[] { (80, "http"), (443, "https"), (8080, "http"), (8443, "https"), (9000, "http") })
        {
            if (!portlar.Contains(port)) continue;
            return port is 80 or 443 ? $"{scheme}://{bilgi.Ip}/" : $"{scheme}://{bilgi.Ip}:{port}/";
        }
        return null;
    }

    private void KameraFiltreleriUygula()
    {
        _kameraSatirView?.Refresh();
        int toplam  = _kameraSatirlari.Count;
        int gorunen = _kameraSatirView?.Cast<object>().Count() ?? toplam;
        KameraFiltreSayacText.Text = toplam == 0 ? "0 cihaz" : $"{gorunen}/{toplam} cihaz";
    }

    private bool KameraSatirFiltredenGecer(object obj)
    {
        if (obj is not KameraSatir satir) return false;
        var tur = (KameraTurFiltreBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Hepsi";
        if (string.Equals(tur, "Bilinmiyor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(satir.Tur, "Cihaz", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(satir.Tur))
                return false;
        }
        else if (!string.Equals(tur, "Hepsi", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(satir.Tur, tur, StringComparison.OrdinalIgnoreCase))
            return false;
        return Icerir(satir.Ip, KameraIpFiltreBox?.Text) &&
               Icerir($"{satir.Ad} {satir.Model}", KameraAdFiltreBox?.Text) &&
               Icerir($"{satir.Marka} {satir.Uretici}", KameraMarkaFiltreBox?.Text) &&
               Icerir($"{satir.Portlar} {satir.Servis} {satir.Kesif}", KameraPortFiltreBox?.Text) &&
               Icerir(satir.Mac, KameraMacFiltreBox?.Text);
    }

    private static bool Icerir(string? kaynak, string? filtre)
        => string.IsNullOrWhiteSpace(filtre) ||
           (kaynak?.Contains(filtre.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    private void KameraKutucugaYaz(string metin, string hex)
        => KameraIlerlemeText.Text = metin;

    // ─── KameraSatir (görünüm modeli) ────────────────────────────────

    public sealed class KameraSatir : INotifyPropertyChanged
    {
        private string  _ip      = "";
        private string  _ad      = "";
        private string  _tur     = "";
        private string  _marka   = "";
        private string  _model   = "";
        private string  _ping    = "";
        private int     _pingMs  = int.MaxValue;
        private string  _portlar = "";
        private string  _kesif   = "";
        private string  _mac     = "";
        private string  _uretici = "";
        private string  _servis  = "";
        private string? _webUrl;
        private int     _guven   = 0;

        public string  Ip      { get => _ip;      set => Set(ref _ip,      value); }
        public string  Ad      { get => _ad;      set => Set(ref _ad,      value); }
        public string  Tur     { get => _tur;     set => Set(ref _tur,     value); }
        public string  Marka   { get => _marka;   set => Set(ref _marka,   value); }
        public string  Model   { get => _model;   set => Set(ref _model,   value); }
        public string  Ping    { get => _ping;    set => Set(ref _ping,    value); }
        public int     PingMs  { get => _pingMs;  set => Set(ref _pingMs,  value); }
        public string  Portlar { get => _portlar; set => Set(ref _portlar, value); }
        public string  Kesif   { get => _kesif;   set => Set(ref _kesif,   value); }
        public string  Mac     { get => _mac;     set => Set(ref _mac,     value); }
        public string  Uretici { get => _uretici; set => Set(ref _uretici, value); }
        public string  Servis  { get => _servis;  set => Set(ref _servis,  value); }
        public string? WebUrl  { get => _webUrl;  set => Set(ref _webUrl,  value); }
        public int     Guven   { get => _guven;   set => Set(ref _guven,   value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Kopyala(KameraSatir diger)
        {
            Ip      = diger.Ip;
            Ad      = diger.Ad;
            Tur     = diger.Tur;
            Marka   = diger.Marka;
            Model   = diger.Model;
            Ping    = diger.Ping;
            PingMs  = diger.PingMs;
            Portlar = diger.Portlar;
            Kesif   = diger.Kesif;
            Mac     = diger.Mac;
            Uretici = diger.Uretici;
            Servis  = diger.Servis;
            WebUrl  = diger.WebUrl;
            Guven   = diger.Guven;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
