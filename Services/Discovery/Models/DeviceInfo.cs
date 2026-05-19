using System;
using System.Collections.Generic;
using AgTarama.Services.Discovery.Classification;

namespace AgTarama.Services.Discovery.Models;

internal sealed class DeviceInfo
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
    public Dictionary<int, string> ServisDetaylari { get; } = new();
    public bool      PingYanit  { get; set; }
    public int       PingMs     { get; set; }
    public int       PingTtl    { get; set; }
    public string    MdnsMarka  { get; set; } = "";
    public string    MdnsTur    { get; set; } = "";
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
    public KimlikKararIzi? KararIzi { get; set; }

    // Yeni alanlar (Faz 6)
    public string?   Os              { get; set; }
    public string?   LlmnrHostname   { get; set; }
    public string?   SmbComputerName { get; set; }
    public string?   SmbOs           { get; set; }
    public string?   SshBanner       { get; set; }
    public DateTime  FirstSeen       { get; set; } = DateTime.Now;
    public DateTime  LastSeen        { get; set; } = DateTime.Now;
    public bool      Online          { get; set; } = false;
}
