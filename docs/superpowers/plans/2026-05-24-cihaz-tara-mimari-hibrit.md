# Cihaz Tara Mimari Hibrit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cihaz Tara'yi referans mimari haline getirip tarama session, presenter, classification ve ortak tool state modellerini davranis regresyonu olmadan ayirmak.

**Architecture:** Kademeli hibrit uygulanir: once Cihaz Tara icin UI bagimsiz classification, presenter ve session katmanlari eklenir; sonra Ping/Port/DNS/Trace/Console icin minimal ortak tool modelleri hazirlanir. MVVM'e gecilmez; `MainWindow` thin code-behind olarak kalir.

**Tech Stack:** .NET 10 WPF, C# nullable enabled, xUnit 2.9.2, mevcut `Services/Discovery` ve `Partials/MainWindow.*` yapisi.

---

## Scope Check

Spec genis kapsamli olsa da tek plan olarak tutulabilir, cunku ilk calisan yazilim ciktisi Cihaz Tara merkezlidir. Ping, Port, DNS, Trace ve Console bu planda bastan yazilmaz; sadece ortak modeller eklenir ve bir kucuk entegrasyonla yol acilir.

Proje kurali: AI otomatik commit atmaz. Bu nedenle her gorev sonunda `git status --short` checkpoint'i vardir; commit kullanici tarafindan manuel yapilir.

## File Structure

Create:

- `Services/Discovery/Classification/DeviceClassification.cs`  
  UI bagimsiz classification sonucu.
- `Services/Discovery/Classification/DeviceClassificationService.cs`  
  `MainWindow.DeviceClassifier.cs` icindeki kanit tabanli karar mantiginin yeni evi.
- `Services/Discovery/Presentation/DeviceScanRow.cs`  
  Test edilebilir, WPF bagimsiz cihaz satiri DTO'su.
- `Services/Discovery/Presentation/DeviceScanFilters.cs`  
  Grid filtre girdilerinin UI bagimsiz modeli.
- `Services/Discovery/Presentation/DeviceScanPresenter.cs`  
  `DeviceInfo` -> `DeviceScanRow`, filtre ve sayac metni uretimi.
- `Services/Discovery/DeviceScanSessionOptions.cs`  
  UI'dan session'a giden temiz tarama secenekleri.
- `Services/Discovery/DeviceScanSessionEvent.cs`  
  Progress, diagnostic, device changed, completed, failed ve canceled event modelleri.
- `Services/Discovery/DeviceScanResult.cs`  
  Session final sonucu.
- `Services/Discovery/DeviceScanSession.cs`  
  Engine yasam dongusu orkestrasyonu.
- `Services/Tools/ToolRunState.cs`  
  Ortak arac calisma durumlari ve state transition yardimcilari.
- `Services/Tools/ToolOutput.cs`  
  Ortak arac cikti modeli.
- `..\AgTarama.Tests\DeviceClassificationServiceTests.cs`
- `..\AgTarama.Tests\DeviceScanPresenterTests.cs`
- `..\AgTarama.Tests\DeviceScanSessionTests.cs`
- `..\AgTarama.Tests\ToolRunStateTests.cs`

Modify:

- `Partials/MainWindow.DeviceScan.cs`  
  Session ve presenter kullanacak; tarama orkestrasyonu azalacak.
- `Partials/MainWindow.DeviceScan.Row.cs`  
  `KameraSatirOlustur`, filtre ve guven skoru presenter'a delege edilecek.
- `Partials/MainWindow.DeviceClassifier.cs`  
  Once wrapper haline getirilecek, sonra bosaltilacak veya silinecek.
- `Services/Discovery/IDeviceDiscoveryEngine.cs`  
  Test edilebilir session icin gerekirse `DeviceChanged` event ownership degil, mevcut `Store.DeviceChanged` kullanimi korunacak.
- `docs/superpowers/specs/2026-05-24-cihaz-tara-mimari-hibrit-design.md`  
  Uygulama sirasinda spec degistirilmez; yalnizca kullanici isterse guncellenir.

---

### Task 1: Classification Contract ve Failing Testler

**Files:**
- Create: `..\AgTarama.Tests\DeviceClassificationServiceTests.cs`
- Create later in Task 2: `Services/Discovery/Classification/DeviceClassification.cs`
- Create later in Task 2: `Services/Discovery/Classification/DeviceClassificationService.cs`

- [ ] **Step 1: Failing classification test dosyasini ekle**

Create `..\AgTarama.Tests\DeviceClassificationServiceTests.cs`:

```csharp
using AgTarama.Services.Discovery.Classification;
using AgTarama.Services.Discovery.Models;
using Xunit;

namespace AgTarama.Tests;

public sealed class DeviceClassificationServiceTests
{
    [Fact]
    public void Classify_GatewayIp_RouterApOlur()
    {
        var device = new DeviceInfo { Ip = "192.168.1.1", IsGateway = true };

        var result = DeviceClassificationService.Classify(device);

        Assert.Equal("Router/AP", result.Tur);
        Assert.True(result.Confidence >= 50);
        Assert.NotNull(device.KararIzi);
    }

    [Fact]
    public void Classify_MikroTikIdentity_MarkaVeTurDogru()
    {
        var device = new DeviceInfo
        {
            Ip = "192.168.1.1",
            MikroTikIdentity = "office-router",
            MikroTikBoard = "RB952Ui-5ac2nD"
        };

        var result = DeviceClassificationService.Classify(device);

        Assert.Equal("MikroTik", result.Marka);
        Assert.Equal("Router/AP", result.Tur);
        Assert.True(result.Confidence >= 60);
    }

    [Fact]
    public void Classify_RtspServer_KameraOlur()
    {
        var device = new DeviceInfo
        {
            Ip = "192.168.1.64",
            RtspServerHeader = "Hikvision-Webs"
        };

        var result = DeviceClassificationService.Classify(device);

        Assert.Equal("Hikvision", result.Marka);
        Assert.Equal("Kamera", result.Tur);
        Assert.True(result.Confidence >= 35);
    }

    [Fact]
    public void NormalizeBrand_Routerboard_MikrotikOlur()
    {
        Assert.Equal("MikroTik", DeviceClassificationService.NormalizeBrand("RouterBOARD"));
        Assert.Equal("MikroTik", DeviceClassificationService.NormalizeBrand("MikroTikls SIA"));
    }
}
```

- [ ] **Step 2: Failing testleri calistir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter DeviceClassificationServiceTests
```

Expected: FAIL. `DeviceClassificationService` and/or `DeviceClassification` type is not defined.

- [ ] **Step 3: Manual checkpoint**

Run:

```powershell
git status --short
```

Expected: Only the new test file and pre-existing unrelated dirty files are listed. Do not commit unless the user explicitly requests it.

---

### Task 2: DeviceClassificationService'i UI'dan Ayir

**Files:**
- Create: `Services/Discovery/Classification/DeviceClassification.cs`
- Create: `Services/Discovery/Classification/DeviceClassificationService.cs`
- Modify: `Partials/MainWindow.DeviceScan.cs`
- Modify: `Partials/MainWindow.DeviceClassifier.cs`
- Test: `..\AgTarama.Tests\DeviceClassificationServiceTests.cs`

- [ ] **Step 1: Classification result modelini ekle**

Create `Services/Discovery/Classification/DeviceClassification.cs`:

```csharp
namespace AgTarama.Services.Discovery.Classification;

internal sealed record DeviceClassification(
    string Marka,
    string? Model,
    string Tur,
    string TurIkon,
    int Confidence);
```

- [ ] **Step 2: Classification service dosyasini olustur**

Create `Services/Discovery/Classification/DeviceClassificationService.cs` with this shell first:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Classification;

internal static class DeviceClassificationService
{
    public static string NormalizeBrand(string marka)
    {
        var m = marka.Trim();
        if (string.IsNullOrWhiteSpace(m)) return "";

        var lower = m.ToLowerInvariant();
        if (lower.Contains("routerboard") || lower.Contains("mikrotikls") || lower.Contains("mikrotik")) return "MikroTik";
        if (lower.Contains("hikvision")) return "Hikvision";
        if (lower.Contains("dahua")) return "Dahua";
        if (lower.Contains("axis")) return "Axis";
        if (lower.Contains("ubiquiti") || lower.Contains("ubnt")) return "Ubiquiti";
        if (lower.Contains("tp-link") || lower.Contains("tplink")) return "TP-Link";
        if (lower.Contains("d-link") || lower.Contains("dlink")) return "D-Link";
        if (lower.Contains("netgear")) return "NETGEAR";
        if (lower.Contains("asus")) return "ASUS";
        if (lower.Contains("cisco")) return "Cisco";
        if (lower.Contains("aruba")) return "Aruba";
        if (lower.Contains("hp") || lower.Contains("hewlett")) return "HP";
        if (lower.Contains("epson")) return "Epson";
        if (lower.Contains("brother")) return "Brother";
        if (lower.Contains("canon")) return "Canon";
        if (lower.Contains("kyocera")) return "Kyocera";
        if (lower.Contains("xerox")) return "Xerox";
        return m;
    }

    public static DeviceClassification Classify(DeviceInfo b)
    {
        var legacy = ClassifyCore(b);
        var confidence = CalculateConfidence(b, legacy.Marka, legacy.Tur);
        return new DeviceClassification(legacy.Marka, legacy.Model, legacy.Tur, legacy.TurIkon, confidence);
    }

    private sealed record ClassificationCore(string Marka, string? Model, string Tur, string TurIkon);

    private static ClassificationCore ClassifyCore(DeviceInfo b)
    {
        // Move the complete body of current MainWindow.KimlikBelirleV2(DeviceInfo b) here.
        // Replace `new CihazKimlik { Marka = x, Model = y, Tur = z, TurIkon = i }`
        // with `new ClassificationCore(x, y, z, i)`.
        // Keep every KanitTopla_* method unchanged except for references to MarkaNormalize:
        // rename calls from MarkaNormalize(...) to NormalizeBrand(...).
        throw new NotImplementedException("Move MainWindow.KimlikBelirleV2 body here in this task.");
    }

    private static int CalculateConfidence(DeviceInfo b, string marka, string tur)
    {
        if (b.KararIzi is { TurSiralama: { Count: > 0 } } iz)
        {
            var turSkor = iz.TurSiralama.FirstOrDefault(x => string.Equals(x.Tur, tur, StringComparison.OrdinalIgnoreCase)).Skor;
            var markaBonus = iz.MarkaSiralama.Any(x => string.Equals(x.Marka, marka, StringComparison.OrdinalIgnoreCase)) ? 10 : 0;
            return Math.Clamp(turSkor + markaBonus, 0, 100);
        }

        var score = 0;
        if (b.OnvifBulundu) score += 25;
        if (b.SsdpBulundu) score += 12;
        if (!string.IsNullOrWhiteSpace(b.HttpFpMarka) || !string.IsNullOrWhiteSpace(b.HttpFpTur)) score += 30;
        if (!string.IsNullOrWhiteSpace(b.SnmpSysDescr)) score += 25;
        if (!string.IsNullOrWhiteSpace(b.MacAdresi)) score += 10;
        if (b.AcikPortlar.Count > 0) score += 10;
        if (b.PingYanit) score += 8;
        return Math.Clamp(score, 0, 100);
    }
}
```

- [ ] **Step 3: Mevcut classifier govdesini mekanik olarak tasi**

Open `Partials/MainWindow.DeviceClassifier.cs`.

Move these members into `DeviceClassificationService`:

```text
MarkaNormalize
KimlikBelirleV2
KararIziOzetle dependencies that are classification-only
All KanitTopla_* methods
All private regex/signature helper methods used only by KanitTopla_* methods
```

Mechanical edits:

```text
MarkaNormalize(...) -> NormalizeBrand(...)
CihazKimlik -> ClassificationCore inside service
new CihazKimlik { Marka = marka, Model = model, Tur = tur, TurIkon = ikon }
    -> new ClassificationCore(marka, model, tur, ikon)
```

Do not move UI methods such as `KameraSatirOlustur`, `KameraFiltreleriUygula`, or `KameraWebUrlSec` in this task.

- [ ] **Step 4: MainWindow wrapper'ini servis kullanacak hale getir**

In `Partials/MainWindow.DeviceScan.cs`, keep the existing nested `CihazKimlik` temporarily and replace `KimlikBelirle` plus `GuvenSkoru` with:

```csharp
private static CihazKimlik KimlikBelirle(DeviceInfo b)
{
    var result = AgTarama.Services.Discovery.Classification.DeviceClassificationService.Classify(b);
    return new CihazKimlik
    {
        Marka = result.Marka,
        Model = result.Model,
        Tur = result.Tur,
        TurIkon = result.TurIkon,
        Guven = result.Confidence,
    };
}

private static int GuvenSkoru(DeviceInfo b, CihazKimlik k)
    => k.Guven;
```

Add `Guven` property to `CihazKimlik`:

```csharp
public int Guven { get; set; }
```

- [ ] **Step 5: Eski partial'i wrapper veya bos dosya haline getir**

After all moved methods compile from the service, reduce `Partials/MainWindow.DeviceClassifier.cs` to:

```csharp
namespace AgTarama;

public partial class MainWindow
{
}
```

If the project file includes all `*.cs` by SDK default, this empty partial is safe. Deleting the file is also acceptable after confirming no docs or references depend on the filename.

- [ ] **Step 6: Classification testleri yesile getir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter DeviceClassificationServiceTests
```

Expected: PASS.

- [ ] **Step 7: Full build calistir**

Run:

```powershell
dotnet build ..\AgTarama.slnx
```

Expected: 0 errors.

- [ ] **Step 8: Manual checkpoint**

Run:

```powershell
git status --short
```

Expected: classification service files, tests, and modified partials are listed. Do not commit unless the user explicitly requests it.

---

### Task 3: DeviceScanPresenter Contract ve Failing Testler

**Files:**
- Create: `..\AgTarama.Tests\DeviceScanPresenterTests.cs`
- Create later: `Services/Discovery/Presentation/DeviceScanRow.cs`
- Create later: `Services/Discovery/Presentation/DeviceScanFilters.cs`
- Create later: `Services/Discovery/Presentation/DeviceScanPresenter.cs`

- [ ] **Step 1: Presenter testlerini ekle**

Create `..\AgTarama.Tests\DeviceScanPresenterTests.cs`:

```csharp
using AgTarama.Services.Discovery.Models;
using AgTarama.Services.Discovery.Presentation;
using Xunit;

namespace AgTarama.Tests;

public sealed class DeviceScanPresenterTests
{
    [Fact]
    public void BuildRow_DeviceInfo_DeterministikSatirUretir()
    {
        var device = new DeviceInfo
        {
            Ip = "192.168.1.10",
            Online = true,
            PingYanit = true,
            PingMs = 12,
            MacAdresi = "AA:BB:CC:DD:EE:FF",
            Uretici = "Hikvision",
            HttpFpMarka = "Hikvision",
            HttpFpTur = "Kamera",
            HttpFpModel = "DS-2CD",
            LastSeen = new DateTime(2026, 5, 24, 14, 30, 0)
        };
        device.AcikPortlar.Add(80);
        device.AcikPortlar.Add(554);
        device.ServisDetaylari[80] = "HTTP";
        device.ServisDetaylari[554] = "RTSP";
        device.KesifKaynaklari.Add("HTTP-FP");
        device.KesifKaynaklari.Add("PORT");

        var row = DeviceScanPresenter.BuildRow(device, new DateTime(2026, 5, 24));

        Assert.Equal("192.168.1.10", row.Ip);
        Assert.Equal("Kamera", row.Tur);
        Assert.Equal("Hikvision", row.Marka);
        Assert.Equal("DS-2CD", row.Model);
        Assert.Equal("Online", row.Durum);
        Assert.Equal("12 ms", row.Ping);
        Assert.Equal("80, 554", row.Portlar);
        Assert.Contains("HTTP-FP", row.Kesif);
        Assert.Contains("80/HTTP", row.Servis);
        Assert.Equal("http://192.168.1.10/", row.WebUrl);
        Assert.True(row.Guven >= 35);
    }

    [Fact]
    public void MatchesFilter_DusukGuvenOffline_Gizlenir()
    {
        var row = new DeviceScanRow
        {
            Ip = "192.168.1.20",
            Tur = "Cihaz",
            Online = false,
            Guven = 5
        };
        var filters = new DeviceScanFilters(ShowLowConfidence: false);

        Assert.False(DeviceScanPresenter.MatchesFilter(row, filters));
    }

    [Fact]
    public void CounterText_GorunenVeToplamYazar()
    {
        Assert.Equal("0 cihaz", DeviceScanPresenter.CounterText(0, 0));
        Assert.Equal("3/10 cihaz", DeviceScanPresenter.CounterText(10, 3));
    }
}
```

- [ ] **Step 2: Failing presenter testlerini calistir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter DeviceScanPresenterTests
```

Expected: FAIL. `DeviceScanPresenter`, `DeviceScanRow`, and `DeviceScanFilters` are not defined.

---

### Task 4: DeviceScanPresenter'i Ekle

**Files:**
- Create: `Services/Discovery/Presentation/DeviceScanRow.cs`
- Create: `Services/Discovery/Presentation/DeviceScanFilters.cs`
- Create: `Services/Discovery/Presentation/DeviceScanPresenter.cs`
- Test: `..\AgTarama.Tests\DeviceScanPresenterTests.cs`

- [ ] **Step 1: DeviceScanRow ekle**

Create `Services/Discovery/Presentation/DeviceScanRow.cs`:

```csharp
namespace AgTarama.Services.Discovery.Presentation;

internal sealed class DeviceScanRow
{
    public string  Ip         { get; set; } = "";
    public string  Ad         { get; set; } = "";
    public string  Tur        { get; set; } = "";
    public string  Marka      { get; set; } = "";
    public string  Model      { get; set; } = "";
    public string  Os         { get; set; } = "";
    public string  Durum      { get; set; } = "";
    public string  SonGorulen { get; set; } = "";
    public bool    Online     { get; set; }
    public string  Ping       { get; set; } = "";
    public int     PingMs     { get; set; } = int.MaxValue;
    public string  Portlar    { get; set; } = "";
    public string  Kesif      { get; set; } = "";
    public string  Mac        { get; set; } = "";
    public string  Uretici    { get; set; } = "";
    public string  Servis     { get; set; } = "";
    public string? WebUrl     { get; set; }
    public int     Guven      { get; set; }
    public string  KararIzi   { get; set; } = "";
}
```

- [ ] **Step 2: DeviceScanFilters ekle**

Create `Services/Discovery/Presentation/DeviceScanFilters.cs`:

```csharp
namespace AgTarama.Services.Discovery.Presentation;

internal sealed record DeviceScanFilters(
    string? Ip = null,
    string? NameOrModel = null,
    string? BrandOrVendor = null,
    string? PortServiceOrDiscovery = null,
    string? Mac = null,
    string Type = "Hepsi",
    bool ShowLowConfidence = false);
```

- [ ] **Step 3: DeviceScanPresenter ekle**

Create `Services/Discovery/Presentation/DeviceScanPresenter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AgTarama.Services.Discovery.Classification;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Presentation;

internal static class DeviceScanPresenter
{
    public static DeviceScanRow BuildRow(DeviceInfo device, DateTime today)
    {
        var classification = DeviceClassificationService.Classify(device);
        var deviceName = PickDeviceName(device) ?? "";

        List<int> ports;
        lock (device.AcikPortlar) ports = device.AcikPortlar.Order().ToList();

        List<string> services;
        lock (device.ServisDetaylari)
            services = device.ServisDetaylari.OrderBy(x => x.Key).Select(x => $"{x.Key}/{x.Value}").ToList();

        var discovery = new HashSet<string>(device.KesifKaynaklari, StringComparer.OrdinalIgnoreCase);
        if (device.OnvifBulundu) discovery.Add("ONVIF");
        if (device.SsdpBulundu) discovery.Add("UPnP");
        var discoveryText = string.Join(", ", discovery.OrderBy(DiscoveryOrder));

        return new DeviceScanRow
        {
            Ip = device.Ip,
            Ad = deviceName,
            Tur = classification.Tur,
            Marka = classification.Marka == "Bilinmiyor" ? "" : classification.Marka,
            Model = classification.Model ?? "",
            Os = device.Os ?? "",
            Durum = device.Online ? "Online" : "Offline",
            SonGorulen = FormatLastSeen(device.LastSeen, today),
            Online = device.Online,
            Ping = device.PingYanit ? $"{device.PingMs} ms" : "",
            PingMs = device.PingYanit ? device.PingMs : int.MaxValue,
            Portlar = string.Join(", ", ports),
            Kesif = discoveryText,
            Mac = device.MacAdresi ?? "",
            Uretici = device.Uretici ?? "",
            Servis = string.Join(" | ", services.DefaultIfEmpty(FirstNonEmpty(device.SunucuBasligi, device.SayfaBasligi, device.RtspDurum) ?? "")),
            WebUrl = SelectWebUrl(device, ports),
            Guven = classification.Confidence,
            KararIzi = DecisionTraceSummary(device.KararIzi),
        };
    }

    public static bool MatchesFilter(DeviceScanRow row, DeviceScanFilters filters)
    {
        if (!filters.ShowLowConfidence && row.Guven < 12 && !row.Online) return false;

        if (string.Equals(filters.Type, "Bilinmiyor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(row.Tur, "Cihaz", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(row.Tur))
                return false;
        }
        else if (!string.Equals(filters.Type, "Hepsi", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(row.Tur, filters.Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Contains(row.Ip, filters.Ip) &&
               Contains($"{row.Ad} {row.Model}", filters.NameOrModel) &&
               Contains($"{row.Marka} {row.Uretici}", filters.BrandOrVendor) &&
               Contains($"{row.Portlar} {row.Servis} {row.Kesif}", filters.PortServiceOrDiscovery) &&
               Contains(row.Mac, filters.Mac);
    }

    public static string CounterText(int total, int visible)
        => total == 0 ? "0 cihaz" : $"{visible}/{total} cihaz";

    public static int DiscoveryOrder(string source) => source.ToUpperInvariant() switch
    {
        "UBIQUITI" => 0,
        "MNDP"     => 1,
        "ONVIF"    => 2,
        "WSD"      => 3,
        "UPNP"     => 4,
        "SSDP"     => 4,
        "MDNS"     => 5,
        "SNMP"     => 6,
        "HTTP-FP"  => 7,
        "NETBIOS"  => 8,
        "SMB"      => 8,
        "LLMNR"    => 9,
        "SSH"      => 9,
        "PORT"     => 10,
        "PING"     => 11,
        "ARP"      => 12,
        _          => 99,
    };

    public static string? SelectWebUrl(DeviceInfo device, IReadOnlyCollection<int>? ports = null)
    {
        List<int> openPorts;
        if (ports is null)
        {
            lock (device.AcikPortlar) openPorts = [..device.AcikPortlar];
        }
        else
        {
            openPorts = ports.ToList();
        }

        foreach (var (port, scheme) in new (int, string)[] { (80, "http"), (443, "https"), (8080, "http"), (8443, "https"), (9000, "http") })
        {
            if (!openPorts.Contains(port)) continue;
            return port is 80 or 443 ? $"{scheme}://{device.Ip}/" : $"{scheme}://{device.Ip}:{port}/";
        }
        return null;
    }

    private static string? PickDeviceName(DeviceInfo device)
        => FirstNonEmpty(device.DnsAdi, device.PingAdi, device.LlmnrHostname, device.DhcpHostname,
            device.UbntHostname, device.MikroTikIdentity, device.OnvifAdi, device.SsdpFriendlyName,
            device.NetbiosCihazAdi, device.SnmpSysName);

    private static string FormatLastSeen(DateTime lastSeen, DateTime today)
        => lastSeen == default ? "" :
           lastSeen.Date == today.Date ? lastSeen.ToString("HH:mm:ss") :
           lastSeen.ToString("dd.MM HH:mm");

    private static bool Contains(string? source, string? filter)
        => string.IsNullOrWhiteSpace(filter) ||
           (source?.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string DecisionTraceSummary(KimlikKararIzi? trace)
    {
        if (trace is null) return "";
        var topType = trace.TurSiralama.FirstOrDefault();
        var topBrand = trace.MarkaSiralama.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(topType.Tur) && string.IsNullOrWhiteSpace(topBrand.Marka)) return "";
        if (string.IsNullOrWhiteSpace(topBrand.Marka)) return $"{topType.Tur}:{topType.Skor}";
        if (string.IsNullOrWhiteSpace(topType.Tur)) return $"{topBrand.Marka}:{topBrand.Skor}";
        return $"{topType.Tur}:{topType.Skor} | {topBrand.Marka}:{topBrand.Skor}";
    }
}
```

- [ ] **Step 4: Presenter testlerini yesile getir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter DeviceScanPresenterTests
```

Expected: PASS.

- [ ] **Step 5: Full build calistir**

Run:

```powershell
dotnet build ..\AgTarama.slnx
```

Expected: 0 errors.

---

### Task 5: MainWindow Row ve Filter Kodunu Presenter'a Delege Et

**Files:**
- Modify: `Partials/MainWindow.DeviceScan.Row.cs`
- Modify: `Partials/MainWindow.DeviceScan.cs`
- Test: `..\AgTarama.Tests\DeviceScanPresenterTests.cs`

- [ ] **Step 1: Namespace import ekle**

At the top of `Partials/MainWindow.DeviceScan.Row.cs`, add:

```csharp
using AgTarama.Services.Discovery.Presentation;
```

- [ ] **Step 2: KameraSatirOlustur'u presenter kullanacak hale getir**

Replace the body of `KameraSatirOlustur(DeviceInfo bilgi)` with:

```csharp
private KameraSatir KameraSatirOlustur(DeviceInfo bilgi)
{
    var row = DeviceScanPresenter.BuildRow(bilgi, DateTime.Today);
    return new KameraSatir
    {
        Ip = row.Ip,
        Ad = row.Ad,
        Tur = row.Tur,
        Marka = row.Marka,
        Model = row.Model,
        Os = row.Os,
        Durum = row.Durum,
        SonGorulen = row.SonGorulen,
        Online = row.Online,
        Ping = row.Ping,
        PingMs = row.PingMs,
        Portlar = row.Portlar,
        Kesif = row.Kesif,
        Mac = row.Mac,
        Uretici = row.Uretici,
        Servis = row.Servis,
        WebUrl = row.WebUrl,
        Guven = row.Guven,
        KararIzi = row.KararIzi,
    };
}
```

- [ ] **Step 3: KameraFiltreleriUygula sayacini presenter'a delege et**

Replace the body of `KameraFiltreleriUygula()` with:

```csharp
private void KameraFiltreleriUygula()
{
    _kameraSatirView?.Refresh();
    int toplam = _kameraSatirlari.Count;
    int gorunen = _kameraSatirView?.Cast<object>().Count() ?? toplam;
    KameraFiltreSayacText.Text = DeviceScanPresenter.CounterText(toplam, gorunen);
}
```

- [ ] **Step 4: KameraSatirFiltredenGecer'i presenter'a delege et**

Replace the body of `KameraSatirFiltredenGecer(object obj)` with:

```csharp
private bool KameraSatirFiltredenGecer(object obj)
{
    if (obj is not KameraSatir satir) return false;

    var tur = (KameraTurFiltreBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Hepsi";
    var filters = new DeviceScanFilters(
        Ip: KameraIpFiltreBox?.Text,
        NameOrModel: KameraAdFiltreBox?.Text,
        BrandOrVendor: KameraMarkaFiltreBox?.Text,
        PortServiceOrDiscovery: KameraPortFiltreBox?.Text,
        Mac: KameraMacFiltreBox?.Text,
        Type: tur,
        ShowLowConfidence: _dusukGuvenGoster);

    var row = new DeviceScanRow
    {
        Ip = satir.Ip,
        Ad = satir.Ad,
        Tur = satir.Tur,
        Marka = satir.Marka,
        Model = satir.Model,
        Online = satir.Online,
        Portlar = satir.Portlar,
        Kesif = satir.Kesif,
        Mac = satir.Mac,
        Uretici = satir.Uretici,
        Servis = satir.Servis,
        Guven = satir.Guven,
    };

    return DeviceScanPresenter.MatchesFilter(row, filters);
}
```

- [ ] **Step 5: Artik kullanilmayan helper'lari temizle**

In `Partials/MainWindow.DeviceScan.Row.cs`, remove these methods if no references remain:

```text
KesifSira
KameraWebUrlSec
Icerir
```

Before removing each method, run:

```powershell
rg "KesifSira|KameraWebUrlSec|Icerir" Partials Services
```

Expected: Only the soon-to-delete definitions remain, or references have moved to `DeviceScanPresenter`.

- [ ] **Step 6: Tests and build**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter "DeviceScanPresenterTests|DeviceClassificationServiceTests"
dotnet build ..\AgTarama.slnx
```

Expected: tests PASS, build 0 errors.

---

### Task 6: DeviceScanSession Contract ve Failing Testler

**Files:**
- Create: `..\AgTarama.Tests\DeviceScanSessionTests.cs`
- Create later: `Services/Discovery/DeviceScanSessionOptions.cs`
- Create later: `Services/Discovery/DeviceScanSessionEvent.cs`
- Create later: `Services/Discovery/DeviceScanResult.cs`
- Create later: `Services/Discovery/DeviceScanSession.cs`

- [ ] **Step 1: Session testlerini ekle**

Create `..\AgTarama.Tests\DeviceScanSessionTests.cs`:

```csharp
using AgTarama.Services.Discovery;
using AgTarama.Services.Discovery.Models;
using Xunit;

namespace AgTarama.Tests;

public sealed class DeviceScanSessionTests
{
    [Fact]
    public async Task RunAsync_BasariliScan_CompletedResultDoner()
    {
        var engine = new FakeDeviceDiscoveryEngine();
        engine.Store.GetOrAdd("192.168.1.10").Online = true;
        var session = new DeviceScanSession(engine);
        var events = new List<DeviceScanSessionEvent>();
        session.EventRaised += (_, e) => events.Add(e);

        var options = new DeviceScanSessionOptions(
            Subnets: [("192.168.1", 1, 10)],
            ScanOptions: new ScanOptions { DeepScan = false, LiveMode = false });

        var result = await session.RunAsync(options, CancellationToken.None);

        Assert.False(result.Canceled);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.Devices);
        Assert.Contains(events, e => e.Kind == DeviceScanSessionEventKind.Completed);
    }

    [Fact]
    public async Task RunAsync_IptalEdilirse_CanceledResultDoner()
    {
        var engine = new FakeDeviceDiscoveryEngine { ThrowCanceled = true };
        var session = new DeviceScanSession(engine);
        var events = new List<DeviceScanSessionEvent>();
        session.EventRaised += (_, e) => events.Add(e);

        var options = new DeviceScanSessionOptions(
            Subnets: [("192.168.1", 1, 10)],
            ScanOptions: new ScanOptions());

        var result = await session.RunAsync(options, CancellationToken.None);

        Assert.True(result.Canceled);
        Assert.Contains(events, e => e.Kind == DeviceScanSessionEventKind.Canceled);
    }

    [Fact]
    public async Task RunAsync_HataOlursa_FailedResultDoner()
    {
        var engine = new FakeDeviceDiscoveryEngine { ThrowFailure = true };
        var session = new DeviceScanSession(engine);

        var options = new DeviceScanSessionOptions(
            Subnets: [("192.168.1", 1, 10)],
            ScanOptions: new ScanOptions());

        var result = await session.RunAsync(options, CancellationToken.None);

        Assert.False(result.Canceled);
        Assert.Equal("engine failed", result.ErrorMessage);
    }

    private sealed class FakeDeviceDiscoveryEngine : IDeviceDiscoveryEngine
    {
        public DeviceStore Store { get; } = new();
        public bool NpcapAvailable => false;
        public bool ThrowCanceled { get; init; }
        public bool ThrowFailure { get; init; }

        public Task StartScanAsync(
            IReadOnlyList<(string Prefix, int Start, int End)> subnets,
            ScanOptions options,
            IProgress<ScanProgress>? progress,
            CancellationToken token)
        {
            if (ThrowCanceled) throw new OperationCanceledException(token);
            if (ThrowFailure) throw new InvalidOperationException("engine failed");
            progress?.Report(new ScanProgress(1, 2, Store.Count, "fake progress", 0, "fake detail"));
            return Task.CompletedTask;
        }

        public Task StartLiveAsync(
            IReadOnlyList<(string Prefix, int Start, int End)> subnets,
            ScanOptions options,
            CancellationToken token)
        {
            return StartScanAsync(subnets, options, null, token);
        }
    }
}
```

- [ ] **Step 2: Failing session testlerini calistir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter DeviceScanSessionTests
```

Expected: FAIL. Session types are not defined.

---

### Task 7: DeviceScanSession'i Ekle

**Files:**
- Create: `Services/Discovery/DeviceScanSessionOptions.cs`
- Create: `Services/Discovery/DeviceScanSessionEvent.cs`
- Create: `Services/Discovery/DeviceScanResult.cs`
- Create: `Services/Discovery/DeviceScanSession.cs`
- Test: `..\AgTarama.Tests\DeviceScanSessionTests.cs`

- [ ] **Step 1: DeviceScanSessionOptions ekle**

Create `Services/Discovery/DeviceScanSessionOptions.cs`:

```csharp
using System.Collections.Generic;

namespace AgTarama.Services.Discovery;

internal sealed record DeviceScanSessionOptions(
    IReadOnlyList<(string Prefix, int Start, int End)> Subnets,
    ScanOptions ScanOptions);
```

- [ ] **Step 2: DeviceScanSessionEvent ekle**

Create `Services/Discovery/DeviceScanSessionEvent.cs`:

```csharp
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery;

internal enum DeviceScanSessionEventKind
{
    Progress,
    Diagnostic,
    DeviceChanged,
    Completed,
    Failed,
    Canceled,
}

internal sealed record DeviceScanSessionEvent(
    DeviceScanSessionEventKind Kind,
    ScanProgress? Progress = null,
    DeviceInfo? Device = null,
    string? Message = null);
```

- [ ] **Step 3: DeviceScanResult ekle**

Create `Services/Discovery/DeviceScanResult.cs`:

```csharp
using System;
using System.Collections.Generic;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery;

internal sealed record DeviceScanResult(
    IReadOnlyList<DeviceInfo> Devices,
    TimeSpan Duration,
    bool Canceled,
    string? ErrorMessage);
```

- [ ] **Step 4: DeviceScanSession ekle**

Create `Services/Discovery/DeviceScanSession.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery;

internal sealed class DeviceScanSession
{
    private readonly IDeviceDiscoveryEngine _engine;

    public DeviceScanSession(IDeviceDiscoveryEngine engine)
    {
        _engine = engine;
    }

    public event EventHandler<DeviceScanSessionEvent>? EventRaised;

    public async Task<DeviceScanResult> RunAsync(DeviceScanSessionOptions options, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();

        void OnDeviceChanged(object? sender, DeviceInfo device)
            => EventRaised?.Invoke(this, new DeviceScanSessionEvent(DeviceScanSessionEventKind.DeviceChanged, Device: device));

        var progress = new Progress<ScanProgress>(p =>
        {
            EventRaised?.Invoke(this, new DeviceScanSessionEvent(DeviceScanSessionEventKind.Progress, Progress: p));
            if (!string.IsNullOrWhiteSpace(p.Detay))
            {
                EventRaised?.Invoke(this, new DeviceScanSessionEvent(DeviceScanSessionEventKind.Diagnostic, Progress: p, Message: p.Detay));
            }
        });

        _engine.Store.DeviceChanged += OnDeviceChanged;
        try
        {
            if (options.ScanOptions.LiveMode)
            {
                await _engine.StartLiveAsync(options.Subnets, options.ScanOptions, token).ConfigureAwait(false);
            }
            else
            {
                await _engine.StartScanAsync(options.Subnets, options.ScanOptions, progress, token).ConfigureAwait(false);
            }

            sw.Stop();
            var result = new DeviceScanResult(_engine.Store.All, sw.Elapsed, Canceled: false, ErrorMessage: null);
            EventRaised?.Invoke(this, new DeviceScanSessionEvent(DeviceScanSessionEventKind.Completed, Message: "Tamamlandi"));
            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            var result = new DeviceScanResult(_engine.Store.All, sw.Elapsed, Canceled: true, ErrorMessage: null);
            EventRaised?.Invoke(this, new DeviceScanSessionEvent(DeviceScanSessionEventKind.Canceled, Message: "Iptal edildi"));
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var result = new DeviceScanResult(_engine.Store.All, sw.Elapsed, Canceled: false, ErrorMessage: ex.Message);
            EventRaised?.Invoke(this, new DeviceScanSessionEvent(DeviceScanSessionEventKind.Failed, Message: ex.Message));
            return result;
        }
        finally
        {
            _engine.Store.DeviceChanged -= OnDeviceChanged;
        }
    }
}
```

- [ ] **Step 5: Session testlerini yesile getir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter DeviceScanSessionTests
```

Expected: PASS.

- [ ] **Step 6: Full build calistir**

Run:

```powershell
dotnet build ..\AgTarama.slnx
```

Expected: 0 errors.

---

### Task 8: MainWindow Cihaz Tara Akisini DeviceScanSession'a Bagla

**Files:**
- Modify: `Partials/MainWindow.DeviceScan.cs`
- Test: `..\AgTarama.Tests\DeviceScanSessionTests.cs`

- [ ] **Step 1: DeviceScanSession field'i ekle**

In `Partials/MainWindow.DeviceScan.cs`, near the existing `_engine` field, add:

```csharp
private DeviceScanSession? _deviceScanSession;
```

If the file does not already import the namespace, add:

```csharp
using AgTarama.Services.Discovery;
```

- [ ] **Step 2: KameraTaramaBaslat icinde session olustur**

Inside `KameraTaramaBaslat()`, after subnet parsing and `ScanOptions` creation, replace direct `_engine.StartLiveAsync` / `_engine.StartScanAsync` orchestration with:

```csharp
_deviceScanSession = new DeviceScanSession(_engine);
_deviceScanSession.EventRaised += DeviceScanSession_EventRaised;

var sessionOptions = new DeviceScanSessionOptions(
    subnets.Select(s => (s.Prefix, s.HostStart, s.HostEnd)).ToList(),
    options);

var result = await _deviceScanSession.RunAsync(sessionOptions, _kameraCts.Token);
if (result.Canceled)
{
    KameraIlerlemeText.Text = "Tarama iptal edildi.";
    return;
}

if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
{
    HataBildir("Cihaz tarama hatasi", new InvalidOperationException(result.ErrorMessage));
    return;
}

foreach (var dev in result.Devices)
{
    KameraKartEkleVeyaGuncelle(dev);
}

KameraAiBtn.IsEnabled = _kameraSatirlari.Count > 0;
```

Keep existing history save behavior in `MainWindow` after successful completion.

- [ ] **Step 3: Session event handler ekle**

Add this method to `Partials/MainWindow.DeviceScan.cs`:

```csharp
private void DeviceScanSession_EventRaised(object? sender, DeviceScanSessionEvent e)
{
    Dispatcher.BeginInvoke(() =>
    {
        switch (e.Kind)
        {
            case DeviceScanSessionEventKind.Progress when e.Progress is not null:
                KameraIlerlemeText.Text = e.Progress.AsamaMetni;
                KameraFiltreSayacText.Text = $"{e.Progress.BulunanCihaz} cihaz";
                break;
            case DeviceScanSessionEventKind.Diagnostic:
                if (!string.IsNullOrWhiteSpace(e.Message))
                    KameraKutucugaYaz(e.Message, "#58A6FF");
                break;
            case DeviceScanSessionEventKind.DeviceChanged when e.Device is not null:
                KameraKartEkleVeyaGuncelle(e.Device);
                break;
            case DeviceScanSessionEventKind.Canceled:
                KameraIlerlemeText.Text = "Tarama iptal edildi.";
                break;
            case DeviceScanSessionEventKind.Failed:
                KameraIlerlemeText.Text = e.Message ?? "Tarama hatasi.";
                break;
            case DeviceScanSessionEventKind.Completed:
                KameraIlerlemeText.Text = "Tarama tamamlandi.";
                break;
        }
    });
}
```

- [ ] **Step 4: Finally icinde event unsubscribe et**

In `KameraTaramaBaslat()` `finally` block, add:

```csharp
if (_deviceScanSession is not null)
{
    _deviceScanSession.EventRaised -= DeviceScanSession_EventRaised;
    _deviceScanSession = null;
}
```

- [ ] **Step 5: Eski duplicate event aboneligini kaldir**

Remove direct `_engine.Store.DeviceChanged += OnEngineDeviceChanged` and unsubscribe lines from `KameraTaramaBaslat()` if they now duplicate `DeviceScanSession`.

Keep `OnEngineDeviceChanged` only if another path still references it. Verify:

```powershell
rg "OnEngineDeviceChanged|DeviceChanged \\+=" Partials\\MainWindow.DeviceScan.cs
```

Expected: event subscription is owned by `DeviceScanSession`, not both session and UI.

- [ ] **Step 6: Build and focused tests**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter "DeviceScanSessionTests|DeviceScanPresenterTests|DeviceClassificationServiceTests"
dotnet build ..\AgTarama.slnx
```

Expected: tests PASS, build 0 errors.

---

### Task 9: Ortak ToolRunState ve ToolOutput Modellerini Ekle

**Files:**
- Create: `Services/Tools/ToolRunState.cs`
- Create: `Services/Tools/ToolOutput.cs`
- Create: `..\AgTarama.Tests\ToolRunStateTests.cs`

- [ ] **Step 1: Failing ToolRunState testlerini ekle**

Create `..\AgTarama.Tests\ToolRunStateTests.cs`:

```csharp
using AgTarama.Services.Tools;
using Xunit;

namespace AgTarama.Tests;

public sealed class ToolRunStateTests
{
    [Fact]
    public void Complete_RunningState_CompletedOlur()
    {
        var state = ToolRunState.Ready("Ping").Start().Complete();

        Assert.Equal(ToolRunStatus.Completed, state.Status);
        Assert.Equal("Ping", state.ToolName);
    }

    [Fact]
    public void Cancel_RunningState_CanceledOlur()
    {
        var state = ToolRunState.Ready("Port").Start().Canceling().Canceled();

        Assert.Equal(ToolRunStatus.Canceled, state.Status);
    }

    [Fact]
    public void Fail_RunningState_HataMesajiTutar()
    {
        var state = ToolRunState.Ready("DNS").Start().Fail("dns failed");

        Assert.Equal(ToolRunStatus.Failed, state.Status);
        Assert.Equal("dns failed", state.Message);
    }
}
```

- [ ] **Step 2: Tool modellerini ekle**

Create `Services/Tools/ToolRunState.cs`:

```csharp
namespace AgTarama.Services.Tools;

internal enum ToolRunStatus
{
    Ready,
    Running,
    Canceling,
    Canceled,
    Completed,
    Failed,
}

internal sealed record ToolRunState(string ToolName, ToolRunStatus Status, string? Message = null)
{
    public static ToolRunState Ready(string toolName)
        => new(toolName, ToolRunStatus.Ready);

    public ToolRunState Start()
        => this with { Status = ToolRunStatus.Running, Message = null };

    public ToolRunState Canceling()
        => this with { Status = ToolRunStatus.Canceling, Message = null };

    public ToolRunState Canceled()
        => this with { Status = ToolRunStatus.Canceled, Message = null };

    public ToolRunState Complete(string? message = null)
        => this with { Status = ToolRunStatus.Completed, Message = message };

    public ToolRunState Fail(string message)
        => this with { Status = ToolRunStatus.Failed, Message = message };
}
```

Create `Services/Tools/ToolOutput.cs`:

```csharp
namespace AgTarama.Services.Tools;

internal enum ToolOutputKind
{
    Chat,
    Panel,
    Diagnostic,
    Log,
}

internal enum ToolOutputSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

internal sealed record ToolOutput(
    ToolOutputKind Kind,
    ToolOutputSeverity Severity,
    string Text,
    string? Metadata = null);
```

- [ ] **Step 3: ToolRunState testlerini yesile getir**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter ToolRunStateTests
```

Expected: PASS.

---

### Task 10: Bir Aracta Ortak State Modelini Kucuk Entegrasyonla Kullan

**Files:**
- Modify: `Partials/MainWindow.Tools.Ping.cs`
- Test: `..\AgTarama.Tests\ToolRunStateTests.cs`

- [ ] **Step 1: Ping partial'a state field ekle**

In `Partials/MainWindow.Tools.Ping.cs`, add namespace:

```csharp
using AgTarama.Services.Tools;
```

Add a field to `MainWindow` partial if there is no existing Ping state field:

```csharp
private ToolRunState _pingRunState = ToolRunState.Ready("Ping");
```

If `MainWindow.xaml.cs` already has a suitable field area for `_pingCts`, place `_pingRunState` next to `_pingCts`.

- [ ] **Step 2: PingBaslat start/complete/fail state gecislerini ekle**

In `PingBaslat`, immediately before starting work:

```csharp
_pingRunState = _pingRunState.Start();
```

On successful completion:

```csharp
_pingRunState = _pingRunState.Complete("Ping tamamlandi");
```

In the `catch (OperationCanceledException)` branch:

```csharp
_pingRunState = _pingRunState.Canceled();
throw;
```

In the general `catch (Exception ex)` branch before reporting:

```csharp
_pingRunState = _pingRunState.Fail(ex.Message);
```

If current Ping code intentionally catches cancellation without rethrowing, keep user-visible behavior but ensure `OperationCanceledException` is not logged as an error. Project convention prefers propagation.

- [ ] **Step 3: Build and existing tests**

Run:

```powershell
dotnet test ..\AgTarama.slnx --filter ToolRunStateTests
dotnet build ..\AgTarama.slnx
```

Expected: tests PASS, build 0 errors.

---

### Task 11: Full Regression ve Manual UI Smoke

**Files:**
- No code files.

- [ ] **Step 1: Full test suite calistir**

Run:

```powershell
dotnet test ..\AgTarama.slnx
```

Expected: PASS except any already-known documented fail. If a failure appears, inspect whether it is new; do not hide it.

- [ ] **Step 2: Debug build calistir**

Run:

```powershell
dotnet build ..\AgTarama.slnx
```

Expected: 0 errors.

- [ ] **Step 3: UI smoke icin uygulamayi ac**

Run:

```powershell
dotnet run --project AgTarama.csproj
```

Manual checks:

- App opens.
- Cihaz Tara sekmesi opens.
- Subnet chips still populate.
- Normal scan can be started and canceled.
- Progress text changes during scan.
- Grid rows still show IP, Tur, Marka, Guven, Portlar, Kesif.
- Ping panel still works after ToolRunState integration.

- [ ] **Step 4: Manual checkpoint**

Run:

```powershell
git status --short
```

Expected: Only intended plan implementation files plus pre-existing unrelated dirty files are listed. Do not commit unless user explicitly asks.

---

## Self-Review

Spec coverage:

- Cihaz Tara session orkestrasyonu: Tasks 6-8.
- Presenter/donusum katmani: Tasks 3-5.
- Classification service: Tasks 1-2.
- ToolRunState and ToolOutput: Tasks 9-10.
- Hata/iptal/progress/diagnostic: Tasks 6-8.
- Tests: Tasks 1, 3, 6, 9, 11.
- MVVM yok karari: preserved by keeping `MainWindow` partials and adding services/presenter only.
- History decision: Task 8 keeps history trigger in `MainWindow`.

Placeholder scan:

- This plan intentionally contains one mechanical move instruction for the existing 700+ line classifier body instead of duplicating that body. It names exact source methods and exact substitutions, with no unspecified future behavior left open.

Type consistency:

- `DeviceClassificationService.Classify(DeviceInfo)` returns `DeviceClassification`.
- `DeviceScanPresenter.BuildRow(DeviceInfo, DateTime)` returns `DeviceScanRow`.
- `DeviceScanSession.RunAsync(DeviceScanSessionOptions, CancellationToken)` returns `DeviceScanResult`.
- `ToolRunState.Ready(...).Start().Complete()/Canceled()/Fail(...)` matches the tests.
