# Geliştirici Görev Rehberi

## Build / Run / Test

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"

# Debug build
dotnet build AgTarama.slnx

# Release build (Obfuscar + hash verify)
dotnet build AgTarama.slnx -c Release

# Çalıştır
dotnet run --project AgTarama\AgTarama.csproj

# Tüm testler
dotnet test AgTarama.slnx --nologo

# Belirli test sınıfı
dotnet test AgTarama.slnx --filter "FullyQualifiedName~OuiVendorLookup"

# Faz 3 sonrası: tek-komut test harness
.\test.ps1
.\test.ps1 -Filter "FullyQualifiedName~AiUsageMeter"
.\test.ps1 -Coverage
```

## bin/ obj/ Temizliği

`bin/` Debug + Release birikince GB'larca yer kaplar. Temizleme:

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"
dotnet clean AgTarama.slnx
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory -Force | Remove-Item -Recurse -Force
```

## Yeni Servis Ekleme

1. `Services/` altında `<Ad>Service.cs` oluştur.
2. Sınıfı `static` veya `sealed class` yap; ağ/IO metotları `async Task<T>` + `CancellationToken` parametresi al.
3. Hata/iptal kuralları: [conventions.md](conventions.md).
4. Kullanım yeri partial'dan çağrı; iş mantığı UI'ya bağlanmaz.
5. Test sınıfı yaz: `AgTarama.Tests/<Ad>ServiceTests.cs` (xUnit).

## Yeni Probe Ekleme (Cihaz Keşif)

1. `Services/Discovery/Probes/` altında `<Ad>Probe.cs` oluştur.
2. **FastProbe** (Faz 1 — paralel, tüm IP'lere): mevcut örnek `ArpProbe`, `IcmpProbe`. `DeviceInfo`'yu `Store.GetOrAdd(ip)` ile oluşturabilir.
3. **DeepProbe** (Faz 2 — yalnızca keşfedilmiş host'larda): mevcut örnek `SmbProbe`, `SshBannerProbe`. **Zorunlu:** `store.TryGet(ip)` kullan — yoksa `return` (phantom device guard).
4. `DeviceDiscoveryEngine.StartScanAsync` içinde probe listesine ekle.
5. Test yaz: `AgTarama.Tests/ProbeTests.cs`'e regresyon (örn. `<Ad>Probe_EmptyStore_CreatesNoPhantomDevices`).

## Yeni AI Provider Ekleme

1. `Services/Ai/AiProvider.cs > Presets` listesine yeni preset ekle:
   ```csharp
   new AiProviderPreset("MyProvider", "My Provider Display", "https://api.example.com/v1", "default-model")
   ```
2. `SettingsWindow.xaml` AI ComboBox preset'i otomatik gelir (`AiProvider.Presets` ile bağlı).
3. Base URL `https` zorunlu — `SettingsWindow.xaml.cs` validator zaten enforce ediyor.
4. OpenAI-uyumlu `chat/completions` endpoint bekleniyor.

## Yeni Test Yazma

```csharp
// AgTarama.Tests/MyServiceTests.cs
using Xunit;
using AgTarama.Services;          // InternalsVisibleTo ile internal de erişilebilir

public class MyServiceTests
{
    [Fact]
    public void Parse_ValidInput_ReturnsExpected()
    {
        var result = MyService.Parse("input");
        Assert.Equal("expected", result);
    }

    [Theory]
    [InlineData("a", 1)]
    [InlineData("b", 2)]
    public void Parse_Theory(string input, int expected)
    {
        Assert.Equal(expected, MyService.Parse(input));
    }
}
```

Detay: [testing.md](testing.md).

## EF Migration / Database

**Uygulanabilir değil** — yerel veritabanı yok. Cloud lisans Supabase'de, schema değişikliği SQL migration ile manuel.

Lisans schema dosyaları: `supabase/migrations/*.sql`. Yeni migration:
```bash
supabase db diff -f <name>   # CLI varsa
```

## Yeni Sekme Ekleme

1. `MainWindow.xaml` > `MainTabControl` içine yeni `<TabItem>` ekle.
2. `MainWindow.xaml.cs` > sabit ekle: `private const int TabXxx = N;`.
3. Yeni partial `Partials/MainWindow.Xxx.cs` oluştur — `public partial class MainWindow`.
4. Event handler'lar bu partial'a yazılır; XAML `Click=` referansları otomatik bağlanır.
5. Sekme geçişi: `MainTabControl.SelectedIndex = TabXxx`.

## GitHub Release

[release.md](release.md).

## Sıfırdan Ortam Kurulumu

```powershell
# 1. Repo klonla
git clone https://github.com/Crakkadmr/ag-tarama.git "C:\Projects\AG TARAMA PROGRAMI"
cd "C:\Projects\AG TARAMA PROGRAMI"

# 2. Bağımlılıkları yükle
dotnet restore AgTarama.slnx

# 3. Wireshark Portable indir
# tools/WiresharkPortable64/ klasörünü manuel sağla

# 4. Çalıştır (UAC — Npcap kurulumu için)
dotnet run --project AgTarama\AgTarama.csproj
```

## Sık Karşılaşılan Hatalar

| Hata | Sebep | Çözüm |
|---|---|---|
| `tshark başlatılamadı` | Wireshark Portable eksik | `tools/WiresharkPortable64/` klasörünü manuel kur |
| Npcap kurulmuyor | UAC reddi | Uygulamayı yönetici olarak çalıştır |
| AI istek 401 | API key vault'ta yok / yanlış | Ayarlar > AI > [Değiştir] |
| AI istek "limit aşıldı" | Günlük/aylık token limiti dolu | `%APPDATA%\AgTarama\ai.usage.json` sil veya limiti yükselt |
| Lisans "süresi doldu" | Cache + NTP sync sorunu | Lisans sekmesi > [Yenile] |
| `dotnet test` `DeviceStoreTests` fail | Bilinen | [decisions.md > Test Kapsama](decisions.md) |
