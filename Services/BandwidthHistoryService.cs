namespace AgTarama.Services;

public sealed record BandwidthSample(DateTime Time, double RxBps, double TxBps);

public static class BandwidthHistoryService
{
    private const int Cap = 3600;

    // Aggregate (all adapters summed) — one sample per second
    private static readonly double[] _rxBuf = new double[Cap];
    private static readonly double[] _txBuf = new double[Cap];
    private static int _head;
    private static int _count;
    private static readonly object _sync = new();

    public static void RecordTick(double totalRxBps, double totalTxBps)
    {
        lock (_sync)
        {
            _rxBuf[_head] = totalRxBps;
            _txBuf[_head] = totalTxBps;
            _head = (_head + 1) % Cap;
            if (_count < Cap) _count++;
        }
    }

    public static (double[] Rx, double[] Tx) GetAggregate(int seconds)
    {
        lock (_sync)
        {
            var n = Math.Min(_count, Math.Min(seconds, Cap));
            var rx = new double[n];
            var tx = new double[n];
            for (int i = 0; i < n; i++)
            {
                var idx = ((_head - 1 - i) % Cap + Cap) % Cap;
                rx[n - 1 - i] = _rxBuf[idx];
                tx[n - 1 - i] = _txBuf[idx];
            }
            return (rx, tx);
        }
    }

    public static (double PeakRx, double PeakTx, double AvgRx, double AvgTx, long TotalRxMB, long TotalTxMB)
        Stats(int seconds)
    {
        var (rx, tx) = GetAggregate(seconds);
        if (rx.Length == 0) return (0, 0, 0, 0, 0, 0);
        return (
            rx.Max(), tx.Max(),
            rx.Average(), tx.Average(),
            (long)(rx.Sum() / 1_000_000.0),
            (long)(tx.Sum() / 1_000_000.0)
        );
    }
}
