using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services;

internal sealed class CaptureService : IDisposable
{
    private Process? _proc;
    private int _paketSayisi;

    public int PaketSayisi => _paketSayisi;

    public async Task YakalaAsync(
        IEnumerable<string> arayuzNolar,
        string pcapDosya,
        int hedefKB,
        Action<double, int, TimeSpan> onProgress,
        CancellationToken token)
    {
        var iArgs = string.Join(" ", arayuzNolar.Select(n => $"-i {n}"));
        var psi = new ProcessStartInfo(
            Paths.TsharkExe,
            $"{iArgs} -w \"{pcapDosya}\" -a filesize:{hedefKB} -P")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        _proc = Process.Start(psi)
            ?? throw new InvalidOperationException("tshark başlatılamadı.");
        _paketSayisi = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!_proc.StandardOutput.EndOfStream)
                {
                    await _proc.StandardOutput.ReadLineAsync();
                    Interlocked.Increment(ref _paketSayisi);
                }
            }
            catch { }
        });

        var baslangic = DateTime.Now;
        while (!token.IsCancellationRequested && _proc.HasExited == false)
        {
            try
            {
                double mb = File.Exists(pcapDosya)
                    ? new FileInfo(pcapDosya).Length / (1024.0 * 1024.0) : 0;
                onProgress(mb, _paketSayisi, DateTime.Now - baslangic);
            }
            catch { }
            try { await Task.Delay(500, token); } catch (TaskCanceledException) { break; }
        }

        if (!token.IsCancellationRequested)
        {
            try { await _proc.WaitForExitAsync(); } catch { }
        }
    }

    public void Durdur()
    {
        try { _proc?.Kill(entireProcessTree: true); } catch { }
        _proc = null;
    }

    public void Dispose() => Durdur();
}
