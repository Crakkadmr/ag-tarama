using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Listeners;
using AgTarama.Services.Discovery.Models;
using AgTarama.Services.Discovery.Probes;

namespace AgTarama.Services.Discovery;

internal sealed class DeviceDiscoveryEngine : IDeviceDiscoveryEngine
{
    public DeviceStore Store { get; } = new();
    public bool NpcapAvailable => PcapHelper.IsNpcapAvailable;

    // ── Probe factory'ler (per-scan instance — stateless probe'lar için güvenli) ──
    private static IProbe[] BuildFastProbes() =>
    [
        new ArpProbe(),
        new IcmpProbe(),
        new TcpPortProbe(),
        new NetbiosProbe(),
        new LlmnrProbe(),
        new NdpProbe(),
    ];

    private static IProbe[] BuildDeepProbes() =>
    [
        new SnmpProbe(),
        new HttpFingerprintProbe(),
        new SmbProbe(),
        new SshBannerProbe(),
    ];

    // ── Listener'lar (broadcast/multicast dinleyiciler) ───────────────
    private static IListener[] BuildListeners(bool deep) =>
    [
        new OnvifWsdListener(),
        new SsdpListener(),
        new MdnsListener(),
        new PassivePacketSniffer(),
        ..deep ? (IListener[])[new MndpListener(), new UbiquitiListener()] : [],
    ];

    public async Task StartScanAsync(
        IReadOnlyList<(string Prefix, int Start, int End)> subnets,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken token)
    {
        Store.Clear();

        int toplam = subnets.Sum(s => Math.Max(0, s.End - s.Start + 1));
        int taranan = 0;
        int paket   = 0;

        using var reportTimer = new System.Timers.Timer(250);
        reportTimer.Elapsed += (_, _) =>
            progress?.Report(new ScanProgress(taranan, toplam, Store.Count,
                $"{taranan}/{toplam} host • {Store.Count} cihaz", paket));
        reportTimer.Start();

        foreach (var (prefix, start, end) in subnets)
        {
            if (token.IsCancellationRequested) break;

            // Listener'lar ListenerDurationMs boyunca arka planda çalışır
            using var listenerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            listenerCts.CancelAfter(options.ListenerDurationMs);
            var listeners = BuildListeners(options.DeepScan);
            var listenerTasks = listeners.Select(l =>
                Task.Run(() => l.StartAsync(prefix, Store, listenerCts.Token), listenerCts.Token));

            // Faz 1: Hızlı probe'lar + listener'lar paralel
            var fastTasks = BuildFastProbes().Select(p =>
                Task.Run(async () =>
                    await p.RunRangeAsync(prefix, start, end, Store, options, token).ConfigureAwait(false),
                token));

            await Task.WhenAll(fastTasks.Concat(listenerTasks)).ConfigureAwait(false);
            System.Threading.Interlocked.Add(ref taranan, Math.Max(0, end - start + 1));

            // Faz 2: Derin probe'lar — TcpPortProbe sonuçları artık mevcut
            if (options.DeepScan && !token.IsCancellationRequested)
            {
                var deepTasks = BuildDeepProbes().Select(p =>
                    Task.Run(async () =>
                        await p.RunRangeAsync(prefix, start, end, Store, options, token).ConfigureAwait(false),
                    token));
                await Task.WhenAll(deepTasks).ConfigureAwait(false);
            }
        }

        reportTimer.Stop();

        // OUI lookup for all discovered devices
        foreach (var dev in Store.All)
        {
            if (string.IsNullOrWhiteSpace(dev.Uretici) && !string.IsNullOrWhiteSpace(dev.MacAdresi))
                dev.Uretici = OuiVendorLookup.Bul(dev.MacAdresi);
        }

        progress?.Report(new ScanProgress(toplam, toplam, Store.Count,
            $"Tamamlandı • {Store.Count} cihaz", paket));
    }

    public async Task StartLiveAsync(
        IReadOnlyList<(string Prefix, int Start, int End)> subnets,
        ScanOptions options,
        CancellationToken token)
    {
        Store.Clear();

        var liveOptions = new ScanOptions
        {
            DeepScan          = false,
            LiveMode          = true,
            Ports             = options.Ports,
            PingTimeoutMs     = options.PingTimeoutMs,
            ArpTimeoutMs      = options.ArpTimeoutMs,
            ListenerDurationMs = int.MaxValue, // sürekli
        };

        // Listeners sürekli çalışır
        var listenerTasks = subnets.SelectMany(s =>
            BuildListeners(false).Select(l =>
                Task.Run(() => l.StartAsync(s.Prefix, Store, token), token)));

        // ARP sweep periyodik
        var arpTask = Task.Run(async () =>
        {
            var arp = new ArpProbe();
            while (!token.IsCancellationRequested)
            {
                foreach (var (prefix, start, end) in subnets)
                {
                    if (token.IsCancellationRequested) break;
                    await arp.RunRangeAsync(prefix, start, end, Store, liveOptions, token)
                        .ConfigureAwait(false);
                }
                // Mark offline devices
                var threshold = DateTime.Now.AddMilliseconds(-options.LiveOfflineThresholdMs);
                foreach (var dev in Store.All)
                {
                    if (dev.LastSeen < threshold && dev.Online)
                    {
                        dev.Online = false;
                        Store.NotifyChanged(dev);
                    }
                }
                await Task.Delay(options.LiveRefreshIntervalMs, token).ConfigureAwait(false);
            }
        }, token);

        await Task.WhenAll(listenerTasks.Append(arpTask)).ConfigureAwait(false);
    }
}
