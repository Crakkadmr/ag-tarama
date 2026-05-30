using System;
using System.Collections.Generic;
using System.Linq;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services;

/// <summary>Bir harita düğümünün çizim için gereken konum + tip bilgisi.</summary>
internal sealed record MapNode(
    DeviceInfo Device,
    double X,
    double Y,
    string Tur,
    string Ikon,
    bool Online,
    bool IsGateway);

/// <summary>
/// Gateway-merkezli, tipe göre kümelenmiş harita yerleşimi. UI'dan bağımsız, deterministik.
/// </summary>
internal static class NetworkMapLayout
{
    internal static readonly string[] KumeSirasi =
        { "Kamera", "Bilgisayar", "Mobil/IoT", "Ağ", "Diğer" };

    internal static string KumeyeAta(string tur) => tur switch
    {
        "Kamera" or "NVR/DVR"                                  => "Kamera",
        "Bilgisayar" or "Sunucu" or "NAS"                      => "Bilgisayar",
        "Telefon" or "Tablet" or "Akıllı TV" or "Apple TV"
            or "Akıllı Cihaz" or "Hoparlör" or "Müzik Cihazı"
            or "Linux IoT"                                     => "Mobil/IoT",
        "Router" or "Router/AP" or "Router/Switch" or "Switch"
            or "Switch/AP" or "Erişim Noktası"
            or "Güvenlik Duvarı"                               => "Ağ",
        _                                                      => "Diğer",
    };

    public static IReadOnlyList<MapNode> Hesapla(
        IReadOnlyList<DeviceInfo> cihazlar,
        Func<DeviceInfo, (string Tur, string Ikon)> turCozumleyici,
        double genislik,
        double yukseklik)
    {
        var sonuc = new List<MapNode>();
        if (cihazlar == null || cihazlar.Count == 0) return sonuc;

        double cx = genislik / 2.0, cy = yukseklik / 2.0;

        var sirali = cihazlar.OrderBy(d => d.Ip, StringComparer.Ordinal).ToList();
        var gatewayler = sirali.Where(d => d.IsGateway).ToList();

        for (int i = 0; i < gatewayler.Count; i++)
        {
            var g = gatewayler[i];
            var (tur, ikon) = turCozumleyici(g);
            double gx = cx, gy = cy;
            if (gatewayler.Count > 1)
            {
                double a = 2 * Math.PI * i / gatewayler.Count;
                gx = cx + 34 * Math.Cos(a);
                gy = cy + 34 * Math.Sin(a);
            }
            sonuc.Add(new MapNode(g, gx, gy, tur, ikon, g.Online, true));
        }

        var gruplar = sirali
            .Where(d => !d.IsGateway)
            .Select(d => { var (tur, ikon) = turCozumleyici(d); return (Dev: d, Tur: tur, Ikon: ikon, Kume: KumeyeAta(tur)); })
            .GroupBy(x => x.Kume)
            .OrderBy(g =>
            {
                int idx = Array.IndexOf(KumeSirasi, g.Key);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ToList();

        int kumeSayisi = gruplar.Count;
        if (kumeSayisi == 0) return sonuc;

        double rKume = Math.Min(genislik, yukseklik) * 0.34;
        for (int k = 0; k < kumeSayisi; k++)
        {
            double aci = 2 * Math.PI * k / kumeSayisi - Math.PI / 2.0;
            double kcx = cx + rKume * Math.Cos(aci);
            double kcy = cy + rKume * Math.Sin(aci);

            var uyeler = gruplar[k].OrderBy(x => x.Dev.Ip, StringComparer.Ordinal).ToList();
            int n = uyeler.Count;
            double rIc = 16 + 10 * n;

            for (int j = 0; j < n; j++)
            {
                double nx, ny;
                if (n == 1) { nx = kcx; ny = kcy; }
                else
                {
                    double a2 = 2 * Math.PI * j / n;
                    nx = kcx + rIc * Math.Cos(a2);
                    ny = kcy + rIc * Math.Sin(a2);
                }
                var u = uyeler[j];
                sonuc.Add(new MapNode(u.Dev, nx, ny, u.Tur, u.Ikon, u.Dev.Online, false));
            }
        }

        return sonuc;
    }
}
