# Kodlama Kuralları

## C# Genel

- `Nullable=enable`, `ImplicitUsings=enable` — `<Project>` seviyesinde.
- File-scoped namespace **kullanılmaz** (mevcut kod brace-scoped namespace ile yazılı; tutarlılık için bunu sürdür).
- `record` / `sealed record` — DTO + value object'ler (`CihazDto`, `OuiBilgi`, `NetbiosSonuc`, `ScanProgress`, vb.).
- `static` helper class'lar — DI yerine. Servisler çoğunlukla static (`SettingsService.Yukle()`, `HistoryService.Kaydet()`, `OuiVendorLookup.Bul()`).

## Async + İptal

**Zorunlu kurallar:**
- Tüm ağ/IO işlemleri `async Task` + `CancellationToken` parametresi alır.
- UI thread bloke edilmez; long-running iş `await` ile.
- `async void` yasak — sadece event handler'lar (`Button_Click`) için OK.
- İptal mantığı: `OperationCanceledException` **yutulmaz**, çağırana propagate edilir (özellikle AI istekleri).
- `try/catch (Exception ex)` blokları `OperationCanceledException` için `when (ex.GetBaseException() is not OperationCanceledException)` filtresi kullanır (örnek: `PingService`).

**CTS yaşam döngüsü:**
- Her uzun-iş sekmesi kendi `_xxxCts` alanını tutar (örn. `_pingCts`, `_portScanCts`, `_kameraCts`, `_wlanCts`).
- Yeni iş başlarken önceki CTS `Cancel()` + `Dispose()`.
- Pencere/window kapanırken `Closed` handler'ında AI işleri için de CTS iptal (örnek: `AiDeviceReportWindow._cts`).

## Naming

| Tip | Kural | Örnek |
|---|---|---|
| Sınıf | PascalCase | `DeviceDiscoveryEngine` |
| Interface | `I` prefix | `IDeviceDiscoveryEngine` |
| Servis | `XxxService` suffix | `PortScanService`, `WlanService` |
| Probe | `XxxProbe` suffix | `ArpProbe`, `SmbProbe` |
| Listener | `XxxListener` suffix | `SsdpListener`, `MdnsListener` |
| Record / DTO | İçeriğe göre, kısa | `OuiBilgi`, `CihazDto` |
| Test sınıfı | `XxxTests` suffix | `OuiVendorLookupTests` |
| Private alan | `_camelCase` | `_kameraCts`, `_ayarlar` |
| Public sabit | PascalCase | `TabChatbot`, `OuiTablosu` |

**Dil:** Kod identifier'ları + UI metinleri Türkçe ağırlıklı (`KameraTaramaBaslat`, `MesajEkle`, `HataBildir`). Yeni kod aynı stili sürdürmeli — `Start/Stop` yerine `Baslat/Durdur`, `Add/Remove` yerine `Ekle/Sil`.

## Partial Sınırları

`MainWindow` 12 partial'a bölünmüş (`Partials/MainWindow.*.cs`). Hepsi `public partial class MainWindow` — derleyici tek sınıfta birleştirir.

**Kural:** Bir partial > 600 satıra çıkıyorsa böl. Faz 4 hedefi: `DeviceScan.cs` (1414) + `NetworkTools.cs` (901) bölünür → her partial < 600.

**Sorumluluk sınırı:** Partial'lar **UI binding** katmanıdır. İş mantığı `Services/`'e gider:
- Parser (CIDR, port aralığı) → `Services/Net/`
- Export (CSV/XLSX/PDF) → `Services/Export/` (örn. `PdfReportService`, `DeviceExportService`)
- Sınıflandırma → `Services/Discovery/Classification/`

## UI Kuralları

- Sonuçlar `MesajEkle("sonuc", ...)` ile chat'e; panel sonuçları kendi `XxxResultPanel`'e — ana chat'e yazılmaz.
- Sekme geçişi: `MainTabControl.SelectedIndex = TabXxx` (sabitler `MainWindow.xaml.cs`'de).
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`'da. `ActiveActionButton`, `ActionButton`'dan **SONRA** tanımlanmalı.
- Harici araç başlatma: `HariciAracBaslat(exe, ad)`.
- Toast: `ToastGoster(mesaj, hata:bool)`.
- **Kök Grid Row 2** mutlaka `Height="*"` — `Auto` yapılırsa tüm tab ScrollViewer'lar scrolllanamaz.
- WPF .NET 10 — `LetterSpacing` gibi web CSS özellikleri yoktur.

## Güvenlik

- API key + secret kesinlikle source veya log'a yazılmaz (`AiClient` hata mesajlarında `sk-or-***last4` maskelemesi).
- AI base URL `Uri.TryCreate` + `https` scheme zorunlu — HTTP reddedilir.
- Update ZIP `.sha256` zorunlu; `SafeExtractZip` Zip Slip + boyut/entry limit (≤ 5000 entry, ≤ 500 MB toplam, ≤ 200 MB tek entry).
- DPAPI + AES-CBC/HMAC machine-bound vault (`AiKeyVault`).

## Loglama

- `LogService.Kaydet(string)` — info; `LogService.Hata(string, Exception?)` — error.
- Yer: `%APPDATA%\AgTarama\logs\YYYYMMDD.log`.
- `MainWindow.HataBildir(mesaj, ex?)` chat kırmızı mesaj + `LogService.Hata` tek noktadan.

## Test Edilebilirlik

- Yeni iş mantığı **UI'dan ayrı** sınıfa konur (test edilebilirlik için). `MainWindow` partial içine business logic ekleme.
- `Services/` altındaki static helper'lar `internal` olabilir — `InternalsVisibleTo("AgTarama.Tests")` aktif.
- Process çağrıları (`tshark`, `netsh`, `arp`) doğrudan; mock için adapter pattern (`IProcessRunner`) henüz yok — [decisions.md](decisions.md) teknik borç.

## NuGet & Versiyonlama

- Yeni paket: `dotnet add AgTarama/AgTarama.csproj package <Ad>`.
- Versiyon yükseltme **yalnızca** kullanıcı `"versiyon yükselt"` / `"release et"` derse — kod/doc değişiklikleri csproj `<Version>` alanına dokunmaz.
- Release prosedürü: [release.md](release.md).

## Yapılmaması Gerekenler

- `DispatcherTimer` + background thread aynı state'e dokunuyorsa `lock` yoksa data race → her zaman `lock (_sync)`.
- `async void` event handler dışında.
- `Process.Start` sonrası `Kill(entireProcessTree)` + `WaitForExitAsync(CancellationToken.None)` garantisi olmadan finally yok → cleanup eksiği bug üretir.
- `GetOrAdd` ile keşfedilmemiş IP için `DeviceInfo` oluşturma → phantom device. DeepProbe'lar `TryGet` kullanır.
- Stil tekrarı (`AiDeviceReportWindow.PresetChipStyle` inline tanımlı) — gerekiyorsa `Window.Resources`'a taşı.
