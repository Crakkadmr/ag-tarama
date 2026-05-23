# Ağ Haritası (Topoloji Görseli) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tamamlanmış bir Cihaz Tara sonucunu, gateway-merkezli ve cihaz tipine göre kümelenmiş, tıklanabilir/zoom-pan yapılabilen ve PNG/PDF olarak dışa aktarılabilen bir "Harita" sekmesi olarak çizmek.

**Architecture:** Saf, UI'dan bağımsız `NetworkMapLayout` servisi düğüm konumlarını hesaplar (TDD ile geliştirilir, xUnit). UI tarafı `Partials/MainWindow.Harita.cs` partial'ında native WPF `Canvas` üzerinde çizim, tıklama, zoom/pan ve export yapar. `MainWindow.xaml`'a yeni bir `TabItem` eklenir. PDF, mevcut `PdfReportService`/QuestPDF'e küçük bir metot eklenerek üretilir.

**Tech Stack:** .NET 10, WPF (native Canvas, RenderTargetBitmap, PngBitmapEncoder), QuestPDF (mevcut), xUnit (mevcut).

---

## Ön Bilgiler (uygulayıcı için — okumadan başlama)

**Repo / çözüm yapısı (önemli, sıra dışı):**
- Git repo kökü = `D:\Projects\AG TARAMA PROGRAMI\AgTarama` (ana WPF projesi).
- Çözüm dosyası `AgTarama.slnx` ve test projesi `AgTarama.Tests` **bir üst klasördedir** (`D:\Projects\AG TARAMA PROGRAMI\`) ve **git repo'sunun dışındadır**.
- Sonuç: Test dosyaları (mevcut `DeviceStoreTests.cs` dahil) versiyon kontrolünde değildir. **Commit adımlarında yalnızca repo içindeki (`AgTarama\...`) dosyaları `git add` et.** Test dosyasını `git add` etmeye çalışma — repo dışında olduğu için hata verir.
- Build: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
- Test: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
- Shell: PowerShell. `&&` yoktur; komutları ayrı çalıştır veya `;` kullan.

**Git kimliği:** Bu repoda `user.name`/`user.email` ayarlı olmayabilir. İlk commit'ten önce ayarlı olduğunu doğrula (`git config user.email`). Boşsa kullanıcıdan iste — kendiliğinden global config değiştirme.

**Mevcut kod gerçekleri (doğrulanmış):**
- Cihaz verisi: `private readonly IDeviceDiscoveryEngine _engine` (MainWindow alanı). Liste: `_engine.Store.All` → `IReadOnlyList<DeviceInfo>`.
- `DeviceInfo` (`Services/Discovery/Models/DeviceInfo.cs`) **internal sealed**. Alanlar: `Ip`, `MacAdresi`, `Uretici`, `AcikPortlar (List<int>)`, `ServisDetaylari (Dictionary<int,string>)`, `Online (bool)`, `IsGateway (bool)`, `KesifKaynaklari (HashSet<string>)` + tüm probe alanları.
- Sınıflandırma: `private static CihazKimlik KimlikBelirleV2(DeviceInfo)` (MainWindow partial — `Partials/MainWindow.DeviceClassifier.cs`). `CihazKimlik` = `private sealed class { string Marka; string? Model; string Tur; string TurIkon; }` (`Partials/MainWindow.DeviceScan.cs:36`). `TurIkon` geometrik unicode glyph'tir (◎ ▣ ▢ ⊛ …), emoji değil.
- `InternalsVisibleTo("AgTarama.Tests")` ayarlı (`AgTarama.csproj:31`) → internal `NetworkMapLayout`/`MapNode` test edilebilir.
- Tab sabitleri: `MainWindow.xaml.cs:51-62` (`TabChatbot=0` … `TabLisans=11`).
- TabControl: `MainWindow.xaml:746` (`x:Name="MainTabControl"`), 12 `TabItem`, kapanış `</TabControl>` satır ~2048. Tab header deseni: `<TabItem><TabItem.Header><TextBlock FontFamily="Consolas" FontSize="12" Text="◎  Cihaz Tara"/></TabItem.Header>…</TabItem>`.
- Buton stilleri: `PrimaryButton`, `ActionButton`, `DangerButton`, `ChipButton` (`MainWindow.xaml > Window.Resources`).
- Tema renkleri: toolbar bg `#111827`, kenarlık `#243147`, içerik bg `#0B1220`/`#0D1117`, panel bg `#161B22`, mavi vurgu `#58A6FF`/`#2F81F7`, yeşil `#3FB950`, gri metin `#8B949E`.
- Toast: `ToastGoster(string mesaj, bool hata = false)`. Ayarlar alanı: `_ayarlar` (`AppSettings`).
- AI rapor penceresi: `new AiDeviceReportWindow(IReadOnlyList<CihazDto> cihazlar, AppSettings ayarlar, Func<IReadOnlyList<string>,Task>? yenidenTara = null)`. `CihazDto` (`Services/Ai/AiDeviceAnalyzer.cs:9`) = `(string Ip, string Ad, string Tur, string Marka, string Model, string Ping, string Portlar, string Kesif, string Mac, string Uretici, string Servis, int Guven)` (sıra: docs/services-ai.md). MainWindow'da yardımcılar mevcut: `CihazAdiSec(DeviceInfo)`, `KimlikBelirle(DeviceInfo)`, `GuvenSkoru(DeviceInfo, CihazKimlik)`.

---

## File Structure

| Dosya | Tip | Sorumluluk |
|---|---|---|
| `Services/NetworkMapLayout.cs` | Yeni (saf, internal) | `MapNode` record + `NetworkMapLayout` static: cihaz listesi + tip çözümleyici → düğüm konumları. WPF tipi KULLANMAZ (sadece `double`). |
| `Partials/MainWindow.Harita.cs` | Yeni partial | Sekme wiring, Canvas çizimi, tıklama→detay paneli, zoom/pan, PNG/PDF export. |
| `MainWindow.xaml` | Değişiklik (~satır 2047, `</TabControl>` öncesi) | Yeni `TabItem` (Harita): toolbar + `Canvas` + sağ detay paneli. |
| `MainWindow.xaml.cs` | Değişiklik (satır 62 civarı) | `private const int TabHarita = 12;` |
| `Services/PdfReportService.cs` | Değişiklik | `GenerateMapReport(byte[] pngBytes, ReportMetadata meta)` metodu. |
| `AgTarama.Tests/NetworkMapLayoutTests.cs` | Yeni test (repo DIŞI — commit edilmez) | `NetworkMapLayout` + `KumeyeAta` davranış testleri. |

---

## Task 1: NetworkMapLayout iskeleti + boş liste davranışı

**Files:**
- Create: `Services/NetworkMapLayout.cs`
- Test: `AgTarama.Tests/NetworkMapLayoutTests.cs` (repo dışı; commit edilmez)

- [ ] **Step 1: Failing test yaz**

`AgTarama.Tests/NetworkMapLayoutTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using AgTarama.Services;
using AgTarama.Services.Discovery.Models;
using Xunit;

namespace AgTarama.Tests;

public class NetworkMapLayoutTests
{
    // Test yardımcı: her cihazı sabit (Tur, Ikon)'a çözer.
    private static Func<DeviceInfo, (string Tur, string Ikon)> Cozumleyici(
        Func<DeviceInfo, string> tur)
        => d => (tur(d), "◈");

    [Fact]
    public void Hesapla_BosListe_BosSonuc()
    {
        var sonuc = NetworkMapLayout.Hesapla(
            new List<DeviceInfo>(), Cozumleyici(_ => "Diğer"), 1000, 650);

        Assert.Empty(sonuc);
    }
}
```

- [ ] **Step 2: Testi çalıştır, FAIL doğrula (derlenmez — tip yok)**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: FAIL — `NetworkMapLayout` / `MapNode` derlenmez.

- [ ] **Step 3: Minimal implementasyon**

`Services/NetworkMapLayout.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services;

/// <summary>Bir harita düğümünün çizim için gereken konum + tip bilgisi.</summary>
internal sealed record MapNode(
    DeviceInfo Device,
    double X,
    double Y,
    string Tur,
    string Ikon,
    bool Online,
    bool IsGateway);

/// <summary>
/// Gateway-merkezli, tipe göre kümelenmiş harita yerleşimi. UI'dan bağımsız, deterministik.
/// </summary>
internal static class NetworkMapLayout
{
    public static IReadOnlyList<MapNode> Hesapla(
        IReadOnlyList<DeviceInfo> cihazlar,
        Func<DeviceInfo, (string Tur, string Ikon)> turCozumleyici,
        double genislik,
        double yukseklik)
    {
        var sonuc = new List<MapNode>();
        if (cihazlar == null || cihazlar.Count == 0) return sonuc;
        return sonuc;
    }
}
```

- [ ] **Step 4: Testi çalıştır, PASS doğrula**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: PASS (1 test).

- [ ] **Step 5: Commit (yalnızca repo içi dosya)**

```powershell
git add Services/NetworkMapLayout.cs
git commit -m "feat: NetworkMapLayout iskeleti (bos liste)"
```

> Not: `NetworkMapLayoutTests.cs` repo dışında olduğu için commit'e dahil edilmez; bu beklenen davranıştır.

---

## Task 2: KumeyeAta — tip → küme eşlemesi

Cihaz türlerini 5 sabit kümeye indirger: Kamera, Bilgisayar, Mobil/IoT, Ağ, Diğer.

**Files:**
- Modify: `Services/NetworkMapLayout.cs`
- Test: `AgTarama.Tests/NetworkMapLayoutTests.cs`

- [ ] **Step 1: Failing test ekle**

`NetworkMapLayoutTests.cs` sınıfına ekle:

```csharp
    [Theory]
    [InlineData("Kamera", "Kamera")]
    [InlineData("NVR/DVR", "Kamera")]
    [InlineData("Bilgisayar", "Bilgisayar")]
    [InlineData("NAS", "Bilgisayar")]
    [InlineData("Sunucu", "Bilgisayar")]
    [InlineData("Telefon", "Mobil/IoT")]
    [InlineData("Tablet", "Mobil/IoT")]
    [InlineData("Akıllı Cihaz", "Mobil/IoT")]
    [InlineData("Linux IoT", "Mobil/IoT")]
    [InlineData("Router/AP", "Ağ")]
    [InlineData("Switch", "Ağ")]
    [InlineData("Erişim Noktası", "Ağ")]
    [InlineData("Güvenlik Duvarı", "Ağ")]
    [InlineData("Cihaz", "Diğer")]
    [InlineData("Bilinmeyen şey", "Diğer")]
    public void KumeyeAta_TuruDogruKumeyeKoyar(string tur, string beklenenKume)
    {
        Assert.Equal(beklenenKume, NetworkMapLayout.KumeyeAta(tur));
    }
```

- [ ] **Step 2: Testi çalıştır, FAIL doğrula**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: FAIL — `KumeyeAta` tanımsız.

- [ ] **Step 3: KumeyeAta + küme sırası ekle**

`NetworkMapLayout` sınıfına ekle (Hesapla'nın üstüne):

```csharp
    internal static readonly string[] KumeSirasi =
        { "Kamera", "Bilgisayar", "Mobil/IoT", "Ağ", "Diğer" };

    internal static string KumeyeAta(string tur) => tur switch
    {
        "Kamera" or "NVR/DVR"                                  => "Kamera",
        "Bilgisayar" or "Sunucu" or "NAS"                      => "Bilgisayar",
        "Telefon" or "Tablet" or "Akıllı TV" or "Apple TV"
            or "Akıllı Cihaz" or "Hoparlör" or "Müzik Cihazı"
            or "Linux IoT"                                     => "Mobil/IoT",
        "Router" or "Router/AP" or "Router/Switch" or "Switch"
            or "Switch/AP" or "Erişim Noktası"
            or "Güvenlik Duvarı"                               => "Ağ",
        _                                                      => "Diğer",
    };
```

- [ ] **Step 4: Testi çalıştır, PASS doğrula**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Services/NetworkMapLayout.cs
git commit -m "feat: NetworkMapLayout tip-kume eslemesi (KumeyeAta)"
```

---

## Task 3: Gateway'i merkeze yerleştir

**Files:**
- Modify: `Services/NetworkMapLayout.cs`
- Test: `AgTarama.Tests/NetworkMapLayoutTests.cs`

- [ ] **Step 1: Failing test ekle**

```csharp
    private static DeviceInfo Dev(string ip, bool gateway = false, bool online = true)
        => new() { Ip = ip, IsGateway = gateway, Online = online };

    [Fact]
    public void Hesapla_TekGateway_MerkezeKoyar()
    {
        var cihazlar = new List<DeviceInfo> { Dev("192.168.1.1", gateway: true) };

        var sonuc = NetworkMapLayout.Hesapla(
            cihazlar, Cozumleyici(_ => "Router/AP"), 1000, 650);

        var gw = Assert.Single(sonuc);
        Assert.True(gw.IsGateway);
        Assert.Equal(500, gw.X, 1);   // genislik/2
        Assert.Equal(325, gw.Y, 1);   // yukseklik/2
    }
```

> Not: `DeviceInfo.Ip` `init`'tir; `Online`/`IsGateway` `set`. Yukarıdaki `Dev` yardımcısı object initializer ile kurar.

- [ ] **Step 2: Testi çalıştır, FAIL doğrula**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: FAIL — `sonuc` boş (Hesapla henüz gateway koymuyor).

- [ ] **Step 3: Gateway yerleşimini implemente et**

`Hesapla` gövdesini (boş-liste kontrolünden sonra) şununla değiştir:

```csharp
        double cx = genislik / 2.0, cy = yukseklik / 2.0;

        // Deterministik sıralama (IP'ye göre).
        var sirali = cihazlar.OrderBy(d => d.Ip, StringComparer.Ordinal).ToList();
        var gatewayler = sirali.Where(d => d.IsGateway).ToList();

        // Gateway(ler) merkeze. Tek gateway tam merkez; birden fazlaysa merkez etrafında küçük halka.
        for (int i = 0; i < gatewayler.Count; i++)
        {
            var g = gatewayler[i];
            var (tur, ikon) = turCozumleyici(g);
            double gx = cx, gy = cy;
            if (gatewayler.Count > 1)
            {
                double a = 2 * Math.PI * i / gatewayler.Count;
                gx = cx + 34 * Math.Cos(a);
                gy = cy + 34 * Math.Sin(a);
            }
            sonuc.Add(new MapNode(g, gx, gy, tur, ikon, g.Online, true));
        }

        return sonuc;
```

- [ ] **Step 4: Testi çalıştır, PASS doğrula**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Services/NetworkMapLayout.cs
git commit -m "feat: NetworkMapLayout gateway merkez yerlesimi"
```

---

## Task 4: Cihazları kümelere yerleştir + çakışmama

Gateway olmayan cihazlar kümeye göre gruplanır; her küme merkez etrafında bir açıya, küme içi üyeler küçük halkaya yerleşir. Aynı konuma iki düğüm düşmez.

**Files:**
- Modify: `Services/NetworkMapLayout.cs`
- Test: `AgTarama.Tests/NetworkMapLayoutTests.cs`

- [ ] **Step 1: Failing testler ekle**

```csharp
    [Fact]
    public void Hesapla_CihazlariEkler_GatewayDahil()
    {
        var cihazlar = new List<DeviceInfo>
        {
            Dev("192.168.1.1", gateway: true),
            Dev("192.168.1.10"),
            Dev("192.168.1.11"),
            Dev("192.168.1.12"),
        };

        var sonuc = NetworkMapLayout.Hesapla(
            cihazlar, Cozumleyici(d => d.Ip.EndsWith("10") ? "Kamera" : "Bilgisayar"),
            1000, 650);

        Assert.Equal(4, sonuc.Count);
        Assert.Single(sonuc, n => n.IsGateway);
    }

    [Fact]
    public void Hesapla_IkiCihaz_AyniKonumaDusmez()
    {
        var cihazlar = new List<DeviceInfo> { Dev("10.0.0.5"), Dev("10.0.0.6") };

        var sonuc = NetworkMapLayout.Hesapla(
            cihazlar, Cozumleyici(_ => "Kamera"), 1000, 650);

        Assert.Equal(2, sonuc.Count);
        Assert.NotEqual((sonuc[0].X, sonuc[0].Y), (sonuc[1].X, sonuc[1].Y));
    }

    [Fact]
    public void Hesapla_GatewaySiz_TekCihaz_TuvalIcinde()
    {
        var cihazlar = new List<DeviceInfo> { Dev("10.0.0.9") };

        var sonuc = NetworkMapLayout.Hesapla(
            cihazlar, Cozumleyici(_ => "Diğer"), 1000, 650);

        var n = Assert.Single(sonuc);
        Assert.False(n.IsGateway);
        Assert.InRange(n.X, 0, 1000);
        Assert.InRange(n.Y, 0, 650);
    }

    [Fact]
    public void Hesapla_BirdenFazlaGateway_HepsiIsaretli()
    {
        var cihazlar = new List<DeviceInfo>
        {
            Dev("192.168.1.1", gateway: true),
            Dev("192.168.0.1", gateway: true),
            Dev("192.168.1.50"),
        };

        var sonuc = NetworkMapLayout.Hesapla(
            cihazlar, Cozumleyici(_ => "Router/AP"), 1000, 650);

        Assert.Equal(2, sonuc.Count(n => n.IsGateway));
        Assert.Equal(3, sonuc.Count);
    }
```

- [ ] **Step 2: Testi çalıştır, FAIL doğrula**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: FAIL — gateway olmayan cihazlar sonuca eklenmiyor.

- [ ] **Step 3: Kümeleme yerleşimini ekle**

`Hesapla` içinde, gateway `for` döngüsünden SONRA, `return sonuc;`'tan ÖNCE şunu ekle:

```csharp
        // Gateway olmayanları kümele.
        var gruplar = sirali
            .Where(d => !d.IsGateway)
            .Select(d => { var (tur, ikon) = turCozumleyici(d); return (Dev: d, Tur: tur, Ikon: ikon, Kume: KumeyeAta(tur)); })
            .GroupBy(x => x.Kume)
            .OrderBy(g =>
            {
                int idx = Array.IndexOf(KumeSirasi, g.Key);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ToList();

        int kumeSayisi = gruplar.Count;
        if (kumeSayisi == 0) return sonuc;

        double rKume = Math.Min(genislik, yukseklik) * 0.34; // merkezden küme merkezine
        for (int k = 0; k < kumeSayisi; k++)
        {
            // Üstten (-90°) başla, saat yönünde dağıt.
            double aci = 2 * Math.PI * k / kumeSayisi - Math.PI / 2.0;
            double kcx = cx + rKume * Math.Cos(aci);
            double kcy = cy + rKume * Math.Sin(aci);

            var uyeler = gruplar[k].OrderBy(x => x.Dev.Ip, StringComparer.Ordinal).ToList();
            int n = uyeler.Count;
            double rIc = 16 + 10 * n; // üye sayısıyla büyüyen küme içi yarıçap

            for (int j = 0; j < n; j++)
            {
                double nx, ny;
                if (n == 1) { nx = kcx; ny = kcy; }
                else
                {
                    double a2 = 2 * Math.PI * j / n;
                    nx = kcx + rIc * Math.Cos(a2);
                    ny = kcy + rIc * Math.Sin(a2);
                }
                var u = uyeler[j];
                sonuc.Add(new MapNode(u.Dev, nx, ny, u.Tur, u.Ikon, u.Dev.Online, false));
            }
        }
```

> Not: `return sonuc;` ifadesini bu bloğun ALTINA taşı (gateway döngüsünden sonra tek `return sonuc;` kalsın).

- [ ] **Step 4: Testi çalıştır, TÜM testler PASS**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --filter "FullyQualifiedName~NetworkMapLayout" --nologo -v minimal`
Expected: PASS (tüm NetworkMapLayout testleri).

- [ ] **Step 5: Commit**

```powershell
git add Services/NetworkMapLayout.cs
git commit -m "feat: NetworkMapLayout tipe gore kumeleme yerlesimi"
```

---

## Task 5: Harita sekmesi kabuğu (tab sabiti + XAML)

UI iskeleti: yeni sekme, toolbar, boş Canvas, gizli detay paneli. Henüz çizim yok.

**Files:**
- Modify: `MainWindow.xaml.cs:62` (Tab sabiti)
- Modify: `MainWindow.xaml` (`</TabControl>` öncesi, ~satır 2047)

- [ ] **Step 1: Tab sabitini ekle**

`MainWindow.xaml.cs`, `private const int TabLisans = 11;` satırının ALTINA:

```csharp
    private const int TabHarita    = 12;
```

- [ ] **Step 2: Yeni TabItem'i ekle**

`MainWindow.xaml` içinde, son `</TabItem>`'den sonra ve `</TabControl>`'dan (~satır 2048) ÖNCE şunu ekle:

```xml
            <!-- ═══ Sekme 12: Harita ═══ -->
            <TabItem>
                <TabItem.Header>
                    <TextBlock FontFamily="Consolas" FontSize="12" Text="◫  Harita"/>
                </TabItem.Header>
                <DockPanel LastChildFill="True">
                    <!-- Araç çubuğu -->
                    <Border DockPanel.Dock="Top"
                            Background="#111827" BorderBrush="#243147" BorderThickness="1"
                            CornerRadius="8" Padding="10,6" Margin="0,0,0,8">
                        <WrapPanel>
                            <Button x:Name="HaritaYenileBtn" Style="{StaticResource PrimaryButton}"
                                    Height="36" Padding="14,0" Margin="0,2,6,2"
                                    HorizontalContentAlignment="Center" Click="HaritaYenileBtn_Click"
                                    ToolTip="Son Cihaz Tara sonucunu haritaya çizer.">
                                <TextBlock Text="🔄  Tara / Yenile" FontFamily="Consolas" FontSize="12" FontWeight="Bold"/>
                            </Button>
                            <Button x:Name="HaritaZoomSifirlaBtn" Style="{StaticResource ActionButton}"
                                    Height="36" Padding="12,0" Margin="0,2,6,2"
                                    HorizontalContentAlignment="Center" Click="HaritaZoomSifirlaBtn_Click"
                                    ToolTip="Yakınlaştırma ve kaydırmayı sıfırlar.">
                                <TextBlock Text="⊙  Görünümü Sıfırla" FontFamily="Consolas" FontSize="12"/>
                            </Button>
                            <Button x:Name="HaritaPngBtn" Style="{StaticResource ActionButton}"
                                    Height="36" Padding="12,0" Margin="0,2,6,2"
                                    HorizontalContentAlignment="Center" Click="HaritaPngBtn_Click"
                                    ToolTip="Haritayı PNG görüntüsü olarak kaydeder.">
                                <TextBlock Text="▤  PNG" FontFamily="Consolas" FontSize="12"/>
                            </Button>
                            <Button x:Name="HaritaPdfBtn" Style="{StaticResource ActionButton}"
                                    Height="36" Padding="12,0" Margin="0,2,0,2"
                                    HorizontalContentAlignment="Center" Click="HaritaPdfBtn_Click"
                                    ToolTip="Haritayı PDF raporu olarak kaydeder.">
                                <TextBlock Text="▦  PDF" FontFamily="Consolas" FontSize="12"/>
                            </Button>
                        </WrapPanel>
                    </Border>

                    <!-- Harita alanı + detay paneli -->
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>

                        <!-- Çizim tuvali -->
                        <Border Grid.Column="0" Background="#0B1220" BorderBrush="#243147"
                                BorderThickness="1" CornerRadius="10" ClipToBounds="True">
                            <Grid>
                                <Canvas x:Name="HaritaCanvas" Background="Transparent"
                                        MouseWheel="HaritaCanvas_MouseWheel"
                                        MouseLeftButtonDown="HaritaCanvas_MouseLeftButtonDown"
                                        MouseMove="HaritaCanvas_MouseMove"
                                        MouseLeftButtonUp="HaritaCanvas_MouseLeftButtonUp">
                                    <Canvas.RenderTransform>
                                        <TransformGroup>
                                            <ScaleTransform x:Name="HaritaScale" ScaleX="1" ScaleY="1"/>
                                            <TranslateTransform x:Name="HaritaPan" X="0" Y="0"/>
                                        </TransformGroup>
                                    </Canvas.RenderTransform>
                                </Canvas>
                                <TextBlock x:Name="HaritaBosMesaj"
                                           Text="Önce 'Cihaz Tara' sekmesinde bir tarama çalıştırın, sonra burada Tara / Yenile'ye basın."
                                           Foreground="#8B949E" FontFamily="Consolas" FontSize="13"
                                           TextWrapping="Wrap" MaxWidth="420" TextAlignment="Center"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"
                                           Visibility="Visible"/>
                            </Grid>
                        </Border>

                        <!-- Detay paneli (başta gizli) -->
                        <Border x:Name="HaritaDetayPanel" Grid.Column="1" Width="280"
                                Background="#161B22" BorderBrush="#243147" BorderThickness="1"
                                CornerRadius="10" Margin="8,0,0,0" Padding="12"
                                Visibility="Collapsed">
                            <DockPanel LastChildFill="True">
                                <Grid DockPanel.Dock="Top">
                                    <TextBlock x:Name="HaritaDetayBaslik" Text="" FontFamily="Consolas"
                                               FontSize="14" FontWeight="Bold" Foreground="#58A6FF"
                                               TextWrapping="Wrap" Margin="0,0,24,8"/>
                                    <Button Content="✕" Width="22" Height="22"
                                            HorizontalAlignment="Right" VerticalAlignment="Top"
                                            Style="{StaticResource ChipButton}"
                                            Click="HaritaDetayKapat_Click"/>
                                </Grid>
                                <Button x:Name="HaritaDetayAiBtn" DockPanel.Dock="Bottom"
                                        Style="{StaticResource PrimaryButton}" Height="34" Margin="0,8,0,0"
                                        HorizontalContentAlignment="Center" Click="HaritaDetayAiBtn_Click">
                                    <TextBlock Text="🤖 AI Rapor" FontFamily="Consolas" FontSize="12" FontWeight="Bold"/>
                                </Button>
                                <ScrollViewer VerticalScrollBarVisibility="Auto">
                                    <StackPanel x:Name="HaritaDetayIcerik"/>
                                </ScrollViewer>
                            </DockPanel>
                        </Border>
                    </Grid>
                </DockPanel>
            </TabItem>
```

- [ ] **Step 3: Geçici stub event handler'lar (derleme için)**

XAML'deki handler'lar henüz yok → derleme kırılır. Task 6'da gerçek partial gelecek; şimdilik **derlemeyi geçirmek için** `Partials/MainWindow.Harita.cs`'i minimal stub'larla oluştur:

`Partials/MainWindow.Harita.cs`:

```csharp
using System.Windows;
using System.Windows.Input;

namespace AgTarama;

public partial class MainWindow
{
    private void HaritaYenileBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaZoomSifirlaBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaPngBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaPdfBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaDetayKapat_Click(object sender, RoutedEventArgs e) { }
    private void HaritaDetayAiBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaCanvas_MouseWheel(object sender, MouseWheelEventArgs e) { }
    private void HaritaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
    private void HaritaCanvas_MouseMove(object sender, MouseEventArgs e) { }
    private void HaritaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
}
```

- [ ] **Step 4: Derle ve doğrula**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded (0 error). Uygulama açılınca yeni "◫ Harita" sekmesi görünür, içinde boş tuval + bilgi mesajı.

- [ ] **Step 5: Commit**

```powershell
git add MainWindow.xaml.cs MainWindow.xaml Partials/MainWindow.Harita.cs
git commit -m "feat: Harita sekmesi kabugu (XAML + tab sabiti + stub handler)"
```

---

## Task 6: Haritayı store'dan çiz (düğüm + bağlantı + online/offline)

**Files:**
- Modify: `Partials/MainWindow.Harita.cs`

- [ ] **Step 1: Çizim alanlarını ve using'leri ekle**

`Partials/MainWindow.Harita.cs`'i tümüyle şu içerikle değiştir (stub'lar gerçek implementasyonla birleşir; zoom/pan ve export sonraki task'lerde doldurulacak):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AgTarama.Services;
using AgTarama.Services.Discovery.Models;

namespace AgTarama;

public partial class MainWindow
{
    private const double HaritaDugumBoyut = 46;          // düğüm kutusu kenarı (yaklaşık)
    private IReadOnlyList<MapNode> _haritaDugumler = Array.Empty<MapNode>();
    private DeviceInfo? _haritaSecili;

    private void HaritaYenileBtn_Click(object sender, RoutedEventArgs e) => HaritaCiz();

    private void HaritaCiz()
    {
        var cihazlar = _engine.Store.All;

        double w = HaritaCanvas.ActualWidth  > 10 ? HaritaCanvas.ActualWidth  : 1000;
        double h = HaritaCanvas.ActualHeight > 10 ? HaritaCanvas.ActualHeight : 650;

        _haritaDugumler = NetworkMapLayout.Hesapla(
            cihazlar,
            d => { var k = KimlikBelirle(d); return (k.Tur, k.TurIkon); },
            w, h);

        HaritaCanvas.Children.Clear();
        HaritaDetayPanel.Visibility = Visibility.Collapsed;
        _haritaSecili = null;

        if (_haritaDugumler.Count == 0)
        {
            HaritaBosMesaj.Visibility = Visibility.Visible;
            return;
        }
        HaritaBosMesaj.Visibility = Visibility.Collapsed;

        // Merkez: gateway varsa onun konumu, yoksa tuval ortası.
        var gw = _haritaDugumler.FirstOrDefault(n => n.IsGateway);
        double cx = gw?.X ?? w / 2.0;
        double cy = gw?.Y ?? h / 2.0;

        // Önce bağlantı çizgileri (z-order altta).
        foreach (var dugum in _haritaDugumler.Where(n => !n.IsGateway))
        {
            var line = new Line
            {
                X1 = cx, Y1 = cy, X2 = dugum.X, Y2 = dugum.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                StrokeThickness = 1,
                Opacity = dugum.Online ? 0.55 : 0.25,
            };
            HaritaCanvas.Children.Add(line);
        }

        // Sonra düğümler.
        foreach (var dugum in _haritaDugumler)
            HaritaCanvas.Children.Add(HaritaDugumOlustur(dugum));
    }

    private FrameworkElement HaritaDugumOlustur(MapNode dugum)
    {
        var renk = dugum.Online
            ? Color.FromRgb(0x3F, 0xB9, 0x50)   // yeşil
            : Color.FromRgb(0x6E, 0x76, 0x81);  // gri

        var ikon = new TextBlock
        {
            Text = dugum.Ikon,
            FontFamily = new FontFamily("Consolas"),
            FontSize = dugum.IsGateway ? 22 : 18,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var etiket = new TextBlock
        {
            Text = CihazAdiSec(dugum.Device) ?? dugum.Device.Ip,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 96,
        };

        var icerik = new StackPanel();
        icerik.Children.Add(ikon);
        icerik.Children.Add(etiket);

        var kutu = new Border
        {
            Background = new SolidColorBrush(dugum.IsGateway
                ? Color.FromRgb(0x0D, 0x3B, 0x66)
                : Color.FromRgb(0x1E, 0x29, 0x3B)),
            BorderBrush = new SolidColorBrush(renk),
            BorderThickness = new Thickness(dugum.IsGateway ? 3 : 2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 4, 6, 4),
            Opacity = dugum.Online ? 1.0 : 0.5,
            Cursor = Cursors.Hand,
            Child = icerik,
            Tag = dugum.Device,
        };
        kutu.MouseLeftButtonUp += HaritaDugum_Click;

        // (X,Y) merkezde olacak şekilde yerleştir.
        kutu.Loaded += (_, _) =>
        {
            Canvas.SetLeft(kutu, dugum.X - kutu.ActualWidth / 2.0);
            Canvas.SetTop(kutu, dugum.Y - kutu.ActualHeight / 2.0);
        };
        // İlk ölçüm öncesi yaklaşık konumlandırma (Loaded gelene kadar):
        Canvas.SetLeft(kutu, dugum.X - HaritaDugumBoyut / 2.0);
        Canvas.SetTop(kutu, dugum.Y - HaritaDugumBoyut / 2.0);

        return kutu;
    }

    private void HaritaDugum_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DeviceInfo dev)
        {
            e.Handled = true;            // pan başlatmayı engelle
            HaritaDetayGoster(dev);
        }
    }

    // Geçici stub'lar (sonraki task'lerde doldurulacak):
    private void HaritaDetayGoster(DeviceInfo dev) { }
    private void HaritaZoomSifirlaBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaPngBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaPdfBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaDetayKapat_Click(object sender, RoutedEventArgs e)
        => HaritaDetayPanel.Visibility = Visibility.Collapsed;
    private void HaritaDetayAiBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaCanvas_MouseWheel(object sender, MouseWheelEventArgs e) { }
    private void HaritaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
    private void HaritaCanvas_MouseMove(object sender, MouseEventArgs e) { }
    private void HaritaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
}
```

- [ ] **Step 2: Derle**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Manuel test**

Uygulamayı çalıştır: `dotnet run --project "D:\Projects\AG TARAMA PROGRAMI\AgTarama\AgTarama.csproj"`
1. Cihaz Tara sekmesinde bir tarama yap (yerel subnet).
2. Harita sekmesine geç, "Tara / Yenile"ye bas.
Expected: Gateway ortada (mavi, kalın kenar), diğer cihazlar tipe göre kümeler halinde etrafında; online yeşil/dolu, offline gri/soluk; merkezden çizgiler. Boş store'da bilgi mesajı görünür.

- [ ] **Step 4: Commit**

```powershell
git add Partials/MainWindow.Harita.cs
git commit -m "feat: Harita cizimi (dugum + baglanti + online/offline)"
```

---

## Task 7: Tıklama → detay paneli

**Files:**
- Modify: `Partials/MainWindow.Harita.cs`

- [ ] **Step 1: HaritaDetayGoster'i implemente et**

`HaritaDetayGoster` stub'ını şununla değiştir:

```csharp
    private void HaritaDetayGoster(DeviceInfo dev)
    {
        _haritaSecili = dev;
        var kimlik = KimlikBelirle(dev);

        HaritaDetayBaslik.Text = $"{kimlik.TurIkon}  {kimlik.Marka} · {kimlik.Tur}";
        HaritaDetayIcerik.Children.Clear();

        void Satir(string etiket, string? deger)
        {
            if (string.IsNullOrWhiteSpace(deger)) return;
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock
            {
                Text = etiket,
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            });
            sp.Children.Add(new TextBlock
            {
                Text = deger,
                FontFamily = new FontFamily("Consolas"), FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9)),
                TextWrapping = TextWrapping.Wrap,
            });
            HaritaDetayIcerik.Children.Add(sp);
        }

        Satir("IP", dev.Ip);
        Satir("Ad", CihazAdiSec(dev));
        Satir("Model", kimlik.Model);
        Satir("MAC", dev.MacAdresi);
        Satir("Üretici", dev.Uretici);
        Satir("Durum", dev.Online ? "● Online" : "○ Offline");
        Satir("Ping", dev.PingYanit ? $"{dev.PingMs} ms (TTL {dev.PingTtl})" : null);

        List<int> portlar;
        lock (dev.AcikPortlar) portlar = dev.AcikPortlar.OrderBy(p => p).ToList();
        Satir("Açık Portlar", portlar.Count > 0 ? string.Join(", ", portlar) : null);

        Satir("Keşif", dev.KesifKaynaklari.Count > 0 ? string.Join(", ", dev.KesifKaynaklari) : null);
        Satir("SNMP", dev.SnmpSysDescr);
        Satir("HTTP", dev.HttpFpMarka is null ? null : $"{dev.HttpFpMarka} {dev.HttpFpModel}".Trim());
        Satir("mDNS", string.IsNullOrEmpty(dev.MdnsTur) ? null : dev.MdnsTur);
        Satir("ONVIF", dev.OnvifBulundu ? (dev.OnvifAdi ?? "Bulundu") : null);
        Satir("SSDP", dev.SsdpFriendlyName);

        HaritaDetayAiBtn.IsEnabled = _ayarlar.AiEnabled;
        HaritaDetayPanel.Visibility = Visibility.Visible;
    }
```

- [ ] **Step 2: Derle**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Manuel test**

Uygulamayı çalıştır, Harita'yı yenile, bir düğüme tıkla.
Expected: Sağda panel açılır; IP/MAC/üretici/port/durum + dolu probe alanları listelenir. ✕ ile kapanır.

- [ ] **Step 4: Commit**

```powershell
git add Partials/MainWindow.Harita.cs
git commit -m "feat: Harita dugum detay paneli"
```

---

## Task 8: Detay panelinde AI Rapor butonu

Seçili cihaz için mevcut `AiDeviceReportWindow` açılır (tek elemanlı `CihazDto` listesiyle).

**Files:**
- Modify: `Partials/MainWindow.Harita.cs`

- [ ] **Step 1: HaritaDetayAiBtn_Click'i implemente et**

`HaritaDetayAiBtn_Click` stub'ını şununla değiştir (ve dosya başına `using AgTarama.Services.Ai;` ekle):

```csharp
    private void HaritaDetayAiBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_haritaSecili is not { } dev) return;
        if (!_ayarlar.AiEnabled)
        {
            ToastGoster("AI özellikleri Ayarlar > AI bölümünden kapalı.", hata: true);
            return;
        }

        var kimlik = KimlikBelirle(dev);
        List<int> portlar;
        lock (dev.AcikPortlar) portlar = dev.AcikPortlar.OrderBy(p => p).ToList();
        string servis;
        lock (dev.ServisDetaylari)
            servis = string.Join("; ", dev.ServisDetaylari.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        var dto = new CihazDto(
            dev.Ip,
            CihazAdiSec(dev) ?? "",
            kimlik.Tur,
            kimlik.Marka,
            kimlik.Model ?? "",
            dev.PingYanit ? $"{dev.PingMs} ms" : "-",
            string.Join(", ", portlar),
            string.Join(", ", dev.KesifKaynaklari),
            dev.MacAdresi ?? "",
            dev.Uretici ?? "",
            servis,
            GuvenSkoru(dev, kimlik));

        var win = new AiDeviceReportWindow(new[] { dto }, _ayarlar)
        {
            Owner = this,
        };
        win.Show();
    }
```

> `CihazDto` alan sırası: `(Ip, Ad, Tur, Marka, Model, Ping, Portlar, Kesif, Mac, Uretici, Servis, Guven)`. `AiDeviceReportWindow` 3. parametresi (yenidenTaraCallback) opsiyonel — verilmez.

- [ ] **Step 2: Derle**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Manuel test**

Harita'da bir düğüm seç → "🤖 AI Rapor"a bas.
Expected: AI rapor penceresi tek cihazla açılır (AI açıksa). AI kapalıysa toast uyarısı.

- [ ] **Step 4: Commit**

```powershell
git add Partials/MainWindow.Harita.cs
git commit -m "feat: Harita detay panelinden AI rapor"
```

---

## Task 9: Zoom + pan

**Files:**
- Modify: `Partials/MainWindow.Harita.cs`

- [ ] **Step 1: Pan durumu alanı ekle**

Sınıfın üst alan tanımlarına (`_haritaSecili` yanına) ekle:

```csharp
    private bool _haritaPanAktif;
    private Point _haritaPanBaslangic;
    private double _haritaPanX0, _haritaPanY0;
```

(Dosya başında `using System.Windows;` zaten var — `Point` için yeterli.)

- [ ] **Step 2: Zoom + pan + reset handler'larını implemente et**

İlgili stub'ları şunlarla değiştir:

```csharp
    private void HaritaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double faktor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        double yeni = Math.Clamp(HaritaScale.ScaleX * faktor, 0.3, 3.0);
        HaritaScale.ScaleX = yeni;
        HaritaScale.ScaleY = yeni;
        e.Handled = true;
    }

    private void HaritaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Düğüm tıklaması e.Handled=true yaptıysa buraya gelmez.
        _haritaPanAktif = true;
        _haritaPanBaslangic = e.GetPosition(this);
        _haritaPanX0 = HaritaPan.X;
        _haritaPanY0 = HaritaPan.Y;
        HaritaCanvas.CaptureMouse();
    }

    private void HaritaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_haritaPanAktif) return;
        var p = e.GetPosition(this);
        HaritaPan.X = _haritaPanX0 + (p.X - _haritaPanBaslangic.X);
        HaritaPan.Y = _haritaPanY0 + (p.Y - _haritaPanBaslangic.Y);
    }

    private void HaritaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _haritaPanAktif = false;
        HaritaCanvas.ReleaseMouseCapture();
    }

    private void HaritaZoomSifirlaBtn_Click(object sender, RoutedEventArgs e)
    {
        HaritaScale.ScaleX = 1; HaritaScale.ScaleY = 1;
        HaritaPan.X = 0; HaritaPan.Y = 0;
    }
```

- [ ] **Step 3: Derle**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded.

- [ ] **Step 4: Manuel test**

Harita'da fare tekerleği → yakınlaş/uzaklaş; boş alanda sürükle → kaydır; düğüme tıkla → panel açılır (pan tetiklenmez); "Görünümü Sıfırla" → başa döner.

- [ ] **Step 5: Commit**

```powershell
git add Partials/MainWindow.Harita.cs
git commit -m "feat: Harita zoom + pan"
```

---

## Task 10: PNG dışa aktarma

Zoom/pan'dan bağımsız, haritanın tam içeriğini PNG olarak kaydeder.

**Files:**
- Modify: `Partials/MainWindow.Harita.cs`

- [ ] **Step 1: HaritaGoruntuUret + HaritaPngBtn_Click'i implemente et**

`using Microsoft.Win32;` ve `using System.IO;` dosya başına ekle. `HaritaPngBtn_Click` stub'ını ve yeni yardımcıyı şununla değiştir/ekle:

```csharp
    // Mevcut zoom/pan'dan bağımsız, kimlik transform ile tam tuvali bitmap'e render eder.
    private RenderTargetBitmap? HaritaBitmapUret()
    {
        if (_haritaDugumler.Count == 0) return null;

        double w = HaritaCanvas.ActualWidth  > 10 ? HaritaCanvas.ActualWidth  : 1000;
        double h = HaritaCanvas.ActualHeight > 10 ? HaritaCanvas.ActualHeight : 650;

        // Transform'u geçici sıfırla (tam içerik tuval sınırları içinde).
        var eskiTransform = HaritaCanvas.RenderTransform;
        HaritaCanvas.RenderTransform = Transform.Identity;
        HaritaCanvas.UpdateLayout();

        var bmp = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);

        // Arka planı da boyamak için tuvali bir kutuya sar yerine doğrudan tuvali render et;
        // arka plan Transparent olduğundan önce dolu bir dikdörtgen çizer gibi DrawingVisual kullan.
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0B, 0x12, 0x20)),
                null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(HaritaCanvas) { Stretch = Stretch.None },
                null, new Rect(0, 0, w, h));
        }
        bmp.Render(dv);

        HaritaCanvas.RenderTransform = eskiTransform;
        HaritaCanvas.UpdateLayout();
        return bmp;
    }

    private byte[]? HaritaPngBytes()
    {
        var bmp = HaritaBitmapUret();
        if (bmp == null) return null;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private void HaritaPngBtn_Click(object sender, RoutedEventArgs e)
    {
        var png = HaritaPngBytes();
        if (png == null) { ToastGoster("Önce haritayı çizin (Tara / Yenile).", hata: true); return; }

        var dlg = new SaveFileDialog
        {
            Filter = "PNG görüntü (*.png)|*.png",
            FileName = $"ag-haritasi-{DateTime.Now:yyyyMMdd-HHmm}.png",
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllBytes(dlg.FileName, png);
        ToastGoster($"Harita kaydedildi: {Path.GetFileName(dlg.FileName)}");
    }
```

> `BitmapFrame`, `PngBitmapEncoder`, `RenderTargetBitmap`, `PixelFormats` → `System.Windows.Media.Imaging` ad alanında. Dosya başına `using System.Windows.Media.Imaging;` ekle.

- [ ] **Step 2: Derle**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Manuel test**

Harita'yı çiz (zoom/pan uygula), "PNG"e bas, kaydet. Açılan PNG tüm haritayı (sıfır transform ile) koyu arka plan üzerinde içermeli — kırpılma yok.

- [ ] **Step 4: Commit**

```powershell
git add Partials/MainWindow.Harita.cs
git commit -m "feat: Harita PNG disa aktarma"
```

---

## Task 11: PDF dışa aktarma

PNG'yi tek A4 sayfaya gömen rapor (mevcut QuestPDF altyapısı).

**Files:**
- Modify: `Services/PdfReportService.cs`
- Modify: `Partials/MainWindow.Harita.cs`

- [ ] **Step 1: PdfReportService.GenerateMapReport ekle**

`Services/PdfReportService.cs` içinde `GenerateDeviceScanReport`'tan sonra, `}` (sınıf kapanışı) öncesine ekle:

```csharp
    public static byte[] GenerateMapReport(byte[] pngBytes, ReportMetadata meta)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(16);
                page.MarginVertical(12);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Height(36).Row(r =>
                {
                    r.RelativeItem().Column(col =>
                    {
                        col.Item().Text("NETWORK SNIFFER - AG HARITASI").Bold().FontSize(12).FontColor("#58A6FF");
                        col.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm} | Operator: {meta.Operator}")
                           .FontSize(8).FontColor("#8B949E");
                    });
                });

                page.Content().PaddingVertical(4).AlignCenter().AlignMiddle()
                    .Image(pngBytes).FitArea();

                page.Footer().Height(16).Row(r =>
                {
                    r.RelativeItem().Text("Network Sniffer - made by demircan").FontSize(7).FontColor("#484F58");
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(7).FontColor("#484F58");
                        t.CurrentPageNumber().FontSize(8).FontColor("#8B949E");
                    });
                });
            });
        }).GeneratePdf();
    }
```

- [ ] **Step 2: HaritaPdfBtn_Click'i implemente et**

`Partials/MainWindow.Harita.cs`, `using AgTarama.Services;` zaten var. `HaritaPdfBtn_Click` stub'ını değiştir:

```csharp
    private void HaritaPdfBtn_Click(object sender, RoutedEventArgs e)
    {
        var png = HaritaPngBytes();
        if (png == null) { ToastGoster("Önce haritayı çizin (Tara / Yenile).", hata: true); return; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF rapor (*.pdf)|*.pdf",
            FileName = $"ag-haritasi-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var pdf = PdfReportService.GenerateMapReport(png, new ReportMetadata());
            File.WriteAllBytes(dlg.FileName, pdf);
            ToastGoster($"PDF kaydedildi: {System.IO.Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            ToastGoster($"PDF hatası: {ex.Message}", hata: true);
        }
    }
```

- [ ] **Step 3: Derle**

Run: `dotnet build "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo`
Expected: Build succeeded.

- [ ] **Step 4: Manuel test**

Harita'yı çiz, "PDF"e bas, kaydet. PDF tek yatay A4 sayfada başlık + harita görüntüsünü içermeli.

- [ ] **Step 5: Commit**

```powershell
git add Services/PdfReportService.cs Partials/MainWindow.Harita.cs
git commit -m "feat: Harita PDF disa aktarma (PdfReportService.GenerateMapReport)"
```

---

## Task 12: Bütünleşik manuel test + tamamlama

**Files:** (yok — doğrulama)

- [ ] **Step 1: Tam test paketini çalıştır**

Run: `dotnet test "D:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx" --nologo -v minimal`
Expected: NetworkMapLayout testleri dahil tümü PASS (önceden bilinen 1 fail varsa AGENTS.md'de belirtildiği gibi onu hariç tut).

- [ ] **Step 2: Uçtan uca manuel senaryo**

`dotnet run --project "D:\Projects\AG TARAMA PROGRAMI\AgTarama\AgTarama.csproj"`
1. Cihaz Tara → yerel ağı tara.
2. Harita → Tara/Yenile: gateway merkez, tipe göre kümeler, online/offline ayrımı, bağlantı çizgileri.
3. Düğüme tıkla → detay paneli; ✕ ile kapat.
4. AI Rapor (AI açıksa) → pencere açılır.
5. Zoom/pan + Görünümü Sıfırla.
6. PNG ve PDF kaydet → dosyaları aç, içerik doğru.
7. Boş store (uygulamayı yeniden başlatıp doğrudan Harita) → bilgi mesajı.

- [ ] **Step 3: Tamamlama**

Announce: "I'm using the finishing-a-development-branch skill to complete this work."
REQUIRED SUB-SKILL: superpowers:finishing-a-development-branch — testleri doğrula, seçenekleri sun (merge/PR/temizlik), seçimi uygula.

---

## Self-Review

**1. Spec coverage:**
- Yeni "Harita" sekmesi (snapshot) → Task 5, 6. ✓
- Gateway-merkezli, tipe göre kümelenmiş yerleşim → Task 2-4 (NetworkMapLayout). ✓
- Sağdan detay paneli (sekme içinde) → Task 5 (XAML), 7 (içerik). ✓
- Zoom + pan → Task 9. ✓
- Online/offline görsel ayrımı → Task 6 (renk/opaklık) + layout `Online` alanı. ✓
- PNG + PDF export → Task 10, 11. ✓
- Native WPF Canvas, ek bağımlılık yok → tüm UI task'leri. ✓
- AI Rapor (AiDeviceReportWindow reuse) → Task 8. ✓
- Test (NetworkMapLayout unit) → Task 1-4. UI manuel → Task 6-12. ✓
- Kapsam dışı (filtre/arama, canlı, gerçek topoloji, sürükleme) → hiçbir task eklemiyor. ✓

**2. Placeholder taraması:** Tüm kod blokları somut; "TBD"/"handle edge cases" yok. Edge case'ler (boş liste, gateway'siz, çoklu gateway) Task 1/4'te gerçek testlerle. ✓

**3. Tip tutarlılığı:**
- `MapNode(DeviceInfo, double X, double Y, string Tur, string Ikon, bool Online, bool IsGateway)` — Task 1'de tanımlı, Task 3/4'te aynı sırada kullanılıyor. ✓
- `NetworkMapLayout.Hesapla(IReadOnlyList<DeviceInfo>, Func<DeviceInfo,(string,string)>, double, double)` — tüm task'lerde aynı imza. ✓
- `KumeyeAta(string)` / `KumeSirasi` — Task 2'de tanımlı, Task 4'te kullanılıyor. ✓
- `KimlikBelirle(DeviceInfo)→CihazKimlik{Tur,TurIkon,Marka,Model}`, `CihazAdiSec`, `GuvenSkoru` — mevcut MainWindow yardımcıları, Task 6/7/8'de kullanılıyor. ✓
- `CihazDto(...12 alan...)`, `AiDeviceReportWindow(IReadOnlyList<CihazDto>, AppSettings, Func?…)` — Task 8'de doğrulanmış imza. ✓
- `PdfReportService.GenerateMapReport(byte[], ReportMetadata)` — Task 11'de tanımlı + çağrılıyor. ✓
- XAML eleman adları (`HaritaCanvas`, `HaritaScale`, `HaritaPan`, `HaritaDetayPanel`, `HaritaDetayBaslik`, `HaritaDetayIcerik`, `HaritaDetayAiBtn`, `HaritaBosMesaj`) — Task 5'te tanımlı, Task 6-11'de kullanılıyor. ✓
