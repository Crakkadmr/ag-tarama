# Ağ Haritası (Topoloji Görseli) — Tasarım Dökümanı

> Tarih: 2026-05-23 · Branch: `bugveyeniozellikler` · Proje: AgTarama (Network Sniffer, .NET 10 WPF)
> Durum: Onaylandı (brainstorming sonucu) — uygulama planı bir sonraki adımda yazılacak.

## 1. Amaç

Tamamlanmış bir **Cihaz Tara** sonucunu (anlık görüntü / snapshot), gateway'i merkeze alan ve cihazları **tipine göre kümelenmiş** olarak gösteren interaktif bir harita olarak çizmek. Mevcut cihaz keşif motorunun (`DeviceDiscoveryEngine` + `DeviceStore`) ürettiği veriyi görselleştirir; yeni bir tarama mekanizması eklemez.

**Çözülen problem:** Cihaz Tara sonuçları şu an liste/tablo halinde. Görsel bir harita, ağdaki cihaz dağılımını ve tiplerini bir bakışta anlaşılır kılar.

## 2. Kapsam

### Dahil (v1)
- Mevcut `DeviceStore.All` anlık görüntüsünü çizen yeni **"Harita"** sekmesi.
- Gateway-merkezli, **tipe göre kümelenmiş** yerleşim.
- Düğüme tıklayınca **sağdan açılan detay paneli** (sekme içinde kalır).
- **Zoom + pan** (fare tekerleği + sürükleme).
- **Online/offline görsel ayrımı** (`DeviceInfo.Online`).
- Haritayı **PNG ve PDF** olarak dışa aktarma.
- **Native WPF Canvas** ile çizim — ek NuGet/runtime bağımlılığı yok.

### Hariç (v1 dışı — gelecekte değerlendirilebilir)
- Filtre/arama çubuğu.
- Canlı (sürekli güncellenen) harita.
- Gerçek fiziksel/L2 topoloji (switch port eşlemesi) — **bu veri mevcut değil**, switched LAN'de cihazlar arası bağlantı bilgisi yok.
- Düğümleri elle sürükleyip yeniden konumlandırma.
- Geçmiş kaydından (HistoryService) harita çizme.

## 3. Teknik gerçek: neden gateway-merkezli?

Switched bir LAN'de cihazlar arası fiziksel bağlantı verisi yoktur; tüm cihazlar mantıksal olarak gateway'e bağlıdır. Bu yüzden harita gerçek bir "topoloji keşfi" değil, **gateway'i merkez alan mantıksal bir düzendir**. Kullanıcıya da bu netlikle sunulur (sahte topoloji iddiası yok).

## 4. Mimari ve Dosya Yapısı

Proje **MVVM kullanmaz**; UI wiring `MainWindow.xaml` + partial'larda yapılır, iş mantığı `Services/` katmanındadır. Bu desen korunur.

| Dosya | Tip | Sorumluluk |
|---|---|---|
| `Services/NetworkMapLayout.cs` | **Yeni** (saf, UI'dan bağımsız) | Cihaz listesi + tip bilgisini alıp her düğümün `(X, Y)` konumunu ve küme atamasını hesaplar. Test edilebilir. |
| `Services/MapNode.cs` (veya `NetworkMapLayout.cs` içinde) | **Yeni model** | `MapNode` record — bir düğümün çizim için gereken tüm verisi. |
| `Partials/MainWindow.Harita.cs` | **Yeni partial** | Sekme wiring, Canvas çizimi, tıklama/seçim, zoom/pan, detay paneli, PNG/PDF export. |
| `MainWindow.xaml` | **Değişiklik** | Yeni `TabItem` (Harita): araç çubuğu + `Canvas` + sağ detay paneli. Stiller `Window.Resources`'da. |
| `PdfReportService.cs` | **Değişiklik** | Harita PNG'sini tek sayfaya gömen `GenerateMapReport(byte[] png, ReportMetadata)` metodu. |
| `AgTarama.Tests/NetworkMapLayoutTests.cs` | **Yeni test** | `NetworkMapLayout` davranış testleri. |

### Model: `MapNode`

```csharp
public sealed record MapNode(
    DeviceInfo Device,
    double X,
    double Y,
    string Ikon,      // KimlikBelirleV2 -> CihazKimlik.TurIkon
    string Tur,       // küme anahtarı (Kamera, Bilgisayar, Mobil/IoT, Ağ, Diğer)
    string Etiket,    // düğüm altı kısa etiket (ad varsa ad, yoksa IP)
    bool Online,
    bool IsGateway);
```

### Servis arayüzü: `NetworkMapLayout`

```csharp
public static class NetworkMapLayout
{
    // cihazlar: DeviceStore.All'dan gelen liste.
    // turCozumleyici: her DeviceInfo için (Tur, Ikon) döndürür — MainWindow.DeviceClassifier.KimlikBelirleV2 sarmalanır.
    // genislik/yukseklik: çizim alanı (Canvas boyutu).
    public static IReadOnlyList<MapNode> Hesapla(
        IReadOnlyList<DeviceInfo> cihazlar,
        Func<DeviceInfo, (string Tur, string Ikon)> turCozumleyici,
        double genislik,
        double yukseklik);
}
```

## 5. Yerleşim Algoritması

1. Cihazları `turCozumleyici` ile `Tur`'a göre grupla. Sabit küme sırası: **Kamera, Bilgisayar, Mobil/IoT, Ağ, Diğer**. Boş kümeler atlanır.
2. Gateway(ler) (`IsGateway == true`) merkeze (`genislik/2, yukseklik/2`) yerleştirilir. Birden fazla gateway varsa merkeze yakın küçük bir küme oluştururlar.
3. Aktif (boş olmayan) küme sayısı `K` ise, her küme merkez etrafında `2π/K` açı aralıklarına yerleştirilir; küme merkezi gateway'den `R_kume` yarıçapında bir noktada olur.
4. Küme içindeki `n` cihaz, küme merkezi etrafında küçük bir yay/ızgara üzerinde konumlanır (çakışma olmayacak şekilde sabit minimum aralık).
5. Gateway yoksa: merkeze sanal bir "Ağ" düğümü yerine, kümeler doğrudan tuval merkezine göre dağıtılır (gateway düğümü çizilmez).

**Belirleyicilik:** Aynı girdi → aynı çıktı (IP'ye göre stabil sıralama). Bu, testleri ve PNG/PDF tutarlılığını mümkün kılar.

## 6. Veri Akışı

```
Kullanıcı "Cihaz Tara" çalıştırır  (mevcut akış — değişmez)
        │  DeviceDiscoveryEngine + DeviceStore doldurulur
        ▼
Harita sekmesi → "🔄 Tara/Yenile" butonu
        │  DeviceStore.All okunur
        │  her cihaz için KimlikBelirleV2(dev) → (Tur, Ikon)
        ▼
NetworkMapLayout.Hesapla(...)  →  IReadOnlyList<MapNode>
        ▼
Canvas çizimi (düğüm Border/Ellipse + bağlantı Line)
        ▼
Tıklama → sağ detay paneli   |   tekerlek/sürükleme → zoom/pan   |   export → PNG/PDF
```

- **Anlık görüntü modeli:** Harita `DeviceStore.DeviceChanged`'e abone OLMAZ. Güncel veriyi görmek için kullanıcı Yenile'ye basar. (Seçilen "snapshot" kararı.)
- `DeviceStore` boşsa, Canvas üzerinde "Önce Cihaz Tara çalıştırın" bilgilendirme metni gösterilir.

## 7. Çizim Detayları (Native WPF Canvas)

- Düğüm: bir `Border` (yuvarlatılmış) içinde emoji ikon + altında `TextBlock` etiket. `Canvas.SetLeft/SetTop` ile konumlanır. `Tag = MapNode` (tıklamada cihaza erişim).
- Bağlantı: gateway/küme merkezinden düğüme `Line` (ince, tema mavisi `#3b82f6`).
- **Online:** yeşil kenarlık (`#22c55e`), opacity 1.0. **Offline:** gri kenarlık, opacity ~0.45.
- Gateway düğümü vurgulu (daha büyük, `#0D3B66` dolgu, kalın mavi kenarlık) — mevcut tema renkleriyle uyumlu.
- Zoom/pan: Canvas'a `RenderTransform` olarak `TransformGroup { ScaleTransform, TranslateTransform }`. Tekerlek `ScaleTransform`'u (0.3–3.0 clamp) ayarlar; boş alanda `MouseLeftButtonDown` + `MouseMove` ile `TranslateTransform` güncellenir.

## 8. Detay Paneli

- Sekmenin sağında, başta `Visibility=Collapsed` bir `Border`.
- Düğüme tek tık → panel görünür olur, seçilen düğüm vurgulanır (kenarlık değişimi).
- İçerik: ikon + marka/tür başlığı, IP, MAC, üretici, açık portlar (`AcikPortlar` + `ServisDetaylari`), online durumu ve dolu olan probe alanları (ONVIF/SSDP/SNMP/HTTP/mDNS vb. — yalnızca dolu olanlar gösterilir).
- **🤖 AI Rapor** butonu → mevcut `AiDeviceReportWindow` akışını seçili `DeviceInfo` ile açar (yeniden kullanım, yeni AI kodu yok).
- Panelde bir kapatma (✕) butonu → `Collapsed`.

## 9. Dışa Aktarma

- **PNG:** Canvas'ın görsel ağacı `RenderTargetBitmap` ile bitmap'e dönüştürülür → `PngBitmapEncoder` → kullanıcı seçtiği dosyaya yazılır (`SaveFileDialog`). Mevcut zoom/pan transformundan bağımsız, haritanın tam içeriğini kapsayacak şekilde render edilir.
- **PDF:** Üretilen PNG, `PdfReportService.GenerateMapReport(png, meta)` ile tek A4 sayfaya gömülür (QuestPDF, mevcut Community lisans ayarı). Başlık + tarih + cihaz sayısı meta bilgisi.

## 10. Test Stratejisi

`NetworkMapLayout` saf ve deterministik olduğu için xUnit ile test edilir:

- Tek gateway + birkaç cihaz → gateway tam merkezde mi (`X≈genislik/2, Y≈yukseklik/2`).
- Cihazlar doğru kümelere ayrılmış mı (tip → küme eşlemesi).
- İki düğüm aynı `(X,Y)`'ye düşmüyor (çakışma yok, minimum aralık).
- Boş cihaz listesi → boş sonuç (exception yok).
- Tek cihaz (gateway yok) → tuval içinde konumlanmış tek düğüm.
- Gateway yokken çağrı → exception fırlatmaz, kümeler merkeze göre dağılır.

UI çizimi, zoom/pan, detay paneli ve export **manuel** test edilir (uygulamayı çalıştırıp bir Cihaz Tara sonucu üzerinde).

## 11. Mevcut Kurallarla Uyum (CLAUDE.md / AGENTS.md)

- `async/await` + `CancellationToken`: Harita çizimi senkron (mevcut store verisini okur); ağ I/O yok, dolayısıyla ek async gerekmez. Yenile sırasında ağ taraması başlatılmaz (mevcut snapshot okunur).
- Yeni araç butonu/sekme: `MainTabControl`'a yeni `TabHarita` indeksi eklenir; stiller yalnızca `MainWindow.xaml > Window.Resources`'da.
- Panel sonuçları kendi panelinde gösterilir, ana chat'e yazılmaz.
- Versiyon bump yok, MD güncelleme yok (bu spec hariç — kullanıcı açıkça istedi).

## 12. Açık Riskler / Notlar

- **Çok cihazlı ağlar (100+):** Küme içi yerleşim kalabalıklaşabilir; zoom/pan bunu hafifletir. v1'de filtre yok, gerekirse v2'de eklenir.
- **`KimlikBelirleV2` konumu:** Şu an `Partials/MainWindow.DeviceClassifier.cs` içinde (MainWindow'a bağlı). `NetworkMapLayout` UI'dan bağımsız kalsın diye sınıflandırma bir `Func` delege olarak dışarıdan enjekte edilir; servis `MainWindow`'a bağımlı olmaz.
- **PNG render boyutu:** Tüm düğümleri kapsayan sınırlayıcı kutu hesaplanıp ona göre `RenderTargetBitmap` boyutlandırılır (kırpılma olmasın).
