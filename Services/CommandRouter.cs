using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace AgTarama.Services;

public static class CommandRouter
{
    private static readonly Dictionary<string, Func<string[], CancellationToken, Task<string>>> _cmd =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly List<string> _hist = new(50);

    public static IReadOnlyList<string> History => _hist;
    public static IEnumerable<string> Names => _cmd.Keys.OrderBy(k => k);

    static CommandRouter() => RegisterDefaults();

    public static void Register(string name, Func<string[], CancellationToken, Task<string>> fn)
        => _cmd[name] = fn;

    public static async Task<string> ExecuteAsync(string line, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(line)) return "";
        line = line.Trim();
        PushHistory(line);

        var parts = Regex.Split(line, @"\s*&&\s*");
        if (parts.Length > 1)
        {
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (ct.IsCancellationRequested) break;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(await RunOne(part.Trim(), ct));
            }
            return sb.ToString();
        }
        return await RunOne(line, ct);
    }

    private static async Task<string> RunOne(string line, CancellationToken ct)
    {
        var tokens = Tokenize(line);
        if (tokens.Length == 0) return "";
        if (!_cmd.TryGetValue(tokens[0], out var fn))
            return $"Bilinmeyen komut: `{tokens[0]}`. `help` yazarak komutları listele.";
        try { return await fn(tokens[1..], ct); }
        catch (OperationCanceledException) { return "[iptal]"; }
        catch (Exception ex) { return $"Hata: {ex.Message}"; }
    }

    private static string[] Tokenize(string line)
        => Regex.Matches(line, @"""[^""]*""|[^\s]+")
                .Select(m => m.Value.Trim('"'))
                .ToArray();

    private static void PushHistory(string line)
    {
        _hist.RemoveAll(h => string.Equals(h, line, StringComparison.Ordinal));
        _hist.Insert(0, line);
        if (_hist.Count > 50) _hist.RemoveAt(50);
    }

    // ─── Command Definitions ───────────────────────────────────────────

    private static void RegisterDefaults()
    {
        Register("help", (_, _) =>
        {
            var names = string.Join("  ", Names.Select(n => $"`{n}`"));
            return Task.FromResult(
                "Komutlar: " + names + "\n" +
                "Örnek : ping 8.8.8.8 -c 4  |  port 192.168.1.1 80,443  |  dns google.com\n" +
                "Zincir: ping 8.8.8.8 && dns google.com");
        });

        Register("clear", (_, _) => Task.FromResult("\x00CLEAR"));

        Register("history", (_, _) =>
        {
            if (_hist.Count == 0) return Task.FromResult("(Geçmiş boş)");
            var sb = new StringBuilder();
            for (int i = 0; i < _hist.Count; i++)
                sb.AppendLine($"{i + 1,3}: {_hist[i]}");
            return Task.FromResult(sb.ToString().TrimEnd());
        });

        Register("ping", async (args, ct) =>
        {
            if (args.Length == 0) return "Kullanım: ping <hedef> [-c adet]";
            var hedef = args[0];
            var sayi = 4;
            for (int i = 1; i < args.Length - 1; i++)
                if (args[i] == "-c" && int.TryParse(args[i + 1], out var n))
                    sayi = Math.Clamp(n, 1, 20);

            var sb = new StringBuilder($"PING {hedef} ({sayi} paket)\n");
            long total = 0; int ok = 0;
            await foreach (var r in PingService.PingleAsync(hedef, sayi, 2000, 400, ct))
            {
                if (r.Durum == IPStatus.Success)
                { sb.AppendLine($"  [{r.Sira}/{r.Toplam}] ✓  {r.RtMs} ms  TTL={r.Ttl}"); total += r.RtMs; ok++; }
                else
                    sb.AppendLine($"  [{r.Sira}/{r.Toplam}] ✗  {r.Hata ?? r.Durum.ToString()}");
            }
            if (ok > 0) sb.AppendLine($"  Ort: {total / ok} ms  ({ok}/{sayi} başarılı)");
            return sb.ToString().TrimEnd();
        });

        Register("dns", async (args, ct) =>
        {
            if (args.Length == 0) return "Kullanım: dns <hedef>";
            try
            {
                var addrs = await Dns.GetHostAddressesAsync(args[0], ct);
                var sb = new StringBuilder($"DNS {args[0]}\n");
                foreach (var a in addrs) sb.AppendLine($"  → {a}");
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex) { return $"DNS hatası: {ex.Message}"; }
        });

        Register("port", async (args, ct) =>
        {
            if (args.Length < 2) return "Kullanım: port <ip> <portlar>  (örn: port 192.168.1.1 80,443,8080)";
            var hedef  = args[0];
            var portlar = PortScanService.Parse(args[1]);
            if (portlar.Length == 0) return "Port listesi geçersiz.";

            var sb    = new StringBuilder($"PORT TARA {hedef}  portlar={args[1]}\n");
            var acik  = new List<int>();
            await PortScanService.TaraAsync(hedef, portlar,
                async p => { lock (acik) acik.Add(p); await Task.CompletedTask; },
                ct, eszamanli: 30, timeoutMs: 800);

            if (acik.Count == 0) sb.AppendLine("  Açık port bulunamadı.");
            else foreach (var p in acik.OrderBy(x => x)) sb.AppendLine($"  ✓ {p}");
            return sb.ToString().TrimEnd();
        });

        Register("traceroute", async (args, ct) =>
        {
            if (args.Length == 0) return "Kullanım: traceroute <hedef>";
            var sb = new StringBuilder($"TRACEROUTE {args[0]}\n");
            using var ping = new Ping();
            for (int ttl = 1; ttl <= 30 && !ct.IsCancellationRequested; ttl++)
            {
                var opts = new PingOptions(ttl, true);
                try
                {
                    var r = await ping.SendPingAsync(args[0], 1500, new byte[32], opts);
                    var addr = r.Address?.ToString() ?? "*";
                    if (r.Status == IPStatus.TtlExpired)
                        sb.AppendLine($"  {ttl,2}. {addr,-20} {r.RoundtripTime} ms");
                    else if (r.Status == IPStatus.Success)
                    { sb.AppendLine($"  {ttl,2}. {addr,-20} {r.RoundtripTime} ms  [HEDEF]"); break; }
                    else
                        sb.AppendLine($"  {ttl,2}. *");
                }
                catch { sb.AppendLine($"  {ttl,2}. *"); }
            }
            return sb.ToString().TrimEnd();
        });

        Register("arp", async (_, ct) =>
        {
            var p = new System.Diagnostics.Process
            {
                StartInfo = new("arp", "-a")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }
            };
            p.Start();
            var out_ = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return string.IsNullOrWhiteSpace(out_) ? "ARP tablosu boş." : "ARP Tablosu\n" + out_.TrimEnd();
        });

        Register("wol", (args, _) =>
        {
            if (args.Length == 0) return Task.FromResult("Kullanım: wol <mac-adresi>");
            var mac = args[0].Replace(":", "").Replace("-", "");
            if (mac.Length != 12) return Task.FromResult("Geçersiz MAC adresi (12 hex karakter olmalı).");
            byte[] macB;
            try { macB = Convert.FromHexString(mac); }
            catch { return Task.FromResult("Geçersiz MAC adresi."); }
            var pkt = new byte[6 + 16 * 6];
            for (int i = 0; i < 6; i++) pkt[i] = 0xFF;
            for (int i = 0; i < 16; i++) Buffer.BlockCopy(macB, 0, pkt, 6 + i * 6, 6);
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Send(pkt, pkt.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            return Task.FromResult($"WoL magic packet gönderildi → {args[0]}");
        });

        Register("scan", (_, _) =>
            Task.FromResult("Cihaz tarama Cihaz Tara sekmesinden başlatılır.\nKomut konsolu üzerinden doğrudan çağrılamaz."));

        Register("ssl", async (args, ct) =>
        {
            if (args.Length == 0) return "Kullanım: ssl <host> [port=443]";
            var host = args[0];
            var port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 443;
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port, ct);
                using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                }, ct);
                if (ssl.RemoteCertificate is null) return "Sertifika alınamadı.";
                var x509 = new X509Certificate2(ssl.RemoteCertificate);
                return $"SSL {host}:{port}\n" +
                       $"  CN     : {x509.GetNameInfo(X509NameType.SimpleName, false)}\n" +
                       $"  Veren  : {x509.Issuer}\n" +
                       $"  Geçerl : {x509.NotBefore:dd.MM.yyyy} → {x509.NotAfter:dd.MM.yyyy}\n" +
                       $"  SHA256 : {x509.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256)}";
            }
            catch (Exception ex) { return $"SSL hatası: {ex.Message}"; }
        });

        Register("banner", async (args, ct) =>
        {
            if (args.Length < 2 || !int.TryParse(args[1], out var port))
                return "Kullanım: banner <ip> <port>";
            var ip = args[0];
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(ip, port, ct);
                tcp.ReceiveTimeout = 2000;
                using var ns = tcp.GetStream();
                var buf = new byte[1024];
                try
                {
                    var req = Encoding.ASCII.GetBytes("HEAD / HTTP/1.0\r\nHost: " + ip + "\r\n\r\n");
                    await ns.WriteAsync(req, ct);
                    await Task.Delay(400, ct);
                }
                catch { }
                var n = await ns.ReadAsync(buf, ct);
                return $"BANNER {ip}:{port}\n{Encoding.ASCII.GetString(buf, 0, n).TrimEnd()}";
            }
            catch (Exception ex) { return $"Banner hatası: {ex.Message}"; }
        });

        Register("web", async (args, ct) =>
        {
            if (args.Length == 0) return "Kullanım: web <url>";
            var url = args[0];
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "http://" + url;
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                using var req  = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);
                using var resp = await http.SendAsync(req, ct);
                var sb = new StringBuilder($"WEB {url}\n  HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}\n");
                foreach (var h in resp.Headers.Take(10))
                    sb.AppendLine($"  {h.Key}: {string.Join(", ", h.Value)}");
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex) { return $"HTTP hatası: {ex.Message}"; }
        });

        Register("smb", async (args, ct) =>
        {
            if (args.Length == 0) return "Kullanım: smb <ip>";
            var ip = args[0];
            bool p445 = false, p139 = false;
            async Task Try(int port, Action<bool> set)
            {
                using var c = new TcpClient();
                try
                {
                    var conn = c.ConnectAsync(ip, port, ct).AsTask();
                    await Task.WhenAny(conn, Task.Delay(1200, ct));
                    set(c.Connected);
                }
                catch { set(false); }
            }
            await Task.WhenAll(
                Try(445, v => p445 = v),
                Try(139, v => p139 = v));
            return $"SMB {ip}\n  445 (SMB)    : {(p445 ? "✓ Açık" : "✗ Kapalı")}\n  139 (NetBIOS): {(p139 ? "✓ Açık" : "✗ Kapalı")}";
        });

        Register("snmp", async (args, ct) =>
        {
            if (args.Length < 3)
                return "Kullanım: snmp <ip> <community> <oid|alias>\nAlias: sysName sysDescr sysUpTime sysLocation sysContact";
            var ip        = args[0];
            var community = args[1];
            var oidStr    = args[2].ToLowerInvariant() switch
            {
                "sysname"     => "1.3.6.1.2.1.1.5.0",
                "sysdescr"    => "1.3.6.1.2.1.1.1.0",
                "sysuptime"   => "1.3.6.1.2.1.1.3.0",
                "syslocation" => "1.3.6.1.2.1.1.6.0",
                "syscontact"  => "1.3.6.1.2.1.1.4.0",
                _             => args[2],
            };
            try
            {
                var pkt = BuildSnmpGet(community, oidStr);
                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = 3000;
                await udp.SendAsync(pkt, pkt.Length, ip, 161);
                var recv = await udp.ReceiveAsync(ct);
                var val  = ParseSnmpValue(recv.Buffer);
                return $"SNMP {ip}\n  OID  : {oidStr}\n  Değer: {val}";
            }
            catch (Exception ex) { return $"SNMP hatası: {ex.Message}"; }
        });
    }

    // ─── SNMP helpers ──────────────────────────────────────────────────

    private static byte[] BuildSnmpGet(string community, string oid)
    {
        var commB   = Encoding.ASCII.GetBytes(community);
        var oidB    = EncodeOid(oid);
        var varBind = Tlv(0x30, Concat(Tlv(0x06, oidB), new byte[] { 0x05, 0x00 }));
        var pduBody = Concat(
            Tlv(0x02, new byte[] { 0x01 }),  // requestId
            Tlv(0x02, new byte[] { 0x00 }),  // errorStatus
            Tlv(0x02, new byte[] { 0x00 }),  // errorIndex
            Tlv(0x30, varBind));             // varBindList
        var msg = Concat(
            Tlv(0x02, new byte[] { 0x00 }),  // version = 0 (v1)
            Tlv(0x04, commB),                // community
            Tlv(0xA0, pduBody));             // GetRequest PDU
        return Tlv(0x30, msg);
    }

    private static byte[] EncodeOid(string oid)
    {
        var parts = oid.Split('.');
        if (parts.Length < 2) return Array.Empty<byte>();
        var bytes = new List<byte>
        {
            (byte)(int.Parse(parts[0]) * 40 + int.Parse(parts[1]))
        };
        for (int i = 2; i < parts.Length; i++)
        {
            var val = int.Parse(parts[i]);
            if (val < 128) { bytes.Add((byte)val); continue; }
            var tmp = new List<byte>();
            for (; val > 0; val >>= 7)
                tmp.Insert(0, (byte)((val & 0x7F) | (tmp.Count > 0 ? 0x80 : 0)));
            bytes.AddRange(tmp);
        }
        return bytes.ToArray();
    }

    private static byte[] Tlv(byte tag, byte[] value)
    {
        var lenB   = LenBytes(value.Length);
        var result = new byte[1 + lenB.Length + value.Length];
        result[0]  = tag;
        Buffer.BlockCopy(lenB, 0, result, 1, lenB.Length);
        Buffer.BlockCopy(value, 0, result, 1 + lenB.Length, value.Length);
        return result;
    }

    private static byte[] LenBytes(int len)
    {
        if (len < 128) return new[] { (byte)len };
        if (len < 256) return new byte[] { 0x81, (byte)len };
        return new byte[] { 0x82, (byte)(len >> 8), (byte)(len & 0xFF) };
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(a => a.Length)];
        int pos = 0;
        foreach (var a in arrays) { Buffer.BlockCopy(a, 0, result, pos, a.Length); pos += a.Length; }
        return result;
    }

    private static string ParseSnmpValue(byte[] buf)
    {
        try
        {
            int pos = 0;
            Skip(buf, ref pos, 0x30);   // outer SEQUENCE
            SkipTlv(buf, ref pos);      // version
            SkipTlv(buf, ref pos);      // community
            Skip(buf, ref pos, 0xA2);   // GetResponse PDU
            SkipTlv(buf, ref pos);      // requestId
            SkipTlv(buf, ref pos);      // errorStatus
            SkipTlv(buf, ref pos);      // errorIndex
            Skip(buf, ref pos, 0x30);   // varBindList
            Skip(buf, ref pos, 0x30);   // varBind
            SkipTlv(buf, ref pos);      // OID (skip)

            var tag = buf[pos++];
            var len = ReadLen(buf, ref pos);
            return tag switch
            {
                0x04 => Encoding.ASCII.GetString(buf, pos, len).Trim('\0'),
                0x02 => IntVal(buf, pos, len).ToString(),
                0x43 => $"{(uint)IntVal(buf, pos, len) / 100.0:F1}s (uptime)",
                0x41 => string.Join(".", buf[pos..(pos + len)]),
                0x05 => "(null)",
                _    => $"(tag=0x{tag:X2} len={len})",
            };
        }
        catch { return "(parse hatası)"; }
    }

    private static void Skip(byte[] buf, ref int pos, byte expectedTag)
    {
        if (buf[pos] != expectedTag)
            throw new InvalidOperationException($"Beklenen 0x{expectedTag:X2} ama 0x{buf[pos]:X2}");
        pos++;
        ReadLen(buf, ref pos);
    }

    private static void SkipTlv(byte[] buf, ref int pos)
    {
        pos++;
        var len = ReadLen(buf, ref pos);
        pos += len;
    }

    private static int ReadLen(byte[] buf, ref int pos)
    {
        if (buf[pos] < 128) return buf[pos++];
        var extra = buf[pos++] & 0x7F;
        int len = 0;
        for (int i = 0; i < extra; i++) len = (len << 8) | buf[pos++];
        return len;
    }

    private static long IntVal(byte[] buf, int pos, int len)
    {
        long v = (buf[pos] & 0x80) != 0 ? -1L : 0L;
        for (int i = 0; i < len; i++) v = (v << 8) | buf[pos + i];
        return v;
    }
}
