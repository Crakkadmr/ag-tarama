using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using AgTarama.Services.Discovery.Models;

namespace AgTarama;

public partial class MainWindow
{
    private KameraSatir KameraSatirOlustur(DeviceInfo bilgi)
    {
        var kim      = KimlikBelirle(bilgi);
        var cihazAdi = CihazAdiSec(bilgi) ?? "";

        List<int> portlar;
        lock (bilgi.AcikPortlar) portlar = bilgi.AcikPortlar.Order().ToList();

        List<string> servisler;
        lock (bilgi.ServisDetaylari)
            servisler = bilgi.ServisDetaylari.OrderBy(x => x.Key).Select(x => $"{x.Key}/{x.Value}").ToList();

        var kesifSet = new HashSet<string>(bilgi.KesifKaynaklari, StringComparer.OrdinalIgnoreCase);
        if (bilgi.OnvifBulundu) kesifSet.Add("ONVIF");
        if (bilgi.SsdpBulundu)  kesifSet.Add("UPnP");
        var kesifler = kesifSet.OrderBy(s => KesifSira(s)).ToList();

        var durum = bilgi.Online ? "Online" : "Offline";
        var sonGorulen = bilgi.LastSeen == default
            ? ""
            : bilgi.LastSeen.Date == DateTime.Today
                ? bilgi.LastSeen.ToString("HH:mm:ss")
                : bilgi.LastSeen.ToString("dd.MM HH:mm");

        return new KameraSatir
        {
            Ip        = bilgi.Ip,
            Ad        = cihazAdi,
            Tur       = kim.Tur,
            Marka     = kim.Marka == "Bilinmiyor" ? "" : kim.Marka,
            Model     = kim.Model ?? "",
            Os        = bilgi.Os ?? "",
            Durum     = durum,
            SonGorulen = sonGorulen,
            Online    = bilgi.Online,
            Ping      = bilgi.PingYanit ? $"{bilgi.PingMs} ms" : "",
            PingMs    = bilgi.PingYanit ? bilgi.PingMs : int.MaxValue,
            Portlar   = string.Join(", ", portlar),
            Kesif     = string.Join(", ", kesifler),
            Mac       = bilgi.MacAdresi ?? "",
            Uretici   = bilgi.Uretici ?? "",
            Servis    = string.Join(" | ", servisler.DefaultIfEmpty(IlkDolu(bilgi.SunucuBasligi, bilgi.SayfaBasligi, bilgi.RtspDurum) ?? "")),
            WebUrl    = KameraWebUrlSec(bilgi),
            Guven     = GuvenSkoru(bilgi, kim),
            KararIzi  = KararIziOzetle(bilgi.KararIzi),
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
        "SMB"      => 8,
        "LLMNR"    => 9,
        "SSH"      => 9,
        "PORT"     => 10,
        "PING"     => 11,
        "ARP"      => 12,
        _          => 99,
    };

    private static string? KameraWebUrlSec(DeviceInfo bilgi)
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
        if (!_dusukGuvenGoster && satir.Guven < 12 && !satir.Online) return false;
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

    // ─── KameraSatir (görünüm modeli) ───
    public sealed class KameraSatir : INotifyPropertyChanged
    {
        private string  _ip        = "";
        private string  _ad        = "";
        private string  _tur       = "";
        private string  _marka     = "";
        private string  _model     = "";
        private string  _os        = "";
        private string  _durum     = "";
        private string  _sonGorulen = "";
        private bool    _online    = false;
        private string  _ping      = "";
        private int     _pingMs    = int.MaxValue;
        private string  _portlar   = "";
        private string  _kesif     = "";
        private string  _mac       = "";
        private string  _uretici   = "";
        private string  _servis    = "";
        private string? _webUrl;
        private int     _guven     = 0;
        private string  _kararIzi  = "";

        public string  Ip        { get => _ip;        set => Set(ref _ip,        value); }
        public string  Ad        { get => _ad;        set => Set(ref _ad,        value); }
        public string  Tur       { get => _tur;       set => Set(ref _tur,       value); }
        public string  Marka     { get => _marka;     set => Set(ref _marka,     value); }
        public string  Model     { get => _model;     set => Set(ref _model,     value); }
        public string  Os        { get => _os;        set => Set(ref _os,        value); }
        public string  Durum     { get => _durum;     set => Set(ref _durum,     value); }
        public string  SonGorulen { get => _sonGorulen; set => Set(ref _sonGorulen, value); }
        public bool    Online    { get => _online;    set => Set(ref _online,    value); }
        public string  Ping      { get => _ping;      set => Set(ref _ping,      value); }
        public int     PingMs    { get => _pingMs;    set => Set(ref _pingMs,    value); }
        public string  Portlar   { get => _portlar;   set => Set(ref _portlar,   value); }
        public string  Kesif     { get => _kesif;     set => Set(ref _kesif,     value); }
        public string  Mac       { get => _mac;       set => Set(ref _mac,       value); }
        public string  Uretici   { get => _uretici;   set => Set(ref _uretici,   value); }
        public string  Servis    { get => _servis;    set => Set(ref _servis,    value); }
        public string? WebUrl    { get => _webUrl;    set => Set(ref _webUrl,    value); }
        public int     Guven     { get => _guven;     set { if (Set(ref _guven, value)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GuvenRenk))); } }
        public string  KararIzi  { get => _kararIzi;  set => Set(ref _kararIzi,  value); }

        public string GuvenRenk => _guven >= 70 ? "Yesil" : _guven >= 40 ? "Sari" : _guven > 0 ? "Kirmizi" : "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Kopyala(KameraSatir diger)
        {
            Ip         = diger.Ip;
            Ad         = diger.Ad;
            Tur        = diger.Tur;
            Marka      = diger.Marka;
            Model      = diger.Model;
            Os         = diger.Os;
            Durum      = diger.Durum;
            SonGorulen = diger.SonGorulen;
            Online     = diger.Online;
            Ping       = diger.Ping;
            PingMs     = diger.PingMs;
            Portlar    = diger.Portlar;
            Kesif      = diger.Kesif;
            Mac        = diger.Mac;
            Uretici    = diger.Uretici;
            Servis     = diger.Servis;
            WebUrl     = diger.WebUrl;
            Guven      = diger.Guven;
            KararIzi   = diger.KararIzi;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
