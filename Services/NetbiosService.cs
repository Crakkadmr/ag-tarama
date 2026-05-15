using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record NetbiosBilgi(string? NetbiosAdi, string? GrupAdi, string? DnsAdi = null, string? PingAdi = null);

internal static class NetbiosService
{
    public static Task<NetbiosBilgi?> NodeStatusAsync(string ip, CancellationToken token, int timeoutMs = 1400)
        => NetbiosUdpNodeStatusAsync(ip, token, timeoutMs);

    public static async Task<NetbiosBilgi?> SorgulaAsync(string ip, CancellationToken token, int timeoutMs = 1800)
    {
        try
        {
            var udpTask  = NetbiosUdpNodeStatusAsync(ip, token, Math.Max(timeoutMs, 2200));
            var nbtTask  = NbtstatSorgulaAsync(ip, token, Math.Max(timeoutMs, 2600));
            var dnsTask  = DnsAdiSorgulaAsync(ip, token, Math.Max(timeoutMs, 2200));
            var pingTask = PingAdiSorgulaAsync(ip, token, Math.Min(Math.Max(timeoutMs, 1600), 2200));

            await Task.WhenAll(udpTask, nbtTask, dnsTask, pingTask).ConfigureAwait(false);

            var udp  = await udpTask.ConfigureAwait(false);
            var nbt  = await nbtTask.ConfigureAwait(false);
            var dns  = await dnsTask.ConfigureAwait(false);
            var ping = await pingTask.ConfigureAwait(false);

            var netbiosAdi = udp?.NetbiosAdi ?? nbt.NetbiosAdi;
            var grupAdi = udp?.GrupAdi ?? nbt.GrupAdi;

            return netbiosAdi is null && grupAdi is null && dns is null && ping is null
                ? null
                : new NetbiosBilgi(netbiosAdi, grupAdi, dns, ping);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<NetbiosBilgi> NbtstatSorgulaAsync(string ip, CancellationToken token, int timeoutMs)
    {
        var output = await KomutCiktisiAsync("nbtstat", $"-A {ip}", token, timeoutMs).ConfigureAwait(false);
        return output is null ? new NetbiosBilgi(null, null) : Parse(output);
    }

    private static async Task<NetbiosBilgi?> NetbiosUdpNodeStatusAsync(string ip, CancellationToken token, int timeoutMs)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);

            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = timeoutMs;
            var query = NetbiosNodeStatusQuery();
            await udp.SendAsync(query, query.Length, new IPEndPoint(IPAddress.Parse(ip), 137)).ConfigureAwait(false);
            var result = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
            return ParseNodeStatus(result.Buffer);
        }
        catch { return null; }
    }

    private static byte[] NetbiosNodeStatusQuery()
    {
        var packet = new byte[50];
        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        packet[0] = (byte)(id >> 8);
        packet[1] = (byte)(id & 0xFF);
        packet[2] = 0x00; packet[3] = 0x00;
        packet[4] = 0x00; packet[5] = 0x01;

        // Encoded wildcard NetBIOS name "*" padded to 15 chars, suffix 0x00.
        packet[12] = 0x20;
        var encoded = "CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        for (int i = 0; i < encoded.Length; i++) packet[13 + i] = (byte)encoded[i];
        packet[45] = 0x00;
        packet[46] = 0x00; packet[47] = 0x21; // NBSTAT
        packet[48] = 0x00; packet[49] = 0x01; // IN
        return packet;
    }

    private static NetbiosBilgi? ParseNodeStatus(byte[] buffer)
    {
        try
        {
            var index = 12;
            while (index < buffer.Length && buffer[index] != 0) index++;
            index++;
            index += 8; // type, class, ttl, rdlength
            if (index >= buffer.Length) return new NetbiosBilgi(null, null);

            int count = buffer[index++];
            string? cihaz = null;
            string? grup = null;

            for (int i = 0; i < count && index + 18 <= buffer.Length; i++, index += 18)
            {
                var name = System.Text.Encoding.ASCII.GetString(buffer, index, 15).Trim();
                var suffix = buffer[index + 15];
                var flags = (ushort)((buffer[index + 16] << 8) | buffer[index + 17]);
                var isGroup = (flags & 0x8000) != 0;
                if (string.IsNullOrWhiteSpace(name) || name == "__MSBROWSE__") continue;

                if (suffix == 0x20 && !isGroup)
                    cihaz ??= name;
                else if (suffix == 0x00 && isGroup)
                    grup ??= name;
                else if (suffix == 0x00 && !isGroup)
                    cihaz ??= name;
            }

            return new NetbiosBilgi(TemizAd(cihaz, ""), TemizAd(grup, ""));
        }
        catch { return null; }
    }

    private static async Task<string?> DnsAdiSorgulaAsync(string ip, CancellationToken token, int timeoutMs)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);
            var entry = await Dns.GetHostEntryAsync(ip).WaitAsync(cts.Token).ConfigureAwait(false);
            return TemizAd(entry.HostName, ip);
        }
        catch { return null; }
    }

    private static async Task<string?> PingAdiSorgulaAsync(string ip, CancellationToken token, int timeoutMs)
    {
        var output = await KomutCiktisiAsync("ping", $"-a -n 1 -w {Math.Max(250, timeoutMs / 2)} {ip}", token, timeoutMs)
            .ConfigureAwait(false);
        if (output is null) return null;

        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var ipPattern = Regex.Escape(ip);
            var match = Regex.Match(line, $@"(?<name>[^\[\]]+)\s+\[{ipPattern}\]", RegexOptions.CultureInvariant);
            if (!match.Success) continue;

            var name = match.Groups["name"].Value.Trim();
            if (name.StartsWith("Pinging ", StringComparison.OrdinalIgnoreCase))
                name = name["Pinging ".Length..].Trim();
            return TemizAd(name, ip);
        }

        return null;
    }

    private static async Task<string?> KomutCiktisiAsync(string dosya, string argumanlar, CancellationToken token, int timeoutMs)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = dosya,
                    Arguments              = argumanlar,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.GetEncoding(
                        CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
                },
                EnableRaisingEvents = true,
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
                return null;
            }

            return await outputTask.ConfigureAwait(false);
        }
        catch { return null; }
    }

    internal static NetbiosBilgi Parse(string output)
    {
        string? cihaz = null;
        string? grup  = null;

        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(rawLine, @"^\s*(?<name>.{1,15})\s+<(?<code>[0-9A-Fa-f]{2})>\s+(?<type>\S+)", RegexOptions.CultureInvariant);
            if (!match.Success) continue;

            var name = match.Groups["name"].Value.Trim();
            var code = match.Groups["code"].Value.ToUpperInvariant();
            var type = match.Groups["type"].Value.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(name) || name == "__MSBROWSE__") continue;

            bool group = type.Contains("GROUP") || type.Contains("GRUP") || type.Contains("GRUPO") || type.Contains("GROUPE");
            bool unique = type.Contains("UNIQUE") || type.Contains("BENZERS");
            if (code == "20" && !group)
                cihaz ??= name;
            else if (code == "00" && group)
                grup ??= name;
            else if (code == "00" && (unique || !group))
                cihaz ??= name;
        }

        return new NetbiosBilgi(TemizAd(cihaz, ""), TemizAd(grup, ""));
    }

    private static string? TemizAd(string? ad, string ip)
    {
        if (string.IsNullOrWhiteSpace(ad)) return null;
        var temiz = ad.Trim().TrimEnd('.');
        if (temiz.Equals(ip, StringComparison.OrdinalIgnoreCase)) return null;
        if (IPAddress.TryParse(temiz, out _)) return null;
        return temiz;
    }
}
