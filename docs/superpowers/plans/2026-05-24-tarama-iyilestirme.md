# Cihaz Tarama İyileştirmesi — Hız + Doğruluk + Potansiyel

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cihaz tarama hızını ~3-5x artırmak (ölü host'lara boşa harcanan TCP timeout'larını kesmek), 3 yeni derin probe ile doğruluğu yükseltmek (Telnet banner, RTSP server, MQTT onayı) ve yeni sinyalleri sınıflandırıcıya entegre etmek.

**Architecture:** Üç bağımsız eksen:  
1. **Hız** — `ScanOptions` timeout'ları düşürülür; `TcpPortProbe`'a `SkipDeadHosts` seçeneği eklenir (ARP ile bulunamayan host'lar atlanır); Engine ARP'ı ayrı "Faz 0" olarak çalıştırır, ardından TcpPortProbe yalnızca store'da var olan host'ları tarar.  
2. **Doğruluk** — `TelnetBannerProbe` (port 23 → router/switch OS), `RtspProbe` (port 554 → kamera Server header), `MqttProbe` (port 1883 → IoT CONNACK), her biri TDD ile geliştirilir; yalnızca ilgili port open olduğunda çalışır.  
3. **Sınıflandırma** — `ClassificationTypes.cs`'e yeni `KanitKaynak` / `KanitAgirlik` değerleri eklenir; `KimlikBelirleV2` yeni sinyalleri tüketir.

**Tech Stack:** .NET 10, C#, xUnit (test dosyaları `AgTarama.Tests` dizinindedir — git repo DIŞI, commit edilmez).

---

## Ön Bilgiler (uygulayıcı için — okumadan başlama)

**Repo yapısı:**
- Git repo kökü: `D:\Projects\AG TARAMA PROGRAMI\AgTarama`
- Çözüm dosyası + test projesi: `D:\Projects\AG TARAMA PROGRAMI\` (bir üst klasör, git DIŞI)
- Commit: **yalnızca** `AgTarama\...` altındaki dosyalar — test dosyalarını `git add` etme.
- Build: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
- Test: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~<Filtre>" --nologo -v minimal`
- Shell: PowerShell (`&&` yok; komutları `;` veya ayrı satır ile zincirle).

**Mevcut probe akışı (kısa özet):**
- `DeviceDiscoveryEngine.StartScanAsync` → Faz 1 (hızlı) + Listener paralel, sonra Faz 2 (derin/DeepScan=true)
- Faz 1 probe'ları: `BuildFastProbes()` → `[ArpProbe, IcmpProbe, TcpPortProbe, NetbiosProbe, LlmnrProbe, NdpProbe]`
- Faz 2 probe'ları: `BuildDeepProbes()` → `[SnmpProbe, HttpFingerprintProbe, SmbProbe, SshBannerProbe]`
- `ScanOptions.Ports` (26 port, satır 16-21) zaten `23, 554, 1883` içeriyor → yeni probe'lar için port eklemek gerekmez.
- `TcpPortProbe`: her host için portları **sıralı** dener; `ConcurrencyLimit` (default 80) host başına paralel slot sayısını değil tüm bağlantı havuzunu sınırlar.
- `DeviceInfo` (`Services/Discovery/Models/DeviceInfo.cs`) — yeni alanlar bu dosyaya eklenir.
- `KanitKaynak` enum ve `KanitAgirlik` sabitleri: `Services/Discovery/Classification/ClassificationTypes.cs`.
- Sınıflandırıcı: `Partials/MainWindow.DeviceClassifier.cs` — `KimlikBelirleV2()` çağıran `KanitTopla_*` metodları.
- `InternalsVisibleTo("AgTarama.Tests")` açık — internal sınıflar test edilebilir.

**Performans matematiği (neden SkipDeadHosts önemli):**
- /24 subnet = 254 host, 26 port × 800ms timeout = en kötü durum ~66s (ölü host'lar filtered port'larla timeout bekler)
- ARP-önce ile ~20 online host bulundu → SkipDeadHosts=true ile: 20 × 26 port × 450ms = ~3s → **20x hızlanma**
- Gerçek ortam (mixed): ~3-5x ortalama hızlanma

---

## File Structure

| Dosya | Tip | Sorumluluk |
|---|---|---|
| `Services/Discovery/ScanOptions.cs` | Değişiklik | `SkipDeadHosts` ekle; timeout varsayılanlarını düşür |
| `Services/Discovery/Probes/TcpPortProbe.cs` | Değişiklik | `SkipDeadHosts` mantığı + `ShouldSkip` static method |
| `Services/Discovery/Probes/LlmnrProbe.cs:41` | Değişiklik | LLMNR bekleme süresi 4000ms → 2000ms |
| `Services/Discovery/DeviceDiscoveryEngine.cs` | Değişiklik | ARP'ı Faz 0 yap; `BuildFastProbesWithoutArp` ekle |
| `Services/Discovery/Probes/TelnetBannerProbe.cs` | Yeni | Port 23 banner → router/switch OS tespiti |
| `Services/Discovery/Probes/RtspProbe.cs` | Yeni | Port 554 OPTIONS → kamera Server header |
| `Services/Discovery/Probes/MqttProbe.cs` | Yeni | Port 1883 CONNECT → CONNACK onayı |
| `Services/Discovery/Models/DeviceInfo.cs` | Değişiklik | `TelnetBanner`, `RtspServerHeader`, `MqttBulundu` ekle |
| `Services/Discovery/Classification/ClassificationTypes.cs` | Değişiklik | `Telnet`, `Rtsp`, `Mqtt` kaynak + ağırlık sabitleri |
| `Partials/MainWindow.DeviceClassifier.cs` | Değişiklik | `KanitTopla_Telnet`, `KanitTopla_Rtsp`, `KanitTopla_Mqtt` |
| `AgTarama.Tests/TaramaIyilestirmeTests.cs` | Yeni test (repo DIŞI) | TDD testleri |

---

## Task 1: ScanOptions timeout'ları + TcpPortProbe.ShouldSkip mantığı

**Files:**
- Modify: `Services/Discovery/ScanOptions.cs`
- Modify: `Services/Discovery/Probes/TcpPortProbe.cs`
- Modify: `Services/Discovery/Probes/LlmnrProbe.cs` (satır 41)
- Test: `AgTarama.Tests/TaramaIyilestirmeTests.cs` (repo dışı — commit edilmez)

- [ ] **Step 1: Failing testler yaz**

`AgTarama.Tests/TaramaIyilestirmeTests.cs`:

```csharp
using AgTarama.Services.Discovery;
using AgTarama.Services.Discovery.Probes;
using Xunit;

namespace AgTarama.Tests;

public class TaramaIyilestirmeTests
{
    // ── TcpPortProbe.ShouldSkip mantık testleri ─────────────────────────

    [Fact]
    public void ShouldSkip_BosDukkanda_HicAtlama()
    {
        var store   = new DeviceStore();
        var options = new ScanOptions { SkipDeadHosts = true };
        // Store boş → Count=0 → fallback to full scan (geriye uyum)
        Assert.False(TcpPortProbe.ShouldSkip("192.168.1.1", store, options));
    }

    [Fact]
    public void ShouldSkip_DoluDukkanda_YabanciAtlanir()
    {
        var store = new DeviceStore();
        store.GetOrAdd("192.168.1.5");                             // Tek kayıtlı host
        var options = new ScanOptions { SkipDeadHosts = true };

        Assert.True(TcpPortProbe.ShouldSkip("192.168.1.1", store, options));  // store'da yok → atla
        Assert.False(TcpPortProbe.ShouldSkip("192.168.1.5", store, options)); // store'da var → tarama
    }

    [Fact]
    public void ShouldSkip_SecilenekFalse_HicAtlama()
    {
        var store = new DeviceStore();
        store.GetOrAdd("192.168.1.5");
        var options = new ScanOptions { SkipDeadHosts = false };

        Assert.False(TcpPortProbe.ShouldSkip("192.168.1.1", store, options)); // opsiyon kapalı
        Assert.False(TcpPortProbe.ShouldSkip("192.168.1.5", store, options));
    }
}
```

- [ ] **Step 2: Testi çalıştır, FAIL doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: FAIL — `SkipDeadHosts` yok, `TcpPortProbe.ShouldSkip` yok.

- [ ] **Step 3: ScanOptions güncelle**

`Services/Discovery/ScanOptions.cs` dosyasının tamamını şununla değiştir:

```csharp
namespace AgTarama.Services.Discovery;

internal sealed class ScanOptions
{
    public bool DeepScan              { get; set; } = false;
    public bool LiveMode              { get; set; } = false;
    public bool SkipDeadHosts         { get; set; } = true;   // ARP ile bulunmayan host'ları TCP'de atla
    public int[] Ports                { get; set; } = DefaultPorts;
    public int ConcurrencyLimit       { get; set; } = 80;
    public int PingTimeoutMs          { get; set; } = 600;    // 1000 → 600 ms
    public int PortTimeoutMs          { get; set; } = 450;    // 800 → 450 ms
    public int ArpTimeoutMs           { get; set; } = 3000;
    public int ListenerDurationMs     { get; set; } = 8000;
    public int LiveRefreshIntervalMs  { get; set; } = 30_000;
    public int LiveOfflineThresholdMs { get; set; } = 90_000;

    public static readonly int[] DefaultPorts =
    {
        21,   22,   23,   53,   80,   135,  139,  443,  445,  515,
        554,  631,  1883, 1900, 3389, 5000, 5060, 5357, 7547, 8000,
        8080, 8443, 9000, 9100, 34567, 37777,
    };
}
```

- [ ] **Step 4: TcpPortProbe'a ShouldSkip + mantık ekle**

`Services/Discovery/Probes/TcpPortProbe.cs` dosyasının tamamını şununla değiştir:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class TcpPortProbe : IProbe
{
    private readonly Action? _onHostDone;

    public TcpPortProbe(Action? onHostDone = null)
    {
        _onHostDone = onHostDone;
    }

    public string Name => "TCP-Port";

    // ARP tarafından doldurulmuş store kullanılarak ölü host'ların atlanıp atlanmaması gerektiğini belirler.
    // Store boşsa (Npcap yok, ARP sonuç vermedi) geriye uyum için tam tarama yapılır.
    internal static bool ShouldSkip(string ip, DeviceStore store, ScanOptions options)
        => options.SkipDeadHosts && store.Count > 0 && !store.TryGet(ip, out _);

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(options.ConcurrencyLimit);
        int count = Math.Max(0, hostEnd - hostStart + 1);

        var tasks = Enumerable.Range(hostStart, count).Select(i => Task.Run(async () =>
        {
            var ip = $"{subnetPrefix}.{i}";

            if (ShouldSkip(ip, store, options))
            {
                _onHostDone?.Invoke();
                return;
            }

            var acik = new List<int>();

            foreach (var port in options.Ports)
            {
                if (token.IsCancellationRequested) break;
                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts2.CancelAfter(options.PortTimeoutMs);
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(ip, port, cts2.Token).ConfigureAwait(false);
                    acik.Add(port);
                }
                catch { }
                finally { sem.Release(); }
            }

            if (acik.Count > 0)
            {
                var bilgi = store.GetOrAdd(ip);
                lock (bilgi.AcikPortlar)
                    bilgi.AcikPortlar.AddRange(acik.Except(bilgi.AcikPortlar));
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("Port");
                store.NotifyChanged(bilgi);
            }

            _onHostDone?.Invoke();
        }, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: LlmnrProbe bekleme süresini 2000ms yap**

`Services/Discovery/Probes/LlmnrProbe.cs` satır 41'deki `4000` değerini `2000` ile değiştir:

```csharp
            cts2.CancelAfter(2000);
```

- [ ] **Step 6: Testleri çalıştır, PASS doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: PASS (3 test).

- [ ] **Step 7: Build doğrula**

```
dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo
```

Expected: 0 Hata.

- [ ] **Step 8: Commit**

```
git add Services/Discovery/ScanOptions.cs Services/Discovery/Probes/TcpPortProbe.cs Services/Discovery/Probes/LlmnrProbe.cs
git commit -m "perf: TcpPortProbe SkipDeadHosts + timeout azaltma (ping 600ms, port 450ms, llmnr 2000ms)"
```

---

## Task 2: Engine — ARP Faz 0 + BuildFastProbesWithoutArp

Bu task, Task 1'deki `SkipDeadHosts` mantığının fiilen işe yaramasını sağlar: ARP ayrı bir faz olarak önce çalışır ve store'u doldurur; ardından `TcpPortProbe` store'daki host'ları bilir.

**Files:**
- Modify: `Services/Discovery/DeviceDiscoveryEngine.cs`

- [ ] **Step 1: DeviceDiscoveryEngine.cs'i güncelle**

`DeviceDiscoveryEngine.cs` dosyasında `BuildFastProbes` metodunu bul ve şununla değiştir:

```csharp
    // ARP çıkarıldı — Faz 0'da ayrı çalıştırılır; böylece TcpPortProbe store'u bilir.
    private static IProbe[] BuildFastProbesWithoutArp(Action? onHostDone = null) =>
    [
        new IcmpProbe(onHostDone),
        new TcpPortProbe(onHostDone),
        new NetbiosProbe(),
        new LlmnrProbe(),
        new NdpProbe(),
    ];
```

Ardından `StartScanAsync` içindeki şu satırları:

```csharp
            // Faz 1: Hızlı probe'lar + listener'lar paralel
            // IcmpProbe her host bitince taranan++ → reportTimer gerçek zamanlı progress yayar.
            var fastProbes = BuildFastProbes(() => System.Threading.Interlocked.Increment(ref taranan));
            ReportDetay($"⚡ Faz 1 — Hızlı probe'lar başladı ({fastProbes.Length} adet)");
            var fastTasks = fastProbes.Select(p =>
                Task.Run(async () =>
                {
                    await p.RunRangeAsync(prefix, start, end, Store, options, token).ConfigureAwait(false);
                    ReportDetay($"✓ {p.Name} tamamlandı");
                }, token));

            await Task.WhenAll(fastTasks.Concat(listenerTasks)).ConfigureAwait(false);
```

şununla değiştir:

```csharp
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
```

- [ ] **Step 2: Build doğrula**

```
dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo
```

Expected: 0 Hata. (`BuildFastProbes` artık kullanılmıyorsa derleyici uyarısı verebilir — eski metodu sil veya rename et.)

> Not: `BuildFastProbes` referansı `StartLiveAsync`'te varsa yok — orada sadece `ArpProbe` kullanılıyor. Derleme başarılı olursa sorun yok.

- [ ] **Step 3: Commit**

```
git add Services/Discovery/DeviceDiscoveryEngine.cs
git commit -m "perf: Engine ARP faz0 oncesi — TcpPortProbe artik online hostlari biliyor"
```

---

## Task 3: TelnetBannerProbe — router/switch/embedded OS tespiti

Port 23 (zaten `DefaultPorts`'ta), banner grab ile ağ cihazı işletim sistemi tespiti.

**Files:**
- Create: `Services/Discovery/Probes/TelnetBannerProbe.cs`
- Modify: `Services/Discovery/Models/DeviceInfo.cs` (`TelnetBanner` alanı)
- Test: `AgTarama.Tests/TaramaIyilestirmeTests.cs`

- [ ] **Step 1: Failing testler ekle**

`AgTarama.Tests/TaramaIyilestirmeTests.cs`'e ekle:

```csharp
    // ── TelnetBannerProbe banner ayrıştırma testleri ─────���───────────────

    [Theory]
    [InlineData("RouterOS 7.8 (MikroTik)",          "MikroTik",  "Router/AP")]
    [InlineData("MikroTik RouterBOARD 952",          "MikroTik",  "Router/AP")]
    [InlineData("Cisco IOS Software, Version 15.2",  "Cisco",     "Switch")]
    [InlineData("Catalyst 2960 Software",            "Cisco",     "Switch")]
    [InlineData("HP ProCurve Switch J9781A",         "HP",        "Switch")]
    [InlineData("ProCurve Network Switch",           "HP",        "Switch")]
    [InlineData("Junos 21.2R1.10",                  "Juniper",   "Switch")]
    [InlineData("JUNOS Base OS release 12.1",        "Juniper",   "Switch")]
    [InlineData("FortiGate-100F v7.0",              "Fortinet",  "Güvenlik Duvarı")]
    [InlineData("pfSense 2.7.0-RELEASE",            "pfSense",   "Güvenlik Duvarı")]
    [InlineData("OPNsense 23.7",                    "OPNsense",  "Güvenlik Duvarı")]
    [InlineData("OpenWrt 22.03.5",                  "OpenWrt",   "Router/AP")]
    [InlineData("DD-WRT v3.0",                      "DD-WRT",    "Router/AP")]
    [InlineData("ZyNOS v4.50",                      "ZyXEL",     "Switch/AP")]
    [InlineData("ArubaOS 8.9.0",                    "Aruba",     "Switch/AP")]
    [InlineData("BusyBox v1.35.0",                   null,        "Linux IoT")]
    [InlineData("Welcome to something random",       null,        "Router/Switch")]
    public void TelnetBannerProbe_AyrıştırBanner_DogruSonuc(
        string banner, string? beklenenMarka, string beklenenTur)
    {
        var (marka, tur) = TelnetBannerProbe.AyrıştırBanner(banner);
        Assert.Equal(beklenenMarka, marka);
        Assert.Equal(beklenenTur, tur);
    }
```

- [ ] **Step 2: Fail doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: FAIL �� `TelnetBannerProbe` yok.

- [ ] **Step 3: DeviceInfo'ya TelnetBanner alanı ekle**

`Services/Discovery/Models/DeviceInfo.cs` dosyasında `SshBanner` satırından sonra ekle:

```csharp
    public string?   TelnetBanner    { get; set; }
```

- [ ] **Step 4: TelnetBannerProbe oluştur**

Yeni dosya `Services/Discovery/Probes/TelnetBannerProbe.cs`:

```csharp
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class TelnetBannerProbe : IProbe
{
    public string Name => "Telnet";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);

        var tasks = System.Linq.Enumerable.Range(hostStart, Math.Max(0, hostEnd - hostStart + 1))
            .Select(i => System.Threading.Tasks.Task.Run(async () =>
            {
                var ip = $"{subnetPrefix}.{i}";
                if (!store.TryGet(ip, out var bilgi)) return;
                bool port23Acik;
                lock (bilgi!.AcikPortlar) port23Acik = bilgi.AcikPortlar.Contains(23);
                if (!port23Acik) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var banner = await BannerOkuAsync(ip, token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(banner)) return;

                    bilgi.TelnetBanner = banner;
                    bilgi.KesifKaynaklari.Add("Telnet");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<string?> BannerOkuAsync(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 23, cts.Token).ConfigureAwait(false);
            tcp.ReceiveTimeout = 1500;
            using var ns = tcp.GetStream();
            var buf = new byte[512];
            var read = await ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (read <= 0) return null;
            // IAC seçeneklerini (0xFF ile başlayan kontrol dizilerini) filtrele
            var sb = new StringBuilder();
            for (int i = 0; i < read; i++)
            {
                if (buf[i] == 0xFF) { i += 2; continue; }  // IAC + cmd + option
                if (buf[i] >= 0x20 || buf[i] == '\n' || buf[i] == '\r')
                    sb.Append((char)buf[i]);
            }
            return sb.ToString().Trim();
        }
        catch { return null; }
    }

    // Telnet banner'ından marka ve cihaz türü çıkar.
    internal static (string? Marka, string Tur) AyrıştırBanner(string banner)
    {
        var b = banner.ToLowerInvariant();
        if (b.Contains("routeros") || b.Contains("mikrotik") || b.Contains("routerboard"))
            return ("MikroTik", "Router/AP");
        if (b.Contains("cisco ios") || b.Contains("catalyst") || b.Contains("cisco small business"))
            return ("Cisco", "Switch");
        if (b.Contains("procurve") || b.Contains("hp network") || b.Contains("hp procurve"))
            return ("HP", "Switch");
        if (b.Contains("junos") || b.Contains("juniper"))
            return ("Juniper", "Switch");
        if (b.Contains("arubaos") || b.Contains("aruba networks"))
            return ("Aruba", "Switch/AP");
        if (b.Contains("fortigate") || b.Contains("fortinet"))
            return ("Fortinet", "Güvenlik Duvarı");
        if (b.Contains("pfsense"))
            return ("pfSense", "Güvenlik Duvarı");
        if (b.Contains("opnsense"))
            return ("OPNsense", "Güvenlik Duvarı");
        if (b.Contains("openwrt") || b.Contains("luci"))
            return ("OpenWrt", "Router/AP");
        if (b.Contains("dd-wrt"))
            return ("DD-WRT", "Router/AP");
        if (b.Contains("zynos") || b.Contains("zyxel"))
            return ("ZyXEL", "Switch/AP");
        if (b.Contains("busybox"))
            return (null, "Linux IoT");
        return (null, "Router/Switch");
    }
}
```

- [ ] **Step 5: Testleri çalıştır, PASS doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: PASS (tüm `TelnetBannerProbe` testleri dahil).

- [ ] **Step 6: Build doğrula**

```
dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo
```

Expected: 0 Hata.

- [ ] **Step 7: Commit**

```
git add Services/Discovery/Probes/TelnetBannerProbe.cs Services/Discovery/Models/DeviceInfo.cs
git commit -m "feat: TelnetBannerProbe — port 23 banner ile router/switch OS tespiti"
```

---

## Task 4: RtspProbe — kamera Server header doğrulaması

Port 554 (zaten `DefaultPorts`'ta), RTSP OPTIONS isteği ile kamerayı doğrula ve `Server:` header'ını çıkar.

**Files:**
- Create: `Services/Discovery/Probes/RtspProbe.cs`
- Modify: `Services/Discovery/Models/DeviceInfo.cs` (`RtspServerHeader` alanı)
- Test: `AgTarama.Tests/TaramaIyilestirmeTests.cs`

- [ ] **Step 1: Failing testler ekle**

`AgTarama.Tests/TaramaIyilestirmeTests.cs`'e ekle:

```csharp
    // ── RtspProbe server header ayrıştırma testleri ──────────────────────

    [Theory]
    [InlineData("RTSP/1.0 200 OK\r\nServer: Hikvision-Webs\r\nContent-Length: 0\r\n\r\n", "Hikvision-Webs")]
    [InlineData("RTSP/1.0 200 OK\r\nserver: Dahua/V2.800\r\n\r\n", "Dahua/V2.800")]
    [InlineData("RTSP/1.0 200 OK\r\nPublic: DESCRIBE\r\n\r\n", null)]
    [InlineData("RTSP/1.0 200 OK\r\nServer: Axis RTSP Server\r\n\r\n", "Axis RTSP Server")]
    [InlineData("", null)]
    public void RtspProbe_AyrıştırSunucu_DogruSonuc(string yanit, string? beklenen)
    {
        Assert.Equal(beklenen, RtspProbe.AyrıştırSunucu(yanit));
    }

    [Theory]
    [InlineData("RTSP/1.0 200 OK\r\n", true)]
    [InlineData("RTSP/1.0 401 Unauthorized\r\n", true)]  // 401 de RTSP cihazıdır
    [InlineData("HTTP/1.1 200 OK\r\n", false)]
    [InlineData("", false)]
    public void RtspProbe_RtspYaniti_Dogrula(string yanit, bool beklenen)
    {
        Assert.Equal(beklenen, RtspProbe.RtspYanitiMi(yanit));
    }
```

- [ ] **Step 2: Fail doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: FAIL — `RtspProbe` yok.

- [ ] **Step 3: DeviceInfo'ya RtspServerHeader alanı ekle**

`Services/Discovery/Models/DeviceInfo.cs` dosyasında `TelnetBanner` satırından sonra ekle:

```csharp
    public string?   RtspServerHeader { get; set; }
```

- [ ] **Step 4: RtspProbe oluştur**

Yeni dosya `Services/Discovery/Probes/RtspProbe.cs`:

```csharp
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class RtspProbe : IProbe
{
    public string Name => "RTSP";

    // RTSP OPTIONS isteği — kimlik doğrulama gerektirmez, Server header döner.
    private const string RtspOptionsIstek =
        "OPTIONS * RTSP/1.0\r\n" +
        "CSeq: 1\r\n" +
        "User-Agent: NetSniffer/1.0\r\n\r\n";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);

        var tasks = System.Linq.Enumerable.Range(hostStart, Math.Max(0, hostEnd - hostStart + 1))
            .Select(i => System.Threading.Tasks.Task.Run(async () =>
            {
                var ip = $"{subnetPrefix}.{i}";
                if (!store.TryGet(ip, out var bilgi)) return;
                bool port554Acik;
                lock (bilgi!.AcikPortlar) port554Acik = bilgi.AcikPortlar.Contains(554);
                if (!port554Acik) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var yanit = await RtspOptionsGonder(ip, token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(yanit) || !RtspYanitiMi(yanit)) return;

                    var sunucu = AyrıştırSunucu(yanit);
                    if (sunucu != null) bilgi.RtspServerHeader = sunucu;
                    bilgi.RtspDurum = "OK";
                    bilgi.KesifKaynaklari.Add("RTSP");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<string?> RtspOptionsGonder(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 554, cts.Token).ConfigureAwait(false);
            tcp.ReceiveTimeout = 1500;
            using var ns = tcp.GetStream();
            var istek = Encoding.ASCII.GetBytes(RtspOptionsIstek);
            await ns.WriteAsync(istek, cts.Token).ConfigureAwait(false);
            var buf = new byte[1024];
            var read = await ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            return read > 0 ? Encoding.ASCII.GetString(buf, 0, read) : null;
        }
        catch { return null; }
    }

    // "Server: <value>" satırını bul ve değeri döndür.
    internal static string? AyrıştırSunucu(string yanit)
    {
        foreach (var satir in yanit.Split('\n'))
        {
            if (satir.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))
                return satir.Substring(7).Trim('\r', '\n', ' ');
        }
        return null;
    }

    // Yanıt gerçekten RTSP cihazından mı geliyor?
    internal static bool RtspYanitiMi(string yanit)
        => yanit.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 5: Testleri çalıştır, PASS doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: PASS (tüm RtspProbe testleri dahil).

- [ ] **Step 6: Build doğrula**

```
dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo
```

Expected: 0 Hata.

- [ ] **Step 7: Commit**

```
git add Services/Discovery/Probes/RtspProbe.cs Services/Discovery/Models/DeviceInfo.cs
git commit -m "feat: RtspProbe — port 554 RTSP OPTIONS ile kamera Server header tespiti"
```

---

## Task 5: MqttProbe — IoT cihaz onayı

Port 1883 (zaten `DefaultPorts`'ta), minimal MQTT CONNECT paketi gönder; CONNACK yanıtı IoT/akıllı ev cihazı olduğunu doğrular.

**Files:**
- Create: `Services/Discovery/Probes/MqttProbe.cs`
- Modify: `Services/Discovery/Models/DeviceInfo.cs` (`MqttBulundu` alanı)
- Test: `AgTarama.Tests/TaramaIyilestirmeTests.cs`

- [ ] **Step 1: Failing testler ekle**

`AgTarama.Tests/TaramaIyilestirmeTests.cs`'e ekle:

```csharp
    // ── MqttProbe CONNACK ayrıştırma testleri ───���────────────────────────

    [Fact]
    public void MqttProbe_ConnackMi_GecerliConnack_True()
    {
        // 0x20=CONNACK, 0x02=remaining length, 0x00=no session, 0x00=accepted
        Assert.True(MqttProbe.ConnackMi(new byte[] { 0x20, 0x02, 0x00, 0x00 }));
    }

    [Fact]
    public void MqttProbe_ConnackMi_KimlikReddi_True()
    {
        // Return code 0x05 = not authorized — hâlâ CONNACK, hâlâ MQTT broker
        Assert.True(MqttProbe.ConnackMi(new byte[] { 0x20, 0x02, 0x00, 0x05 }));
    }

    [Fact]
    public void MqttProbe_ConnackMi_KisaVeri_False()
    {
        Assert.False(MqttProbe.ConnackMi(new byte[] { 0x20, 0x02, 0x00 })); // 3 byte — kısa
    }

    [Fact]
    public void MqttProbe_ConnackMi_YanlisFixedHeader_False()
    {
        Assert.False(MqttProbe.ConnackMi(new byte[] { 0x10, 0x02, 0x00, 0x00 })); // 0x10=CONNECT değil
    }

    [Fact]
    public void MqttProbe_ConnackMi_BosVeri_False()
    {
        Assert.False(MqttProbe.ConnackMi(Array.Empty<byte>()));
    }
```

- [ ] **Step 2: Fail doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: FAIL — `MqttProbe` yok.

- [ ] **Step 3: DeviceInfo'ya MqttBulundu alanı ekle**

`Services/Discovery/Models/DeviceInfo.cs` dosyasında `RtspServerHeader` satırından sonra ekle:

```csharp
    public bool      MqttBulundu     { get; set; }
```

- [ ] **Step 4: MqttProbe oluştur**

Yeni dosya `Services/Discovery/Probes/MqttProbe.cs`:

```csharp
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class MqttProbe : IProbe
{
    public string Name => "MQTT";

    // Minimal MQTT 3.1.1 CONNECT paketi: boş ClientID, CleanSession=true.
    // RFC 7252 — https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html
    private static readonly byte[] MqttConnectPaketi =
    {
        0x10,             // Fixed header: CONNECT (packet type 1)
        0x0C,             // Remaining length = 12
        0x00, 0x04,       // Protocol Name length = 4
        0x4D, 0x51, 0x54, 0x54, // "MQTT"
        0x04,             // Protocol level = 4 (v3.1.1)
        0x02,             // Connect flags: CleanSession = 1
        0x00, 0x3C,       // KeepAlive = 60 saniye
        0x00, 0x00,       // ClientID length = 0 (boş)
    };

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(32);

        var tasks = System.Linq.Enumerable.Range(hostStart, Math.Max(0, hostEnd - hostStart + 1))
            .Select(i => System.Threading.Tasks.Task.Run(async () =>
            {
                var ip = $"{subnetPrefix}.{i}";
                if (!store.TryGet(ip, out var bilgi)) return;
                bool port1883Acik;
                lock (bilgi!.AcikPortlar) port1883Acik = bilgi.AcikPortlar.Contains(1883);
                if (!port1883Acik) return;

                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var yanit = await MqttConnectGonder(ip, token).ConfigureAwait(false);
                    if (yanit == null || !ConnackMi(yanit)) return;

                    bilgi.MqttBulundu = true;
                    bilgi.KesifKaynaklari.Add("MQTT");
                    store.NotifyChanged(bilgi);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

        await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<byte[]?> MqttConnectGonder(string ip, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(2000);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, 1883, cts.Token).ConfigureAwait(false);
            tcp.ReceiveTimeout = 1500;
            using var ns = tcp.GetStream();
            await ns.WriteAsync(MqttConnectPaketi, cts.Token).ConfigureAwait(false);
            var buf = new byte[16];
            var read = await ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (read < 4) return null;
            var yanit = new byte[read];
            Array.Copy(buf, yanit, read);
            return yanit;
        }
        catch { return null; }
    }

    // MQTT CONNACK fixed header = 0x20, remaining length = 0x02
    internal static bool ConnackMi(byte[] veri)
        => veri.Length >= 4 && veri[0] == 0x20 && veri[1] == 0x02;
}
```

- [ ] **Step 5: Testleri çalı��tır, PASS doğrula**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~TaramaIyilestirme" --nologo -v minimal
```

Expected: PASS (tüm MqttProbe testleri dahil).

- [ ] **Step 6: Build doğrula**

```
dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo
```

Expected: 0 Hata.

- [ ] **Step 7: Commit**

```
git add Services/Discovery/Probes/MqttProbe.cs Services/Discovery/Models/DeviceInfo.cs
git commit -m "feat: MqttProbe — port 1883 CONNACK ile IoT cihaz tespiti"
```

---

## Task 6: Sınıflandırma + Engine entegrasyonu

Yeni probe sinyallerini `KanitKaynak`, `KanitAgirlik` ve `KimlikBelirleV2`'ye ekle; yeni probe'ları `BuildDeepProbes`'a bağla.

**Files:**
- Modify: `Services/Discovery/Classification/ClassificationTypes.cs`
- Modify: `Partials/MainWindow.DeviceClassifier.cs`
- Modify: `Services/Discovery/DeviceDiscoveryEngine.cs`

- [ ] **Step 1: ClassificationTypes.cs güncelle**

`Services/Discovery/Classification/ClassificationTypes.cs` dosyasında:

`KanitKaynak` enum'una `Telnet`, `Rtsp`, `Mqtt` ekle:

```csharp
internal enum KanitKaynak
{
    HttpFp, Ubiquiti, MikroTik, Snmp, Onvif, Wsd, Ssdp, Mdns,
    Netbios, OuiMac, PortPattern, Banner, Ttl, AdHostname,
    Llmnr, Smb, Ssh, ArpActive, Gateway, Dhcp,
    Telnet, Rtsp, Mqtt,
}
```

`KanitAgirlik` sınıfına şu sabitleri ekle (mevcut sabitlerin sonuna):

```csharp
    public const int TelnetBanner    = 30;   // Router/switch OS tespiti
    public const int RtspServer      = 35;   // Kamera RTSP sunucu doğrulaması
    public const int MqttDevice      = 20;   // IoT/akıllı ev MQTT broker yanıtı
```

- [ ] **Step 2: Sınıflandırıcıya KanitTopla_Telnet, KanitTopla_Rtsp, KanitTopla_Mqtt ekle**

`Partials/MainWindow.DeviceClassifier.cs` dosyasında `KimlikBelirleV2` içindeki `KanitTopla_*` çağrıları bloğuna (satır 84-102 civarı) şunları ekle:

```csharp
        KanitTopla_Telnet(b, turL, markaL);
        KanitTopla_Rtsp(b, turL, markaL);
        KanitTopla_Mqtt(b, turL, markaL);
```

Ardından dosyanın sonuna (son `}` kapanışından önce) şu metodları ekle:

```csharp
    private static void KanitTopla_Telnet(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.TelnetBanner)) return;
        var (marka, tur) = AgTarama.Services.Discovery.Probes.TelnetBannerProbe.AyrıştırBanner(b.TelnetBanner);
        turL.Add(new TurAdayi(tur, KanitAgirlik.TelnetBanner, KanitKaynak.Telnet, b.TelnetBanner[..Math.Min(b.TelnetBanner.Length, 60)]));
        if (marka != null)
            markaL.Add(new MarkaAdayi(marka, KanitAgirlik.TelnetBanner, KanitKaynak.Telnet, marka));
    }

    private static void KanitTopla_Rtsp(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (string.IsNullOrWhiteSpace(b.RtspServerHeader)) return;
        var srv = b.RtspServerHeader.ToLowerInvariant();
        turL.Add(new TurAdayi("Kamera", KanitAgirlik.RtspServer, KanitKaynak.Rtsp, b.RtspServerHeader));
        foreach (var (anahtar, marka, _) in MarkaIpuclari)
        {
            if (srv.Contains(anahtar, StringComparison.OrdinalIgnoreCase))
            {
                markaL.Add(new MarkaAdayi(marka, KanitAgirlik.RtspServer, KanitKaynak.Rtsp, b.RtspServerHeader));
                break;
            }
        }
    }

    private static void KanitTopla_Mqtt(DeviceInfo b, List<TurAdayi> turL, List<MarkaAdayi> markaL)
    {
        if (!b.MqttBulundu) return;
        turL.Add(new TurAdayi("Akıllı Cihaz", KanitAgirlik.MqttDevice, KanitKaynak.Mqtt, "mqtt-connack"));
    }
```

- [ ] **Step 3: BuildDeepProbes'a yeni probe'ları ekle**

`Services/Discovery/DeviceDiscoveryEngine.cs` dosyasında `BuildDeepProbes()` metodunu şununla değiştir:

```csharp
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
```

- [ ] **Step 4: Build doğrula**

```
dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo
```

Expected: 0 Hata.

- [ ] **Step 5: Tam test paketi çalıştır**

```
dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo -v minimal
```

Expected: Tüm testler PASS (önceden bilinen fail'lar varsa hariç).

- [ ] **Step 6: Commit**

```
git add Services/Discovery/Classification/ClassificationTypes.cs Partials/MainWindow.DeviceClassifier.cs Services/Discovery/DeviceDiscoveryEngine.cs
git commit -m "feat: siniflandirici Telnet/RTSP/MQTT sinyalleri + DeepProbes entegrasyonu"
```

---

## Self-Review

**1. Spec coverage:**
- Hız artışı → Task 1 (timeout azaltma, SkipDeadHosts) + Task 2 (Engine ARP-önce). ✓
- TelnetBannerProbe → Task 3. ✓
- RtspProbe → Task 4. ✓
- MqttProbe → Task 5. ✓
- Sınıflandırma entegrasyonu → Task 6. ✓

**2. Placeholder taraması:**
- Her adımda tam kod var. ✓
- Test teorileri somut InlineData değerleriyle. ✓
- Commit komutları tam dosya listesiyle. ✓

**3. Tip tutarlılığı:**
- `TelnetBannerProbe.AyrıştırBanner(string) → (string? Marka, string Tur)` — Task 3'te tanımlanmış, Task 6'da `KanitTopla_Telnet` içinde aynı imzayla çağrılıyor. ✓
- `RtspProbe.AyrıştırSunucu(string) → string?` ve `RtspProbe.RtspYanitiMi(string) → bool` — Task 4'te tanımlanmış, Task 6'da kullanılmıyor (sınıflandırıcı `RtspServerHeader` alanını kullanıyor). ✓
- `MqttProbe.ConnackMi(byte[]) → bool` — Task 5'te tanımlanmış, sadece test edildi; sınıflandırıcı `MqttBulundu` bool alanını kullan��yor. ✓
- `DeviceInfo.TelnetBanner`, `RtspServerHeader`, `MqttBulundu` — Task 3/4/5'te eklendi, Task 6'da kullanılıyor. ✓
- `KanitKaynak.Telnet/Rtsp/Mqtt` ve `KanitAgirlik.TelnetBanner/RtspServer/MqttDevice` — Task 6 Step 1'de tanımlanıyor, Task 6 Step 2'deki `KanitTopla_*` içinde kullanılıyor. ✓
