using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record UbiquitiKaydi(
    string Ip,
    string? Mac,
    string? Hostname,
    string? Platform,
    string? Firmware,
    string? ModelKodu);

/// <summary>
/// Ubiquiti Discovery v1 + v2 (UDP 10001). UniFi AP, EdgeRouter, AirOS cihazlarını keşfeder.
/// Probe paketi atılır, gelen TLV yanıtları parse edilir.
/// </summary>
internal static class UbiquitiDiscoveryService
{
    private const int UbntPort = 10001;
    private static readonly byte[] ProbeV1 = { 0x01, 0x00, 0x00, 0x00 };
    private static readonly byte[] ProbeV2 = { 0x02, 0x08, 0x00, 0x00 };

    public static async Task<IReadOnlyList<UbiquitiKaydi>> TaraAsync(string subnet, CancellationToken token, int dinlemeMs = 2500)
    {
        var sonuc = new Dictionary<string, UbiquitiKaydi>(StringComparer.Ordinal);
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            var hedefSubnet = new IPEndPoint(IPAddress.Parse($"{subnet}.255"), UbntPort);
            var hedefBroadcast = new IPEndPoint(IPAddress.Broadcast, UbntPort);

            await udp.SendAsync(ProbeV1, ProbeV1.Length, hedefSubnet).ConfigureAwait(false);
            await udp.SendAsync(ProbeV1, ProbeV1.Length, hedefBroadcast).ConfigureAwait(false);
            await udp.SendAsync(ProbeV2, ProbeV2.Length, hedefSubnet).ConfigureAwait(false);
            await udp.SendAsync(ProbeV2, ProbeV2.Length, hedefBroadcast).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(dinlemeMs);

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var alindi = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
                    var ip = alindi.RemoteEndPoint.Address.ToString();
                    if (!ip.StartsWith(subnet + ".", StringComparison.Ordinal)) continue;
                    if (alindi.Buffer.Length < 4) continue;
                    var kayit = ParseTlv(ip, alindi.Buffer);
                    if (kayit is null) continue;
                    sonuc[ip] = Birlestir(sonuc.TryGetValue(ip, out var mevcut) ? mevcut : null, kayit);
                }
                catch (OperationCanceledException) { break; }
                catch { /* tek paket hatasını yok say */ }
            }
        }
        catch { }
        return sonuc.Values.ToArray();
    }

    private static UbiquitiKaydi? ParseTlv(string ip, byte[] buf)
    {
        // İlk 4 byte: header. Sonrası TLV listesi.
        if (buf.Length < 4) return null;
        int index = 4;
        string? mac = null;
        string? hostname = null;
        string? platform = null;
        string? firmware = null;
        string? modelKodu = null;

        while (index + 3 <= buf.Length)
        {
            byte tip = buf[index];
            int uzunluk = (buf[index + 1] << 8) | buf[index + 2];
            index += 3;
            if (uzunluk <= 0 || index + uzunluk > buf.Length) break;
            var payload = new ReadOnlySpan<byte>(buf, index, uzunluk);

            switch (tip)
            {
                case 0x01: // MAC
                    if (uzunluk == 6) mac = FormatMac(payload);
                    break;
                case 0x02: // MAC + IP
                    if (uzunluk == 10) mac = FormatMac(payload[..6]);
                    break;
                case 0x03: // Firmware
                    firmware = Encoding.ASCII.GetString(payload).Trim();
                    break;
                case 0x0B: // Hostname
                    hostname = Encoding.UTF8.GetString(payload).Trim();
                    break;
                case 0x0C: // Platform / model
                    platform = Encoding.UTF8.GetString(payload).Trim();
                    break;
                case 0x14: // Model kodu
                    modelKodu = Encoding.ASCII.GetString(payload).Trim();
                    break;
            }
            index += uzunluk;
        }

        if (mac is null && hostname is null && platform is null && firmware is null && modelKodu is null)
            return null;
        return new UbiquitiKaydi(ip, mac, NullaCevir(hostname), NullaCevir(platform),
            NullaCevir(firmware), NullaCevir(modelKodu));
    }

    private static UbiquitiKaydi Birlestir(UbiquitiKaydi? eski, UbiquitiKaydi yeni)
    {
        if (eski is null) return yeni;
        return new UbiquitiKaydi(
            yeni.Ip,
            yeni.Mac ?? eski.Mac,
            yeni.Hostname ?? eski.Hostname,
            yeni.Platform ?? eski.Platform,
            yeni.Firmware ?? eski.Firmware,
            yeni.ModelKodu ?? eski.ModelKodu);
    }

    private static string FormatMac(ReadOnlySpan<byte> bytes)
    {
        Span<char> ch = stackalloc char[17];
        const string hex = "0123456789ABCDEF";
        for (int i = 0; i < 6; i++)
        {
            ch[i * 3]     = hex[bytes[i] >> 4];
            ch[i * 3 + 1] = hex[bytes[i] & 0x0F];
            if (i < 5) ch[i * 3 + 2] = ':';
        }
        return new string(ch);
    }

    private static string? NullaCevir(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
