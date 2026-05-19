# AgTarama Dokümantasyon İndeksi

> Network Sniffer (AgTarama) — .NET 10 WPF ağ tarama uygulaması.
> AI agent + insan geliştirici giriş noktası.
> Üst seviye giriş: `AGENTS.md` (Codex CLI master index).

## Dosya Haritası

| Ne yapıyorsun? | Oku |
|---|---|
| Stack, komut, env değişken, csproj özeti | [project.md](project.md) |
| Mimari, klasör ağacı, mesaj türleri | [architecture.md](architecture.md) |
| C# kuralları, async pattern, naming, partial sınırları | [conventions.md](conventions.md) |
| NuGet bağımlılıkları + amaçları | [nuget-packages.md](nuget-packages.md) |
| Mimari kararlar, teknik borç, bilinen kısıtlar | [decisions.md](decisions.md) |
| Yaygın görevler (servis ekle, probe ekle, AI provider, test yaz) | [tasks.md](tasks.md) |
| Test ortamı ve test yazma rehberi | [testing.md](testing.md) |
| GitHub Release prosedürü | [release.md](release.md) |
| Versiyon değişiklik geçmişi | [CHANGELOG.md](CHANGELOG.md) |
| XAML, stil, renk, sekme | [ui.md](ui.md) |
| MainWindow partial dosya haritası | [partials.md](partials.md) |
| Servisler — core (capture, network, history, settings, OUI…) | [services.md](services.md) |
| AI servisleri (AiClient, AiKeyVault, AiUsageMeter, prompts, analyzer'lar) | [services-ai.md](services-ai.md) |
| Cihaz keşif alt sistemi (DeviceDiscoveryEngine, Probes, Listeners) | [services-discovery.md](services-discovery.md) |
| Lisans, Supabase, güvenlik, güncelleme | [licensing.md](licensing.md) |

## Hızlı Başlangıç

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"
dotnet build AgTarama.slnx     # Debug build
dotnet test  AgTarama.slnx     # Tüm testler
dotnet run --project AgTarama\AgTarama.csproj
```

Detay: [project.md](project.md), [tasks.md](tasks.md).

## Doc Güncelleme Politikası

Markdown dosyaları **otomatik güncellenmez**. Sadece kullanıcı `"md güncelle"` dediğinde elden geçirilir. Kod değişiklikleri MD dosyalarına dokunmaz.
