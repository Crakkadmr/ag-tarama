# Mimari Kararlar & Teknik Borç

## Neden Bu Mimari?

**Tek pencere, MVVM yok.** WPF + `MainWindow.xaml` + `MainWindow.xaml.cs` + 12 partial. Sebep:
- Tek geliştirici + manuel test akışı; MVVM overhead'i değer üretmiyor.
- UI binding doğrudan code-behind'dan; service katmanı UI'dan bağımsız (`Services/` altı).
- Partial bölme → mantıksal modülerlik; XAML hala tek dosya (`MainWindow.xaml`).

## Neden Bu Teknolojiler?

| Karar | Sebep |
|---|---|
| .NET 10 + WPF | Modern C# 13 özellikleri, Windows-only hedef, native AOT gerekmiyor |
| QuestPDF | Açık kaynak, declarative, lisans uyumlu (Community) |
| ClosedXML | Saf C#, Excel COM bağımsız |
| SharpPcap + PacketDotNet | Pasif MAC keşfi için tek production-grade .NET wrapper |
| Supabase | Lisans cloud — PostgreSQL + RLS + REST anon key paterniyle sunucu maliyeti minimum |
| OpenRouter (AI default) | DeepSeek v4 Flash; ucuz, OpenAI uyumlu API |
| xUnit | .NET standardı, fluent assert, `InternalsVisibleTo` desteği |
| Obfuscar (Release) | Açık kaynak, post-build entegre, hassas string'leri korur |

## Önemli Kısıtlamalar

- **Hedef OS:** Sadece Windows 10/11 64-bit (Npcap sürücüsü zorunlu).
- **Versiyon politikası:** `<Version>`, `<AssemblyVersion>`, `<FileVersion>` yalnızca kullanıcı "release et / versiyon yükselt" derse değişir. Kod/doc değişiklikleri csproj versiyonuna dokunmaz.
- **Commit politikası:** Kod değişikliği sonrası otomatik commit ATMA — kullanıcı manuel test eder, kendisi commit'ler.
- **Doc politikası:** Markdown dosyaları sadece `"md güncelle"` komutuyla güncellenir; kod değişiklikleri MD'lere dokunmaz.

## Bilinen Teknik Borç

### Mimari

- **`MainWindow.DeviceScan.cs` 1414 satır + `NetworkTools.cs` 901 satır** — Faz 4 kapsamında alt-partial + service extraction planlı.
- **MVVM dönüşümü yapılmadı** — bilinçli karar (proje boyutu için aşırı). Yeni özellikte de uygulanmıyor.
- **Process adapter yok** — `tshark`, `netsh`, `arp`, `nbtstat`, `tracert` doğrudan `Process.Start` ile çağrılıyor. Test edilebilirlik için `IProcessRunner` adapter pattern uygulanabilir (henüz yapılmadı).

### Güvenlik

- **AI default key** — `AiDefaultKey.cs` XOR-obfuscated; binary'den geri elde edilebilir. Kullanıcı kendi anahtarını set edene kadar shared key kullanılıyor. Maliyet kontrolü `AiUsageMeter` günlük/aylık limit ile yapılıyor; kuota aşılırsa istek reddedilir.
- **`AGT_UPDATE_SIGNER_THUMBPRINT` opsiyonel** — env var set edilmemişse update ZIP imza doğrulaması atlanır, log uyarısı yazılır. Production deploy için thumbprint pinning şiddetle önerilir.

### Bağımlılık

- **`System.IO.Packaging 6.0.0` NU1903 CVE** — `AgTarama.Tests` üzerinden transitif çekiliyor (Microsoft.NET.Test.Sdk). Test paketi ana app'e karışmaz, yine de SDK upgrade ile çözülmeli (Test.Sdk 17.12+ kontrol et).

### Test Kapsama

- 48 test sadece `OuiVendorLookup`, `MacUtils`, `DeviceStore`, `Probes` için.
- **Eksik:** AI iptal semantiği, CIDR /31-/32 sınırları, `AiUsageMeter` lock, `UpdateService.SafeExtractZip`, `WlanService` async, `DeviceClassifier` MikroTik normalize, settings `https` validator.
- **Şu an FAIL eden:** `DeviceStoreTests.GetOrAdd_NewIp_CreatesEntry` (`DeviceStoreTests.cs:17`). Phantom device fix sonrası `Online=false` default değişikliği test beklentisiyle eşleşmiyor olabilir — incelenecek.

### Operasyonel

- **Global exception handler yok** — `App.xaml.cs`'de `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` kayıt yok. Crash logging eksiği.
- **Log retention/rotation yok** — `LogService` append-only; eski log'lar manuel temizlik gerekiyor.
- **UI hata mesajları ham** — çoğunlukla `ex.Message` doğrudan kullanıcıya gidiyor; ürünleşme için kullanıcı dostu mesaj katmanı yok.
- **Stil tekrarı** — `AiDeviceReportWindow.PresetChipStyle`, `SettingsWindow.PasswordBox` stilleri inline; merkezi `Window.Resources`'a alınmadı.

### Lisanslama

- **`licenses_view` security_invoker** — `docs/licensing.md`'de düzeltme yazıyor ama Supabase canlı durumu lokal doğrulanmadı. RLS bypass riski olabilir.
- **`generate_license_key()`, `insert_license_*()`** — fonksiyonlarda `search_path` mutable (Supabase advisor WARN).

## Gelecek Karar Bekleyenler

- Process adapter pattern uygula? (Test edilebilirlik kazancı vs. boilerplate maliyeti.)
- Crash telemetry opt-in? (Sentry vs. minimal local crash log.)
- Tarama profili kaydet/yükle özelliği — `AppSettings` veya ayrı profil dosya formatı.
- AI doğal dil sorgu — son cihaz taramasından bağlam üretip AI'ya gönder.
- `packages.lock.json` ile NuGet versiyonları sabitle (reproducible build).
