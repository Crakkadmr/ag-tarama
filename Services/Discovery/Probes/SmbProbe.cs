using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

// SMB Negotiate — port 445, computer name + OS extraction
internal sealed class SmbProbe : IProbe
{
    public string Name => "SMB";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);
        int count = Math.Max(0, hostEnd - hostStart + 1);

        var tasks = System.Linq.Enumerable.Range(hostStart, count)
            .Select(i => Task.Run(async () =>
            {
                var ip = $"{subnetPrefix}.{i}";
                if (!store.TryGet(ip, out var bilgi) || bilgi == null) return;

                // Only probe if port 445 is known open
                bool has445;
                lock (bilgi.AcikPortlar) has445 = bilgi.AcikPortlar.Contains(445);
                if (!has445) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var (computer, os) = await NegotiateAsync(ip, token).ConfigureAwait(false);
                    if (computer == null && os == null) return;
                    bilgi.SmbComputerName = computer;
                    bilgi.SmbOs = os;
                    if (!string.IsNullOrWhiteSpace(os)) bilgi.Os = os;
                    bilgi.Online = true;
                    bilgi.KesifKaynaklari.Add("SMB");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<(string? Computer, string? Os)> NegotiateAsync(
        string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 445, cts.Token).ConfigureAwait(false);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2000;

            // SMBv1 Negotiate (minimal)
            byte[] negotiate = BuildSmbNegotiate();
            await stream.WriteAsync(negotiate, cts.Token).ConfigureAwait(false);

            var buf = new byte[4096];
            int n = await stream.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            return ParseNegotiateResponse(buf, n);
        }
        catch { return (null, null); }
    }

    // Minimal SMBv1 Negotiate Request
    private static byte[] BuildSmbNegotiate()
    {
        // NetBIOS session + SMBv1 negotiate
        byte[] smb = new byte[]
        {
            // SMB header
            0xFF, 0x53, 0x4D, 0x42,  // \xFFSMB
            0x72,                     // Command: Negotiate
            0x00, 0x00, 0x00, 0x00,  // Status
            0x18,                     // Flags
            0x01, 0x28,              // Flags2
            0x00, 0x00,              // PID High
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Signature
            0x00, 0x00,              // Reserved
            0xFF, 0xFF,              // TID
            0xFE, 0xFF,              // PID
            0x00, 0x00,              // UID
            0x40, 0x00,              // MID
            // Parameters (WordCount=0)
            0x00,
            // Data: ByteCount + dialects
            0x0C, 0x00,              // ByteCount = 12
            0x02, 0x4E, 0x54, 0x20, 0x4C, 0x4D, 0x20, 0x30, 0x2E, 0x31, 0x32, 0x00,
            // \x02NT LM 0.12\x00
        };
        // Wrap in NBT session message
        var nbt = new byte[4 + smb.Length];
        nbt[0] = 0x00;
        nbt[1] = 0x00;
        nbt[2] = (byte)((smb.Length >> 8) & 0xFF);
        nbt[3] = (byte)(smb.Length & 0xFF);
        Buffer.BlockCopy(smb, 0, nbt, 4, smb.Length);
        return nbt;
    }

    private static (string? Computer, string? Os) ParseNegotiateResponse(byte[] buf, int n)
    {
        // Look for OS string in SMBv1 negotiate response (UTF-16LE strings after header)
        if (n < 40) return (null, null);
        try
        {
            // OS string starts at offset ~47 in SMBv1 response (variable)
            // Find null-terminated UTF-16LE strings
            int start = 40;
            string? os = ReadUtf16String(buf, ref start, n);
            string? lanman = ReadUtf16String(buf, ref start, n);
            return (null, os);
        }
        catch { return (null, null); }
    }

    private static string? ReadUtf16String(byte[] buf, ref int pos, int max)
    {
        int end = pos;
        while (end + 1 < max && (buf[end] != 0 || buf[end + 1] != 0))
            end += 2;
        if (end == pos) { pos += 2; return null; }
        var s = Encoding.Unicode.GetString(buf, pos, end - pos).Trim();
        pos = end + 2;
        return s.Length > 0 ? s : null;
    }
}
