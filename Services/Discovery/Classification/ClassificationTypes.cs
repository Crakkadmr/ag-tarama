using System.Collections.Generic;

namespace AgTarama.Services.Discovery.Classification;

internal enum KanitKaynak
{
    HttpFp, Ubiquiti, MikroTik, Snmp, Onvif, Wsd, Ssdp, Mdns,
    Netbios, OuiMac, PortPattern, Banner, Ttl, AdHostname,
    Llmnr, Smb, Ssh, ArpActive,
}

internal sealed record TurAdayi(string Tur, int Agirlik, KanitKaynak Kaynak, string Detay);
internal sealed record MarkaAdayi(string Marka, int Agirlik, KanitKaynak Kaynak, string Detay);

internal sealed record KimlikKararIzi(
    IReadOnlyList<TurAdayi> TurAdaylari,
    IReadOnlyList<MarkaAdayi> MarkaAdaylari,
    IReadOnlyList<(string Tur, int Skor)> TurSiralama,
    IReadOnlyList<(string Marka, int Skor)> MarkaSiralama);

internal static class KanitAgirlik
{
    public const int HttpFpVendorWithModel = 55;
    public const int HttpFpProbeOnly       = 35;
    public const int Ubiquiti              = 50;
    public const int UbiquitiMarka         = 60;
    public const int MikroTik              = 50;
    public const int MikroTikMarka         = 60;
    public const int SnmpTur               = 45;
    public const int SnmpMarka             = 50;
    public const int OnvifTur              = 45;
    public const int OnvifMarka            = 25;
    public const int WsdTur                = 40;
    public const int WsdMarka              = 15;
    public const int SsdpTur               = 30;
    public const int SsdpMarka             = 35;
    public const int MdnsTur               = 40;
    public const int MdnsMarka             = 30;
    public const int NetbiosTur            = 25;
    public const int NetbiosMarka          = 5;
    public const int OuiTur                = 10;
    public const int OuiMarka              = 40;
    public const int ArpMacOuiActive       = 15;
    public const int PortPatternStrong     = 25;
    public const int PortPatternWeak       = 10;
    public const int BannerTur             = 20;
    public const int BannerMarka           = 25;
    public const int TtlTur                = 5;
    public const int AdHostnameTur         = 30;
    public const int AdHostnameMarka       = 20;
    public const int LlmnrHostname         = 15;
    public const int SmbComputerName       = 35;
    public const int SshBanner             = 25;
    public const int MinKararEsigi         = 12;
}
