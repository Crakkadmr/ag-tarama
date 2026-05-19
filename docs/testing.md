# Test Ortamı

> Bu doküman Faz 3'te (master-refactor.md) tamamlanacak. Şimdilik mevcut test altyapısı + kullanım.

## Mevcut Durum

- **Proje:** `AgTarama.Tests` (xUnit 2.9.2, net10.0-windows)
- **Konum:** `C:\Projects\AG TARAMA PROGRAMI\AgTarama.Tests\`
- **Test sayısı:** 48 (1 fail — `DeviceStoreTests.GetOrAdd_NewIp_CreatesEntry`, [decisions.md](decisions.md))
- **InternalsVisibleTo:** `AgTarama.csproj` `internal` türleri test'te kullanılabilir

## Çalıştırma

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"

# Tüm testler
dotnet test AgTarama.slnx --nologo

# Belirli sınıf
dotnet test AgTarama.slnx --filter "FullyQualifiedName~OuiVendorLookup"

# Coverage
dotnet test AgTarama.slnx --collect:"XPlat Code Coverage"
```

## Test Sınıfları

| Dosya | Test | Kapsam |
|---|---|---|
| `OuiVendorLookupTests.cs` | 18 | null/boş MAC, vendor kısaltma, BulDetay tür ipuçları, routerboard normalize |
| `MacUtilsTests.cs` | 12 | Kolon/dash/dot/raw format, null/boş, OuiPrefix |
| `DeviceStoreTests.cs` | 8 | GetOrAdd, TryGet, Clear, DeviceChanged event, LastSeen, All |
| `ProbeTests.cs` | 10 | Phantom device regresyon, port gate, TryGet contract |

## Yeni Test Yazma

```csharp
using Xunit;
using AgTarama.Services;

public class MyServiceTests
{
    [Fact]
    public void Parse_ValidInput_Works()
    {
        var r = MyService.Parse("1-3");
        Assert.Equal(new[] {1,2,3}, r);
    }

    [Theory]
    [InlineData("1",   new[] {1})]
    [InlineData("1,3", new[] {1,3})]
    public void Parse_Theory(string input, int[] expected)
        => Assert.Equal(expected, MyService.Parse(input));
}
```

## Mock HTTP (AI testleri için — Faz 3)

`AiClient` tests `HttpMessageHandler` override ile:

```csharp
class FakeHandler : HttpMessageHandler
{
    public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(Response);
}
```

## Faz 3 Hedefleri (master-refactor.md)

- `test.ps1` tek-komut harness (`-Filter`, `-Coverage`)
- Yeni test sınıfları: `AiUsageMeterTests`, `AiClientTests`, `CidrParserTests`, `SettingsValidatorTests`, `SafeExtractZipTests`, `DeviceClassifierTests`
- Hedef: 48 → ~110 test, hepsi yeşil
- `CidrParser` extract (`Services/Net/CidrParser.cs`) — DeviceScan partial'dan ayır

## Coverage

```powershell
dotnet test AgTarama.slnx --collect:"XPlat Code Coverage"
# Output: AgTarama.Tests\TestResults\<guid>\coverage.cobertura.xml
```

Yorumlama için ReportGenerator vb. (henüz wire'lı değil).
