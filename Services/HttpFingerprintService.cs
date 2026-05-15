using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record HttpFingerprintSonuc(
    string? Marka,
    string? Tur,
    string? Model,
    string? Kaynak);

/// <summary>
/// Açık HTTP/HTTPS portuna vendor-specific endpoint'leri paralel deneyerek
/// cihazın markasını/modelini doğrular. Yalnızca port açıksa çağrılır.
/// </summary>
internal static class HttpFingerprintService
{
    private static readonly (string Yol, Func<string, HttpFingerprintSonuc?> Cozucu)[] Imzalar =
    {
        ("/ISAPI/System/deviceInfo", HikvisionParse),
        ("/cgi-bin/magicBox.cgi?action=getSystemInfo", DahuaParse),
        ("/api.cgi?cmd=GetDevInfo", ReolinkParse),
        ("/onvif/device_service", OnvifProbeParse),
        ("/api/v1/status", UnifiParse),
    };

    public static async Task<HttpFingerprintSonuc?> ProbeAsync(string ip, int port, CancellationToken token, int timeoutMs = 1500)
    {
        var gorevler = Imzalar.Select(im => DeneAsync(ip, port, im.Yol, im.Cozucu, token, timeoutMs)).ToArray();
        var sonuclar = await Task.WhenAll(gorevler).ConfigureAwait(false);
        return sonuclar.FirstOrDefault(s => s is not null);
    }

    private static async Task<HttpFingerprintSonuc?> DeneAsync(
        string ip, int port, string yol,
        Func<string, HttpFingerprintSonuc?> cozucu,
        CancellationToken token, int timeoutMs)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token).ConfigureAwait(false);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = timeoutMs;
            var req = Encoding.ASCII.GetBytes(
                $"GET {yol} HTTP/1.0\r\nHost: {ip}\r\nUser-Agent: AgTarama/1.0\r\nAccept: */*\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(req, cts.Token).ConfigureAwait(false);
            var buf = new byte[8192];
            int n = await stream.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (n <= 0) return null;
            var resp = Encoding.UTF8.GetString(buf, 0, n);
            return cozucu(resp);
        }
        catch { return null; }
    }

    private static HttpFingerprintSonuc? HikvisionParse(string resp)
    {
        if (!resp.Contains("DeviceInfo", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("hikvision", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("App-webs", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!resp.Contains("hikvision", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("<model>", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("<deviceType>", StringComparison.OrdinalIgnoreCase))
            return null;
        var model = OkuEtiket(resp, "model") ?? OkuEtiket(resp, "deviceName");
        var tip = OkuEtiket(resp, "deviceType") ?? "";
        var tur = tip.Contains("DVR", StringComparison.OrdinalIgnoreCase) ||
                  tip.Contains("NVR", StringComparison.OrdinalIgnoreCase) ? "NVR/DVR" : "Kamera";
        return new HttpFingerprintSonuc("Hikvision", tur, model, "HTTP/ISAPI");
    }

    private static HttpFingerprintSonuc? DahuaParse(string resp)
    {
        if (!resp.Contains("deviceType=", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("magicBox", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("dahua", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = Regex.Match(resp, @"deviceType=(?<v>[^\r\n]+)", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        var tur = (model?.Contains("DVR", StringComparison.OrdinalIgnoreCase) == true ||
                   model?.Contains("NVR", StringComparison.OrdinalIgnoreCase) == true) ? "NVR/DVR" : "Kamera";
        return new HttpFingerprintSonuc("Dahua", tur, model, "HTTP/magicBox");
    }

    private static HttpFingerprintSonuc? ReolinkParse(string resp)
    {
        if (!resp.Contains("reolink", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("\"model\"", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = Regex.Match(resp, @"""model""\s*:\s*""(?<v>[^""]+)""", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        return new HttpFingerprintSonuc("Reolink", "Kamera", model, "HTTP/Reolink-API");
    }

    private static HttpFingerprintSonuc? OnvifProbeParse(string resp)
    {
        // /onvif/device_service GET'e SOAP fault da dönse, "onvif" string'i cihazın ONVIF olduğunu gösterir
        if (!resp.Contains("onvif", StringComparison.OrdinalIgnoreCase)) return null;
        return new HttpFingerprintSonuc(null, "Kamera", null, "HTTP/ONVIF-probe");
    }

    private static HttpFingerprintSonuc? UnifiParse(string resp)
    {
        if (!resp.Contains("unifi", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("ubnt", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("ubiquiti", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = Regex.Match(resp, @"""model""\s*:\s*""(?<v>[^""]+)""", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        return new HttpFingerprintSonuc("Ubiquiti", "Erişim Noktası", model, "HTTP/UniFi");
    }

    private static string? OkuEtiket(string xml, string etiket)
    {
        var match = Regex.Match(xml, $@"<{Regex.Escape(etiket)}[^>]*>([^<]+)</{Regex.Escape(etiket)}>",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
