using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

/// <summary>
/// SNMP v1/v2c sysDescr probe (UDP 161, community "public"). Port 161 açık
/// cihazlarda detaylı vendor/model bilgisi sağlar. ASN.1 DER manuel kodlama
/// (NuGet bağımlılığı yok).
/// </summary>
internal static class SnmpFingerprintService
{
    private const string SysDescrOid = "1.3.6.1.2.1.1.1.0";
    private const string SysNameOid  = "1.3.6.1.2.1.1.5.0";

    public static async Task<string?> SysDescrAsync(string ip, CancellationToken token, int timeoutMs = 1500)
        => await GetAsync(ip, SysDescrOid, "public", token, timeoutMs).ConfigureAwait(false);

    public static async Task<string?> SysNameAsync(string ip, CancellationToken token, int timeoutMs = 1500)
        => await GetAsync(ip, SysNameOid, "public", token, timeoutMs).ConfigureAwait(false);

    private static async Task<string?> GetAsync(string ip, string oid, string community, CancellationToken token, int timeoutMs)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.ReceiveTimeout = timeoutMs;
            var pkt = BuildSnmpGet(community, oid);
            await udp.SendAsync(pkt, pkt.Length, ip, 161).ConfigureAwait(false);
            var recv = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
            return ParseSnmpString(recv.Buffer);
        }
        catch { return null; }
    }

    // ── ASN.1 DER kodlama ─────────────────────────────────────────────────

    private static byte[] BuildSnmpGet(string community, string oid)
    {
        var commB   = Encoding.ASCII.GetBytes(community);
        var oidB    = EncodeOid(oid);
        var varBind = Tlv(0x30, Concat(Tlv(0x06, oidB), new byte[] { 0x05, 0x00 }));
        var pduBody = Concat(
            Tlv(0x02, new byte[] { 0x01 }),   // requestId
            Tlv(0x02, new byte[] { 0x00 }),   // errorStatus
            Tlv(0x02, new byte[] { 0x00 }),   // errorIndex
            Tlv(0x30, varBind));              // varBindList
        var msg = Concat(
            Tlv(0x02, new byte[] { 0x01 }),   // version = 1 (v2c)
            Tlv(0x04, commB),                 // community
            Tlv(0xA0, pduBody));              // GetRequest PDU
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

    // ── Yanıt parser ──────────────────────────────────────────────────────

    private static string? ParseSnmpString(byte[] buf)
    {
        try
        {
            int pos = 0;
            Skip(buf, ref pos, 0x30);
            SkipTlv(buf, ref pos);     // version
            SkipTlv(buf, ref pos);     // community
            Skip(buf, ref pos, 0xA2);  // GetResponse PDU
            SkipTlv(buf, ref pos);     // requestId
            SkipTlv(buf, ref pos);     // errorStatus
            SkipTlv(buf, ref pos);     // errorIndex
            Skip(buf, ref pos, 0x30);  // varBindList
            Skip(buf, ref pos, 0x30);  // varBind
            SkipTlv(buf, ref pos);     // OID

            var tag = buf[pos++];
            var len = ReadLen(buf, ref pos);
            if (tag == 0x04)
                return Encoding.UTF8.GetString(buf, pos, len).Trim('\0', ' ', '\r', '\n');
            return null;
        }
        catch { return null; }
    }

    private static void Skip(byte[] buf, ref int pos, byte expectedTag)
    {
        if (buf[pos] != expectedTag)
            throw new InvalidOperationException();
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
        var first = buf[pos++];
        if ((first & 0x80) == 0) return first;
        var count = first & 0x7F;
        int len = 0;
        for (int i = 0; i < count; i++) len = (len << 8) | buf[pos++];
        return len;
    }
}
