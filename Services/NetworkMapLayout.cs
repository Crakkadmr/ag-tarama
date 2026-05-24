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
    public static IReadOnlyList<MapNode> Hesapla(
        IReadOnlyList<DeviceInfo> cihazlar,
        Func<DeviceInfo, (string Tur, string Ikon)> turCozumleyici,
        double genislik,
        double yukseklik)
    {
        var sonuc = new List<MapNode>();
        if (cihazlar == null || cihazlar.Count == 0) return sonuc;
        return sonuc;
    }
}
