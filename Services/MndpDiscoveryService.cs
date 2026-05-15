using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed record MndpKaydi(
    string Ip,
    string? Mac,
    string? Identity,
    string? Version,
    string? Platform,
    string? Board,
    string? SoftwareId);

/// <summary>
/// MikroTik Neighbor Discovery Protocol (UDP 5678). RouterBOARD cihazlarını keşfeder.
/// Aktif probe + pasif dinleme — MikroTik'ler periyodik broadcast atar.
/// </summary>
internal static class MndpDiscoveryService
{
    private const int MndpPort = 5678;
    private static readonly byte[] Probe = { 0x00, 0x00, 0x00, 0x00 };

    public static async Task<IReadOnlyList<MndpKaydi>> TaraAsync(string subnet, CancellationToken token, int dinlemeMs = 3000)
    {
        var sonuc = new Dictionary<string, MndpKaydi>(StringComparer.Ordinal);
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, MndpPort));

            var hedefBroadcast = new IPEndPoint(IPAddress.Broadcast, MndpPort);
            var hedefSubnet    = new IPEndPoint(IPAddress.Parse($"{subnet}.255"), MndpPort);
            try { await udp.SendAsync(Probe, Probe.Length, hedefBroadcast).ConfigureAwait(false); } catch { }
            try { await udp.SendAsync(Probe, Probe.Length, hedefSubnet).ConfigureAwait(false); } catch { }

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
                catch { }
            }
        }
        catch { }
        finally { try { udp?.Dispose(); } catch { } }
        return sonuc.Values.ToArray();
    }

    private static MndpKaydi? ParseTlv(string ip, byte[] buf)
    {
        // MNDP header: 2 byte type + 2 byte seqno. Sonrası TLV: 2 byte type + 2 byte length + payload.
        if (buf.Length < 4) return null;
        int index = 4;
        string? mac = null, identity = null, version = null, platform = null, board = null, softId = null;

        while (index + 4 <= buf.Length)
        {
            int tip      = (buf[index] << 8) | buf[index + 1];
            int uzunluk  = (buf[index + 2] << 8) | buf[index + 3];
            index += 4;
            if (uzunluk < 0 || index + uzunluk > buf.Length) break;
            var payload = new ReadOnlySpan<byte>(buf, index, uzunluk);

            switch (tip)
            {
                case 0x01: if (uzunluk == 6) mac = FormatMac(payload); break;
                case 0x05: identity = Encoding.UTF8.GetString(payload).Trim(); break;
                case 0x07: version  = Encoding.UTF8.GetString(payload).Trim(); break;
                case 0x08: platform = Encoding.UTF8.GetString(payload).Trim(); break;
                case 0x0B: softId   = Encoding.UTF8.GetString(payload).Trim(); break;
                case 0x0C: board    = Encoding.UTF8.GetString(payload).Trim(); break;
            }
            index += uzunluk;
        }

        if (mac is null && identity is null && version is null && board is null) return null;
        return new MndpKaydi(ip, mac, NullaCevir(identity), NullaCevir(version),
            NullaCevir(platform), NullaCevir(board), NullaCevir(softId));
    }

    private static MndpKaydi Birlestir(MndpKaydi? eski, MndpKaydi yeni)
    {
        if (eski is null) return yeni;
        return new MndpKaydi(
            yeni.Ip,
            yeni.Mac ?? eski.Mac,
            yeni.Identity ?? eski.Identity,
            yeni.Version ?? eski.Version,
            yeni.Platform ?? eski.Platform,
            yeni.Board ?? eski.Board,
            yeni.SoftwareId ?? eski.SoftwareId);
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
