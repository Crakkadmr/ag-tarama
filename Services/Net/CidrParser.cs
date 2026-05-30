using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AgTarama.Services.Net;

internal sealed class TaramaSubneti
{
    public string Prefix { get; init; } = "";
    public int HostStart { get; init; } = 1;
    public int HostEnd { get; init; } = 254;
    public string OriginalCidr { get; init; } = "";
    public string Cidr => string.IsNullOrEmpty(OriginalCidr)
        ? $"{Prefix}.0/24"
        : OriginalCidr;
    public int HostCount => HostEnd >= HostStart ? HostEnd - HostStart + 1 : 0;
}

internal static class CidrParser
{
    public static List<TaramaSubneti> Parse(string giris)
    {
        var list = new List<TaramaSubneti>();
        var tekiller = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(giris)) return list;

        var parcalar = giris.Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var parca in parcalar)
        {
            var token = parca.Trim();

            var cidr = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})/(?<m>\d{1,2})$");
            if (cidr.Success)
            {
                if (!int.TryParse(cidr.Groups["a"].Value, out var a) || a is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["b"].Value, out var b) || b is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["c"].Value, out var c) || c is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["d"].Value, out var d) || d is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["m"].Value, out var mask) || mask is < 16 or > 32) continue;
                CidrAraligaCoz(a, b, c, d, mask, token, list, tekiller);
                continue;
            }

            var p3 = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})$");
            var p4 = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})$");
            string? prefix = null;
            if (p3.Success)
                prefix = $"{p3.Groups["a"].Value}.{p3.Groups["b"].Value}.{p3.Groups["c"].Value}";
            else if (p4.Success)
                prefix = $"{p4.Groups["a"].Value}.{p4.Groups["b"].Value}.{p4.Groups["c"].Value}";

            if (prefix is null) continue;

            var oktetler = prefix.Split('.');
            if (oktetler.Length != 3) continue;
            if (!oktetler.All(x => int.TryParse(x, out var n) && n is >= 0 and <= 255)) continue;

            var key = $"{prefix}|1-254";
            if (tekiller.Add(key))
                list.Add(new TaramaSubneti { Prefix = prefix, HostStart = 1, HostEnd = 254 });
        }

        return list;
    }

    private static void CidrAraligaCoz(int a, int b, int c, int d, int mask, string orijinal,
                                       List<TaramaSubneti> list, HashSet<string> tekiller)
    {
        uint ipUint = ((uint)a << 24) | ((uint)b << 16) | ((uint)c << 8) | (uint)d;
        uint maskUint = mask == 0 ? 0u : 0xFFFFFFFFu << (32 - mask);
        uint network = ipUint & maskUint;
        uint broadcast = network | ~maskUint;

        if (mask >= 24)
        {
            int ho1 = (int)((network >> 24) & 0xFF);
            int ho2 = (int)((network >> 16) & 0xFF);
            int ho3 = (int)((network >> 8) & 0xFF);
            var prefix = $"{ho1}.{ho2}.{ho3}";

            int hostStart, hostEnd;
            if (mask == 24)
            {
                hostStart = 1;
                hostEnd = 254;
            }
            else if (mask == 31)
            {
                hostStart = (int)(network & 0xFF);
                hostEnd = (int)(broadcast & 0xFF);
            }
            else if (mask == 32)
            {
                hostStart = (int)(network & 0xFF);
                hostEnd = hostStart;
            }
            else
            {
                hostStart = (int)((network & 0xFF) + 1);
                hostEnd = (int)((broadcast & 0xFF) - 1);
            }

            if (hostEnd < hostStart) return;
            var key = $"{prefix}|{hostStart}-{hostEnd}";
            if (tekiller.Add(key))
                list.Add(new TaramaSubneti
                {
                    Prefix = prefix,
                    HostStart = hostStart,
                    HostEnd = hostEnd,
                    OriginalCidr = orijinal,
                });
        }
        else
        {
            ulong toplam = (ulong)broadcast - network + 1ul;
            ulong adetCidr = toplam / 256ul;
            if (adetCidr > 256) return;

            for (uint cur = network; cur <= broadcast && cur >= network; cur += 256)
            {
                int o1 = (int)((cur >> 24) & 0xFF);
                int o2 = (int)((cur >> 16) & 0xFF);
                int o3 = (int)((cur >> 8) & 0xFF);
                var p = $"{o1}.{o2}.{o3}";
                var key = $"{p}|1-254";
                if (tekiller.Add(key))
                    list.Add(new TaramaSubneti
                    {
                        Prefix = p,
                        HostStart = 1,
                        HostEnd = 254,
                        OriginalCidr = orijinal,
                    });
                if (cur > 0xFFFFFF00u) break;
            }
        }
    }
}
