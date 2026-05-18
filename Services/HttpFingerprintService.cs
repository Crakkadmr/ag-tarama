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
    string? Kaynak,
    int Skor); // 55 = vendor API + model alındı, 35 = sadece probe match, 25 = banner-only

/// <summary>
/// Açık HTTP/HTTPS portuna vendor-specific endpoint'leri paralel deneyerek
/// cihazın markasını/modelini doğrular. v0.4.x: endpoint listesi genişletildi,
/// ham `Server:` banner regex tablosu eklendi, isabet skoru döner.
/// </summary>
internal static class HttpFingerprintService
{
    private static readonly (string Yol, Func<string, HttpFingerprintSonuc?> Cozucu)[] Imzalar =
    {
        ("/ISAPI/System/deviceInfo",                       HikvisionParse),
        ("/cgi-bin/magicBox.cgi?action=getSystemInfo",     DahuaParse),
        ("/api.cgi?cmd=GetDevInfo",                        ReolinkParse),
        ("/onvif/device_service",                          OnvifProbeParse),
        ("/api/v1/status",                                 UnifiParse),
        ("/axis-cgi/basicdeviceinfo.cgi",                  AxisParse),
        ("/cgi-bin/hi3510/param.cgi?cmd=getserverinfo",    Hi3510Parse),
        ("/webapi/entry.cgi?api=SYNO.API.Info&version=1&method=query", SynologyParse),
        ("/cgi-bin/sysinfo.cgi",                           QnapParse),
        ("/xml/device_description.xml",                    SonosUpnpParse),
        ("/cm?cmnd=Status",                                TasmotaParse),
        ("/shelly",                                        ShellyParse),
        ("/api/states",                                    HomeAssistantParse),
        ("/",                                              BannerOnlyParse), // Server: header fallback
    };

    public static async Task<HttpFingerprintSonuc?> ProbeAsync(string ip, int port, CancellationToken token, int timeoutMs = 1500)
    {
        var gorevler = Imzalar.Select(im => DeneAsync(ip, port, im.Yol, im.Cozucu, token, timeoutMs)).ToArray();
        var sonuclar = await Task.WhenAll(gorevler).ConfigureAwait(false);
        // En yüksek skor kazanır
        return sonuclar.Where(s => s is not null).OrderByDescending(s => s!.Skor).FirstOrDefault();
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
            var buf = new byte[16384];
            int n = await stream.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (n <= 0) return null;
            var resp = Encoding.UTF8.GetString(buf, 0, n);
            return cozucu(resp);
        }
        catch { return null; }
    }

    // ── Vendor parser'ları ──────────────────────────────────────────────

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
        int skor = model is not null ? 55 : 35;
        return new HttpFingerprintSonuc("Hikvision", tur, model, "HTTP/ISAPI", skor);
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
        int skor = model is not null ? 55 : 35;
        return new HttpFingerprintSonuc("Dahua", tur, model, "HTTP/magicBox", skor);
    }

    private static HttpFingerprintSonuc? ReolinkParse(string resp)
    {
        if (!resp.Contains("reolink", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("\"model\"", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = Regex.Match(resp, @"""model""\s*:\s*""(?<v>[^""]+)""", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        int skor = model is not null ? 55 : 35;
        return new HttpFingerprintSonuc("Reolink", "Kamera", model, "HTTP/Reolink-API", skor);
    }

    private static HttpFingerprintSonuc? OnvifProbeParse(string resp)
    {
        if (!resp.Contains("onvif", StringComparison.OrdinalIgnoreCase)) return null;
        return new HttpFingerprintSonuc(null, "Kamera", null, "HTTP/ONVIF-probe", 35);
    }

    private static HttpFingerprintSonuc? UnifiParse(string resp)
    {
        if (!resp.Contains("unifi", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("ubnt", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("ubiquiti", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = Regex.Match(resp, @"""model""\s*:\s*""(?<v>[^""]+)""", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        int skor = model is not null ? 55 : 35;
        return new HttpFingerprintSonuc("Ubiquiti", "Erişim Noktası", model, "HTTP/UniFi", skor);
    }

    private static HttpFingerprintSonuc? AxisParse(string resp)
    {
        if (!resp.Contains("axis", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("Server: Boa", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("AXIS", StringComparison.Ordinal))
            return null;
        var match = Regex.Match(resp, @"""ProdNbr""\s*:\s*""(?<v>[^""]+)""", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        int skor = model is not null ? 55 : 35;
        return new HttpFingerprintSonuc("Axis", "Kamera", model, "HTTP/Axis-CGI", skor);
    }

    private static HttpFingerprintSonuc? Hi3510Parse(string resp)
    {
        // Hisilicon Hi3510 tabanlı genel IP kamera firmware'leri
        if (!resp.Contains("var serverinfo", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("hi3510", StringComparison.OrdinalIgnoreCase))
            return null;
        return new HttpFingerprintSonuc("Hi3510", "Kamera", null, "HTTP/Hi3510", 30);
    }

    private static HttpFingerprintSonuc? SynologyParse(string resp)
    {
        if (!resp.Contains("SYNO.API", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("synology", StringComparison.OrdinalIgnoreCase))
            return null;
        return new HttpFingerprintSonuc("Synology", "NAS", null, "HTTP/DSM", 50);
    }

    private static HttpFingerprintSonuc? QnapParse(string resp)
    {
        if (!resp.Contains("qnap", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("QTS", StringComparison.Ordinal))
            return null;
        return new HttpFingerprintSonuc("QNAP", "NAS", null, "HTTP/QTS", 50);
    }

    private static HttpFingerprintSonuc? SonosUpnpParse(string resp)
    {
        if (!resp.Contains("Sonos", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("urn:schemas-upnp-org:device", StringComparison.OrdinalIgnoreCase))
            return null;
        if (resp.Contains("Sonos", StringComparison.OrdinalIgnoreCase))
        {
            var model = OkuEtiket(resp, "modelName") ?? OkuEtiket(resp, "modelNumber");
            return new HttpFingerprintSonuc("Sonos", "Hoparlör", model, "HTTP/UPnP", model is not null ? 55 : 40);
        }
        // Generic UPnP: marka/model XML'den çek; tür belirsiz, evidence düşük tutulur.
        var mfr   = OkuEtiket(resp, "manufacturer");
        var mdl   = OkuEtiket(resp, "modelName");
        if (string.IsNullOrWhiteSpace(mfr) && string.IsNullOrWhiteSpace(mdl)) return null;
        return new HttpFingerprintSonuc(mfr, null, mdl, "HTTP/UPnP-XML", 25);
    }

    private static HttpFingerprintSonuc? TasmotaParse(string resp)
    {
        if (!resp.Contains("Tasmota", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("StatusSTS", StringComparison.OrdinalIgnoreCase))
            return null;
        return new HttpFingerprintSonuc("Tasmota", "Akıllı Cihaz", null, "HTTP/Tasmota", 50);
    }

    private static HttpFingerprintSonuc? ShellyParse(string resp)
    {
        if (!resp.Contains("shelly", StringComparison.OrdinalIgnoreCase)) return null;
        var match = Regex.Match(resp, @"""type""\s*:\s*""(?<v>[^""]+)""", RegexOptions.IgnoreCase);
        var model = match.Success ? match.Groups["v"].Value.Trim() : null;
        int skor = model is not null ? 55 : 35;
        return new HttpFingerprintSonuc("Shelly", "Akıllı Cihaz", model, "HTTP/Shelly", skor);
    }

    private static HttpFingerprintSonuc? HomeAssistantParse(string resp)
    {
        if (!resp.Contains("home assistant", StringComparison.OrdinalIgnoreCase) &&
            !resp.Contains("homeassistant", StringComparison.OrdinalIgnoreCase))
            return null;
        return new HttpFingerprintSonuc("Home Assistant", "Akıllı Cihaz", null, "HTTP/HA", 40);
    }

    /// <summary>
    /// `Server:` header'ından vendor çıkarımı. Genel HTTP sunucuları (nginx/apache/IIS)
    /// kısa yoldan elenir; yalnızca cihaza özgü banner'lar (Boa, GoAhead, mini_httpd,
    /// RomPager, App-webs, dnvrs-webs, uc-httpd) işaret olarak değerlendirilir.
    /// </summary>
    private static HttpFingerprintSonuc? BannerOnlyParse(string resp)
    {
        var sv = Regex.Match(resp, @"^Server:\s*(?<v>[^\r\n]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!sv.Success) return null;
        var server = sv.Groups["v"].Value.Trim();
        var s = server.ToLowerInvariant();

        // Cihaz/firmware banner'ları
        if (s.Contains("hikvision-webs") || s.Contains("app-webs") || s.Contains("dnvrs-webs"))
            return new HttpFingerprintSonuc("Hikvision", "Kamera", null, $"Server: {server}", 25);
        if (s.Contains("uc-httpd"))
            return new HttpFingerprintSonuc("XMeye/Generic", "NVR/DVR", null, $"Server: {server}", 25);
        if (s.Contains("rompager") || s.Contains("allegro"))
            return new HttpFingerprintSonuc(null, "Router/AP", null, $"Server: {server}", 20);
        if (s.Contains("mini_httpd") || s.Contains("goahead-webs") || s.Contains("goahead"))
            return new HttpFingerprintSonuc("IP Kamera", "Kamera", null, $"Server: {server}", 20);
        if (s.Contains("boa/"))
            return new HttpFingerprintSonuc(null, "Kamera", null, $"Server: {server}", 15);
        if (s.Contains("lighttpd (cygwin)"))
            return new HttpFingerprintSonuc(null, "Yazıcı", null, $"Server: {server}", 20);
        if (s.Contains("micro_httpd") || s.Contains("thttpd"))
            return new HttpFingerprintSonuc(null, "Akıllı Cihaz", null, $"Server: {server}", 12);
        if (s.Contains("microsoft-iis"))
            return new HttpFingerprintSonuc("Windows/IIS", "Bilgisayar", null, $"Server: {server}", 20);
        if (s.Contains("hp http server"))
            return new HttpFingerprintSonuc("HP", "Yazıcı", null, $"Server: {server}", 30);
        if (s.Contains("brother"))
            return new HttpFingerprintSonuc("Brother", "Yazıcı", null, $"Server: {server}", 30);
        if (s.Contains("jetdirect") || s.Contains("jet direct"))
            return new HttpFingerprintSonuc("HP JetDirect", "Yazıcı", null, $"Server: {server}", 35);
        // Generic — değerlendirme dışı
        return null;
    }

    private static string? OkuEtiket(string xml, string etiket)
    {
        var match = Regex.Match(xml, $@"<{Regex.Escape(etiket)}[^>]*>([^<]+)</{Regex.Escape(etiket)}>",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
