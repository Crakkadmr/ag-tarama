using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
    // IcmpProbe + TcpPortProbe progress callback'i; ikisi ortalama "tarama %"i belirler.
    // ICMP hızlı biter (~3s), TCP-Port uzun sürer (~30-60s); ikisinin toplam ilerlemesi
    // gerçek tarama yüzdesini doğru yansıtır (sadece ICMP olsa %100 erken görünür).
    // ARP çıkarıldı — Faz 0'da ayrı çalıştırılır; böylece TcpPortProbe store'u bilir.
    private static IProbe[] BuildFastProbesWithoutArp(Action? onHostDone = null) =>
    [
        new IcmpProbe(onHostDone),
        new TcpPortProbe(onHostDone),
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
        new TelnetBannerProbe(),
        new RtspProbe(),
        new MqttProbe(),
    ];

    // ── Listener'lar (broadcast/multicast dinleyiciler) ───────────────
    private static IListener[] BuildListeners(bool deep) =>
    [
        new OnvifWsdListener(),
        new SsdpListener(),
        new MdnsListener(),
        new PassivePacketSniffer(),
        new DhcpListener(),
        ..deep ? (IListener[])[new MndpListener(), new UbiquitiListener()] : [],
    ];

    public async Task StartScanAsync(
        IReadOnlyList<(string Prefix, int Start, int End)> subnets,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken token)
    {
        Store.Clear();

        // Host sayısı (gerçek subnet host kapasitesi)
        int hostCount = subnets.Sum(s => Math.Max(0, s.End - s.Start + 1));
        // Toplam ilerleme adımı: ICMP + TcpPortProbe iki kaynak host başına +1 attığı için 2x.
        // Sayaç tek probe'la %100'e ulaşmaz; her iki probe da bitene kadar dolmaya devam eder.
        int toplam  = hostCount * 2;
        int taranan = 0;
        int paket   = 0;

        using var reportTimer = new System.Timers.Timer(250);
        reportTimer.Elapsed += (_, _) =>
            progress?.Report(new ScanProgress(taranan, toplam, Store.Count,
                $"{Math.Min(taranan / 2, hostCount)}/{hostCount} host • {Store.Count} cihaz", paket));
        reportTimer.Start();

        // Detay raporu yardımcısı — UI overlay'i için probe/listener bazlı bildirim.
        void ReportDetay(string detay) =>
            progress?.Report(new ScanProgress(taranan, toplam, Store.Count,
                $"{Math.Min(taranan / 2, hostCount)}/{hostCount} host • {Store.Count} cihaz", paket, detay));

        foreach (var (prefix, start, end) in subnets)
        {
            if (token.IsCancellationRequested) break;

            ReportDetay($"▶ Subnet {prefix}.0/24 başlatıldı");

            // Listener'lar ListenerDurationMs boyunca arka planda çalışır
            using var listenerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            listenerCts.CancelAfter(options.ListenerDurationMs);
            var listeners = BuildListeners(options.DeepScan);
            ReportDetay($"📡 Listener'lar açıldı ({listeners.Length} adet, {options.ListenerDurationMs / 1000}s)");
            var listenerTasks = listeners.Select(l =>
                Task.Run(() => l.StartAsync(prefix, Store, listenerCts.Token), listenerCts.Token));

            // Faz 0: ARP önce — store'u hızla doldur; TcpPortProbe bunu SkipDeadHosts için kullanır.
            var arp = new ArpProbe();
            ReportDetay("🔎 Faz 0 — ARP ön tarama başladı");
            await arp.RunRangeAsync(prefix, start, end, Store, options, token).ConfigureAwait(false);
            ReportDetay($"✓ ARP tamamlandı ({Store.Count} cihaz bulundu)");

            // Faz 1: Kalan hızlı probe'lar + listener'lar paralel
            var fastProbes = BuildFastProbesWithoutArp(() => System.Threading.Interlocked.Increment(ref taranan));
            ReportDetay($"⚡ Faz 1 — Hızlı probe'lar başladı ({fastProbes.Length} adet)");
            var fastTasks = fastProbes.Select(p =>
                Task.Run(async () =>
                {
                    await p.RunRangeAsync(prefix, start, end, Store, options, token).ConfigureAwait(false);
                    ReportDetay($"✓ {p.Name} tamamlandı");
                }, token));

            await Task.WhenAll(fastTasks.Concat(listenerTasks)).ConfigureAwait(false);
            ReportDetay($"✔ Faz 1 + Listener bitti");

            // Faz 2: Derin probe'lar — TcpPortProbe sonuçları artık mevcut
            if (options.DeepScan && !token.IsCancellationRequested)
            {
                var deepProbes = BuildDeepProbes();
                ReportDetay($"🔍 Faz 2 — Derin probe'lar başladı ({deepProbes.Length} adet)");
                var deepTasks = deepProbes.Select(p =>
                    Task.Run(async () =>
                    {
                        await p.RunRangeAsync(prefix, start, end, Store, options, token).ConfigureAwait(false);
                        ReportDetay($"✓ {p.Name} (derin) tamamlandı");
                    }, token));
                await Task.WhenAll(deepTasks).ConfigureAwait(false);
                ReportDetay($"✔ Faz 2 bitti");
            }
        }

        reportTimer.Stop();

        // Gateway IP'leri işaretle — NIC default gateway adreslerini Store ile eşle.
        // Bu, ilgili cihazın Tür'ünü "Router/AP" olarak zorunlu kılar (KanitTopla_Gateway).
        var gateways = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().GatewayAddresses)
            .Select(g => g.Address.ToString())
            .Where(ip => !string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var dev in Store.All)
        {
            if (gateways.Contains(dev.Ip)) dev.IsGateway = true;
        }

        // OUI lookup for all discovered devices
        foreach (var dev in Store.All)
        {
            if (string.IsNullOrWhiteSpace(dev.Uretici) && !string.IsNullOrWhiteSpace(dev.MacAdresi))
                dev.Uretici = OuiVendorLookup.Bul(dev.MacAdresi);
        }

        progress?.Report(new ScanProgress(toplam, toplam, Store.Count,
            $"Tamamlandı • {hostCount} host • {Store.Count} cihaz", paket));
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
