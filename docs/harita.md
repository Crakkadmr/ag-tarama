# Ağ Haritası Sekmesi

Sekme 12 (`TabHarita = 12`), Cihaz Tara sonucunu topoloji haritası olarak görselleştirir. Gateway merkeze yerleşir; cihazlar türe göre kümelere ayrılarak çevresine dağılır.

## Mimari Özet

```
MainWindow.Harita.cs       — çizim, etkileşim, detay paneli, dışa aktarma
Services/NetworkMapLayout.cs — koordinat hesabı (UI'dan bağımsız, test edilebilir)
Services/PdfReportService.cs — PDF üretimi (GenerateMapReport)
```

## Akış

1. Kullanıcı "Cihaz Tara" sekmesinde tarama çalıştırır → `_engine.Store` dolar.
2. "◫ Harita" sekmesine geçer → `HaritaYenileBtn` ("🔄 Tara / Yenile") tıklar.
3. `HaritaCiz()` → `NetworkMapLayout.Hesapla(cihazlar, turCozumleyici, w, h)` çağrılır.
4. Dönen `IReadOnlyList<MapNode>` üzerinden bağlantı çizgileri + düğümler canvas'a eklenir.
5. Düğüme tıklanınca sağda `HaritaDetayPanel` (280px) açılır.

## NetworkMapLayout (`Services/NetworkMapLayout.cs`)

### MapNode

```csharp
internal sealed record MapNode(
    DeviceInfo Device, double X, double Y,
    string Tur, string Ikon, bool Online, bool IsGateway);
```

### KumeyeAta — tür → küme eşlemesi

| Küme | Türler |
|---|---|
| `Kamera` | Kamera, NVR/DVR |
| `Bilgisayar` | Bilgisayar, Sunucu, NAS |
| `Mobil/IoT` | Telefon, Tablet, Akıllı TV, Apple TV, Akıllı Cihaz, Hoparlör, Müzik Cihazı, Linux IoT |
| `Ağ` | Router, Router/AP, Router/Switch, Switch, Switch/AP, Erişim Noktası, Güvenlik Duvarı |
| `Diğer` | Geri kalan her şey |

`KumeSirasi = ["Kamera", "Bilgisayar", "Mobil/IoT", "Ağ", "Diğer"]` — kümeler saat 12'den başlayarak saat yönünde sıralanır.

### Hesapla — yerleşim algoritması

- **Gateway:** Canvas merkezine (cx, cy) konumlanır. Birden fazla gateway varsa merkez etrafında küçük dairede (r=34) eşit açıyla dağılır.
- **Küme merkezi:** Her küme, merkeze `rKume = min(w,h) × 0.34` uzaklıkta ve eşit açıyla (saat 12'den başlar) yerleşir.
- **Küme içi düğümler:** Küme merkezine `rIc = 16 + 10×n` yarıçaplı daire üzerinde eşit açıyla dağılır (n=1 ise küme merkezine çakışır).
- IP'ye göre sıralanır → deterministik çizim (aynı cihazlar her "Yenile"de aynı yerde).

## MainWindow.Harita.cs (`Partials/`)

### Alan değişkenleri

```csharp
private const double HaritaDugumBoyut = 46;       // Canvas.SetLeft/Top için başlangıç tahmin
private IReadOnlyList<MapNode> _haritaDugumler;    // son çizilen düğümler
private DeviceInfo? _haritaSecili;                 // detay panelinde açık cihaz
private bool _haritaPanAktif;                      // sürükleme kilidi
private Point _haritaPanBaslangic;
private double _haritaPanX0, _haritaPanY0;
```

### HaritaCiz()

1. `HaritaCanvas.Children.Clear()` — önceki çizimi siler.
2. `NetworkMapLayout.Hesapla(...)` çağrılır; 0 cihaz varsa `HaritaBosMesaj` gösterilir.
3. Gateway haricindeki tüm düğümler için gateway merkezinden düğüme `Line` çizilir (online: opacity=0.55, offline: 0.25).
4. Her düğüm için `HaritaDugumOlustur(MapNode)` → `Border` döner, canvas'a eklenir.

### HaritaDugumOlustur(MapNode)

- **Renk:** online → `#3FB950`, offline → `#6E7681`.
- **Arka plan:** gateway → `#0D3B66` (daha koyu), normal → `#1E293B`.
- **Çerçeve kalınlığı:** gateway → 3px, normal → 2px; opacity offline'da 0.5.
- **İçerik:** `StackPanel` — emoji ikon (TextBlock, gateway 22pt / normal 18pt) + etiket (IP veya cihaz adı, max 96px, ellipsis).
- **Konum:** `Loaded` event'inde `ActualWidth/Height` bilinince yeniden hizalanır (centred). İlk geçici konum `HaritaDugumBoyut/2` ile tahminlenir.
- **Tıklama:** `MouseLeftButtonUp` → `HaritaDugum_Click` → `HaritaDetayGoster(dev)`.

### Pan & Zoom

| Eylem | Kod |
|---|---|
| Mouse scroll | `HaritaCanvas_MouseWheel` — `HaritaScale` ×1.1 veya ÷1.1; clamp [0.3, 3.0] |
| Sürükleme | `MouseLeftButtonDown` → başlangıç noktası; `MouseMove` > 5px eşik → `CaptureMouse()` + `HaritaPan` güncelle |
| Bırakma | `MouseLeftButtonUp` → `ReleaseMouseCapture()`, `_haritaPanAktif=false` |
| Sıfırla | `HaritaZoomSifirlaBtn_Click` → `Scale=1`, `Pan=0/0` |

**Not:** `CaptureMouse` sürükleme başlayınca (5px eşikten sonra) alınır — erken capture child `Border`'ların `MouseLeftButtonUp` olayını yutardı.

`HaritaCanvas.RenderTransform`:
```xml
<TransformGroup>
    <ScaleTransform x:Name="HaritaScale"/>
    <TranslateTransform x:Name="HaritaPan"/>
</TransformGroup>
```

### Detay Paneli (`HaritaDetayPanel`)

Tıklanan cihaz için sağ tarafta 280px genişliğinde `Border` (başta `Collapsed`).

`HaritaDetayGoster(DeviceInfo)`:
- `HaritaDetayBaslik` — `"{ikon}  {marka} · {tür}"`
- `HaritaDetayIcerik` — IP, Ad, Model, MAC, Üretici, Durum, Ping, Açık Portlar, Keşif, SNMP, HTTP, mDNS, ONVIF, SSDP (dolu alanlar dinamik oluşturulur)
- `HaritaDetayAiBtn` — `_ayarlar.AiEnabled` ise etkin; `AiDeviceReportWindow` açar

Kapatma: `HaritaDetayKapat_Click` → `Visibility=Collapsed`.

### Dışa Aktarma

#### PNG (`HaritaPngBtn_Click`)

1. `HaritaBitmapUret()` — `RenderTransform` geçici olarak `Transform.Identity`'ye alınır, `RenderTargetBitmap` (96dpi, Pbgra32) oluşturulur.
2. Arka plan `#0B1220` dolgu, üstüne `VisualBrush(HaritaCanvas)` çizilir.
3. `PngBitmapEncoder` → `byte[]` → `SaveFileDialog` → dosyaya yazılır.

#### PDF (`HaritaPdfBtn_Click`)

1. Önce `HaritaPngBytes()` ile PNG elde edilir.
2. `PdfReportService.GenerateMapReport(pngBytes, meta)` — QuestPDF A4 Landscape, başlık + tarih + `Image.FitArea()`.
3. `SaveFileDialog` → dosyaya yazılır.

## XAML Named Elements

| Name | Tür | Açıklama |
|---|---|---|
| `HaritaCanvas` | Canvas | Ana çizim tuvali |
| `HaritaScale` | ScaleTransform | Zoom (0.3–3.0) |
| `HaritaPan` | TranslateTransform | Pan (sürükleme offseti) |
| `HaritaBosMesaj` | TextBlock | Veri yokken gösterilir |
| `HaritaYenileBtn` | Button | Haritayı yeniden çizer |
| `HaritaZoomSifirlaBtn` | Button | Scale=1, Pan=0 |
| `HaritaPngBtn` | Button | PNG dışa aktarma |
| `HaritaPdfBtn` | Button | PDF dışa aktarma |
| `HaritaDetayPanel` | Border | Sağ detay paneli (Collapsed/Visible) |
| `HaritaDetayBaslik` | TextBlock | İkon + marka · tür başlığı |
| `HaritaDetayIcerik` | StackPanel | Dinamik satırlar (Satir() helper) |
| `HaritaDetayAiBtn` | Button | AI Rapor butonu |

## Bağımlılıklar

- `Services/NetworkMapLayout.cs` — `NetworkMapLayout.Hesapla`, `MapNode`
- `Services/PdfReportService.cs` — `GenerateMapReport(byte[], ReportMetadata)`
- `MainWindow.DeviceClassifier.cs` — `KimlikBelirle(dev)`, `CihazAdiSec(dev)`, `GuvenSkoru`
- `AiDeviceReportWindow` — "🤖 AI Rapor" tıklamasında açılır
- `_engine.Store.All` — tarama verisi kaynağı
