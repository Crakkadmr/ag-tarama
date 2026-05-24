# Cihaz Tara Mimari Hibrit Tasarimi

Tarih: 2026-05-24
Durum: Taslak, kullanici incelemesi bekliyor

## Amac

AgTarama'da ilk mimari toparlama sprintinin odagi Cihaz Tara olacak. Hedef, Cihaz Tara davranisini degistirmeden tarama cekirdegini daha net, test edilebilir ve genisletilebilir hale getirmek; ayni zamanda Ping, Port, DNS, Trace ve Console gibi araclar icin ortak bir calisma modeli zemini hazirlamaktir.

Secilen yaklasim kademeli hibrittir:

- Cihaz Tara referans mimari haline getirilir.
- Diger araclara tam refactor yapmadan ortak state, output ve cancellation modelleri cikarilir.
- Sonraki dalgalarda diger araclar ayni stile tasinir.

Bu tasarim proje kararlarina uyar: MVVM'e gecilmez, `MainWindow` tamamen kaldirilmaz, ancak code-behind daha ince bir UI baglama katmanina donusturulur.

## Basari Olcutleri

- Cihaz Tara davranisi regresyona ugramaz.
- `MainWindow.DeviceScan*.cs` dosyalarindaki tarama orkestrasyonu azalir.
- Cihaz siniflandirma mantigi UI bagimsiz test edilebilir servise tasinir.
- Progress, diagnostic, hata ve iptal akisinin tek yerden anlasilmasi saglanir.
- Ping, Port, DNS, Trace ve Console icin ortak mimari stile gecis yolu acilir.
- UI thread bloke edilmez; `async/await` ve `CancellationToken` kurallari korunur.

## Kapsam

Ilk sprint genis kapsami hedefler ama uygulama dalgali ilerler.

Kapsamda:

- Cihaz Tara session orkestrasyonu
- Cihaz Tara presenter/donusum katmani
- Cihaz siniflandirma servisinin UI'dan ayrilmasi
- Ortak arac calisma durumu modeli
- Ortak arac cikti modeli
- Hata, iptal, progress ve diagnostic ayrimi
- Ilgili unit testler

Kapsam disi:

- MVVM'e gecis
- Tum `MainWindow` yapisinin bastan yazilmasi
- UI gorsel tasariminin kokten degismesi
- Paket yakalama veya lisanslama mimarisinin bu sprintte yeniden tasarlanmasi
- Versiyon yukseltme veya release hazirligi

## Onerilen Bilesenler

### DeviceScanSession

Konum: `Services/Discovery/DeviceScanSession.cs`

Cihaz Tara'nin orkestrasyon katmani olur. UI'dan gelen temizlenmis secenekleri alir, `DeviceDiscoveryEngine` ile taramayi baslatir, engine eventlerini normalize eder ve tarama yasam dongusunu yonetir.

Sorumluluklar:

- Tarama baslatma
- Iptal token zinciri
- Live mode ve normal scan ayrimi
- Deep scan ayari
- Engine `DeviceChanged` abonelikleri
- Progress ve diagnostic eventleri
- Completed, Failed ve Canceled sonuc uretilmesi

### DeviceScanSessionOptions

Konum: `Services/Discovery/DeviceScanSessionOptions.cs`

UI'dan gelen seceneklerin temiz modelidir. `MainWindow` string textbox ve checkbox durumlarini bu modele cevirir; session bu modelle calisir.

Ornek alanlar:

- Subnet araliklari
- Deep scan
- Live mode
- Timeout degerleri
- Concurrency limit
- Port listesi

### DeviceScanResult

Konum: `Services/Discovery/DeviceScanResult.cs`

Tamamlanan veya sonlanan taramanin sonuc modelidir.

Alanlar:

- Cihaz listesi
- Bulunan cihaz sayisi
- Sure
- Iptal edildi mi
- Hata mesaji
- Diagnostic ozeti

### DeviceScanPresenter

Konum: `Services/Discovery/Presentation/DeviceScanPresenter.cs`

`DeviceInfo` modellerini UI'nin gosterecegi satir ve ozetlere donusturur. Bu sinif ViewModel degildir; WPF binding yasam dongusunu yonetmez. Sadece sunum icin veri hazirlar.

Sorumluluklar:

- `DeviceInfo` -> cihaz satiri donusumu
- Guven skoru hesaplama
- Kesif kaynagi ozeti
- Filtreleme karari
- Filtre sayac metni
- UI'da gosterilecek kisa ad, marka, model, tur ve servis metinleri

### DeviceClassificationService

Konum: `Services/Discovery/Classification/DeviceClassificationService.cs`

`MainWindow.DeviceClassifier.cs` icindeki kanit tabanli siniflandirma mantigi UI bagimsiz servise tasinir.

Sorumluluklar:

- Marka normalize etme
- Tur/marka/model karari
- Kanit agirliklarini kullanma
- `KimlikKararIzi` uretme veya guncelleme
- Guven skoruna veri saglama

Bu tasima sonrasinda `MainWindow` siniflandirma algoritmasini bilmez; sadece servis sonucunu kullanir.

### ToolRunState

Konum: `Services/Tools/ToolRunState.cs`

Ping, Port, DNS, Trace, Console ve ileride Capture gibi araclar icin ortak calisma durumudur.

Durumlar:

- Ready
- Running
- Canceling
- Canceled
- Completed
- Failed

### ToolOutput

Konum: `Services/Tools/ToolOutput.cs`

Arac ciktisinin nereye gidecegini tanimlayan kucuk modeldir.

Alanlar:

- Output kind: Chat, Panel, Diagnostic, Log
- Severity: Info, Success, Warning, Error
- Text
- Optional metadata

## Yeni Cihaz Tara Akisi

1. Kullanici subnet, deep scan ve live mode seceneklerini UI'dan girer.
2. `MainWindow`, UI girdilerini dogrular ve `DeviceScanSessionOptions` olusturur.
3. `MainWindow`, `DeviceScanSession` baslatir.
4. `DeviceScanSession`, `DeviceDiscoveryEngine` cagrilarini ve event aboneliklerini yonetir.
5. Engine, probe ve listener sonuclarini `DeviceStore` uzerinden uretir.
6. `DeviceScanSession`, engine olaylarini tek tip eventlere cevirir:
   - `ProgressChanged`
   - `DeviceChanged`
   - `DiagnosticChanged`
   - `Completed`
   - `Failed`
   - `Canceled`
7. `DeviceScanPresenter`, `DeviceInfo` nesnelerinden UI satirlari ve ozet metinleri uretir.
8. `MainWindow`, yalnizca collection, buton durumu, panel metni, toast ve history tetikleme islerini yapar.

Bu akista UI "tarama nasil calisir?" sorusunu bilmez. UI sadece "su an ne gosterilecek?" sorusuna cevap verir.

## History Karari

Tarama sonunda gecmis kaydini `DeviceScanSession` yazmaz.

Karar:

- `DeviceScanSession` final sonucu uretir.
- `MainWindow` bu sonuca gore history kaydini tetikler.

Gerekce:

- History su an kullanici akisi ve UI niyetine daha yakindir.
- Session katmani domain orkestrasyonu olarak sade kalir.
- Ileride gerekirse ayri bir `HistoryCoordinator` cikarilabilir.

## Hata ve Iptal Modeli

### Iptal

`OperationCanceledException` yutulmaz. Session seviyesinde `Canceled` sonucuna cevrilir.

UI davranisi:

- Butonlar hazir duruma alinir.
- Progress iptal edildi olarak guncellenir.
- Log hata gibi degil, kullanici aksiyonu gibi yazilir.

### Hata

Gercek session hatalari `Failed` sonucudur.

Probe bazli hatalar tum taramayi oldurmemelidir. Bu hatalar diagnostic olarak kalir. Sadece session'i surdurmeyi imkansiz hale getiren durumlar `Failed` olur.

UI davranisi:

- Tek noktadan `HataBildir` veya `ToastGoster(hata: true)` kullanilir.
- Ana chat'e panel sonuc detaylari akitilmaz.

## Progress ve Diagnostic Ayrimi

Progress structured modele yaklastirilir.

Onerilen alanlar:

- Phase
- ScannedHosts
- TotalHosts
- FoundDevices
- Detail

Diagnostic, kullanicinin gormesi gereken ilerleme ile debug/log bilgisini ayirir.

Kurallar:

- Progress bar ve sayaclar progress modelinden beslenir.
- Son islem satiri diagnostic eventlerinden beslenebilir.
- Tum diagnostic detaylari ana chat'e yazilmaz.
- Gerekirse panel icinde detayli akis gorunumu sunulur.

## MainWindow Siniri

`MainWindow.DeviceScan*.cs` dosyalarinda kalacak isler:

- Button click handler'lari
- DataGrid binding
- Context menu aksiyonlari
- Dialog ve clipboard islemleri
- Sekme gecisleri
- Toast ve basit kullanici bildirimi
- History kaydini tetikleme

`MainWindow.DeviceScan*.cs` dosyalarindan azalacak isler:

- Tarama yasam dongusu
- Engine event aboneliginin karmasik yonetimi
- Siniflandirma algoritmasi
- Guven skoru algoritmasi
- Filtre/sayac metni uretimi
- DeviceInfo -> satir donusum detaylari

## Diger Araclara Etkisi

Bu sprintte Ping, Port, DNS, Trace ve Console bastan yazilmaz.

Ancak ortak zemin cikarilir:

- Calisma durumu modeli
- Iptal modeli
- Cikti hedefi modeli
- Hata/success metni standardi

Sonraki sprintte her arac bu modele kademeli tasinabilir.

## Test Tasarimi

### DeviceClassificationServiceTests

Kapsam:

- Marka normalize
- Router/AP, Kamera, NVR/DVR, Yazici gibi tur kararlari
- Kanit agirliklarinin beklenen siralamasi
- Guven skoru ve karar izi

### DeviceScanPresenterTests

Kapsam:

- `DeviceInfo` -> satir donusumu
- Kesif kaynagi siralamasi
- Kisa ad secimi
- Filtre gecme/gecmeme kararlari
- Filtre sayac metni

### DeviceScanSessionTests

Fake engine ile test edilir.

Kapsam:

- Basarili tarama event sirasi
- Iptal akisi
- Failed akisi
- Progress ve diagnostic eventlerinin iletilmesi
- Event unsubscribe davranisi

### ToolRunStateTests

Kapsam:

- Ready -> Running -> Completed
- Running -> Canceling -> Canceled
- Running -> Failed
- Hatali state gecislerinin engellenmesi veya deterministik kalmasi

## Uygulama Dalgalari

### Dalga 1: Cihaz Tara Referans Mimarisi

- `DeviceScanSession` eklenir.
- `DeviceScanPresenter` eklenir.
- `DeviceClassificationService` eklenir.
- `MainWindow.DeviceScan*` dosyalari yeni bilesenleri kullanacak sekilde inceltilir.
- Davranis degisikligi hedeflenmez.

### Dalga 2: Ortak Arac Calisma Modeli

- `ToolRunState` eklenir.
- `ToolOutput` eklenir.
- Ping, Port, DNS, Trace ve Console icin tekrar eden state/cancellation kodlari kademeli azaltilir.

### Dalga 3: Kullanici Akisi ve Polish

- Cihaz Tara -> detay inceleme -> AI rapor -> export/history akisi daha tutarli hale getirilir.
- Progress ve diagnostic gorunumu iyilestirilir.
- Hata, iptal ve tamamlandi mesajlari standardize edilir.

## Riskler

- `MainWindow` partial'lari birbirine bagli oldugu icin tasima sirasinda XAML event baglantilari kirilabilir.
- Siniflandirma mantigi buyuk oldugu icin servis tasimasinda davranis regresyonu olabilir.
- Erken soyutlama diger araclar icin gereksiz model yaratabilir.
- Live mode event akisi yanlis unsubscribe edilirse UI'ya eski session'dan event gelebilir.

## Risk Azaltma

- Once failing veya karakterizasyon testleri yazilir.
- Davranis degistirmeden tasima yapilir.
- Her dalga sonunda build ve test calistirilir.
- Event abonelikleri session ownership altina alinir.
- Ortak arac modeli minimal tutulur; gereksiz framework olusturulmaz.

## Acik Kararlar

Bu tasarimda su kararlar verilmis kabul edilir:

- MVVM'e gecilmeyecek.
- History yazma tetigini `MainWindow` yapacak.
- Cihaz Tara ilk referans mimari olacak.
- Diger araclar ayni sprintte tamamen refactor edilmeyecek.
- Commit kullanici tarafindan manuel yapilacak.
