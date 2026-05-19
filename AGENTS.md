# AGENTS.md — Proje Master Index

> AI agent (Codex CLI + Claude Code) giriş noktası.
> Detaylı referans: `docs/` klasöründe konuya göre ayrılmış.
> Son güncelleme: 2026-05-19 (v0.4.0 + master-refactor sprinti).

---

## 1. Proje Kimliği

| Alan | Değer |
|---|---|
| Ad | Network Sniffer (AgTarama) |
| Tip | WPF Desktop Uygulaması |
| Hedef | `net10.0-windows`, `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable` |
| Output | `WinExe` |
| Namespace | `AgTarama` |
| Sürüm | v0.4.0 |
| Solution | `C:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx` |
| Branch | `bugveyeniozellikler` (main: `main`) |
| Git user | Crakkadmr |
| Kök yol | `C:\Projects\AG TARAMA PROGRAMI\AgTarama` |

Detay: [docs/project.md](docs/project.md).

---

## 2. Proje Amacı

WPF tabanlı **Network Sniffer** markalı chatbot arayüzlü ağ tarama ve paket yakalama uygulaması.

Ana özellikler:
- Paket yakalama (tshark) + Wireshark Portable analizi
- Ping, Port Tara, Traceroute, DNS, ARP, Wake-on-LAN
- Bant genişliği monitörü (grafik + istatistik)
- Cihaz Tara (ONVIF+WSD, SSDP, mDNS, SNMP, MNDP, Ubiquiti, HTTP fingerprint, ARP, NetBIOS, LLMNR)
- Wi-Fi Tarama (Evil-Twin tespiti)
- F12 komut konsolu (`CommandRouter`)
- Favori IP, geçmiş, lisanslama (Supabase)
- **AI Modu** (OpenRouter / Google / OpenAI / Custom; deepseek/deepseek-v4-flash default)

---

## 3. Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"
dotnet build AgTarama.slnx              # Debug build
dotnet run --project AgTarama\AgTarama.csproj
dotnet test AgTarama.slnx               # 48 test (1 bilinen fail)
dotnet build AgTarama.slnx -c Release   # Release + Obfuscar
```

Detay: [docs/tasks.md](docs/tasks.md).

---

## 4. Doküman Haritası

| Ne yapıyorsun? | Oku |
|---|---|
| Doc haritası, hızlı başlangıç | [docs/README.md](docs/README.md) |
| Stack, komut, env, csproj | [docs/project.md](docs/project.md) |
| Mimari, klasör ağacı | [docs/architecture.md](docs/architecture.md) |
| C# kuralları, async, naming | [docs/conventions.md](docs/conventions.md) |
| NuGet paketleri | [docs/nuget-packages.md](docs/nuget-packages.md) |
| Kararlar, teknik borç | [docs/decisions.md](docs/decisions.md) |
| Yaygın görevler (servis ekle, probe ekle…) | [docs/tasks.md](docs/tasks.md) |
| Test ortamı, yeni test yazma | [docs/testing.md](docs/testing.md) |
| GitHub Release prosedürü | [docs/release.md](docs/release.md) |
| Versiyon değişiklik geçmişi | [docs/CHANGELOG.md](docs/CHANGELOG.md) |
| XAML, stil, renk, sekme | [docs/ui.md](docs/ui.md) |
| MainWindow partial haritası | [docs/partials.md](docs/partials.md) |
| Core servisler | [docs/services.md](docs/services.md) |
| AI servisleri | [docs/services-ai.md](docs/services-ai.md) |
| Cihaz keşif alt sistemi | [docs/services-discovery.md](docs/services-discovery.md) |
| Lisans + güvenlik + update | [docs/licensing.md](docs/licensing.md) |

Aktif refactor sprinti: [master-refactor.md](master-refactor.md).

---

## 5. Doküman Güncelleme Politikası

**Markdown dosyaları otomatik güncellenmez.** Sadece kullanıcı açıkça `"md guncelle"` veya `"AGENTS.md'yi guncelle"` dediğinde güncellenir. Kod değişiklikleri MD'lere dokunmaz. Kullanıcı `"md guncelle"` dediğinde:

- Hangi alt dosyaların güncel olmadığını analiz et
- Sadece etkilenen dosyaları düzenle

---

## 6. Geliştirme Kuralları (özet)

- `async/await` + `CancellationToken` zorunlu; UI thread bloke edilmemeli.
- `OperationCanceledException` yutulmaz, propagate edilir.
- Panel sonuçları kendi `XxxResultPanel`'e; ana chat'e yazılmaz.
- Sekme geçişi: `MainTabControl.SelectedIndex = TabXxx`.
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`.
- Harici araç başlatma: `HariciAracBaslat(exe, ad)`. Toast: `ToastGoster(mesaj, hata:bool)`.
- **Versiyon yükseltme** yalnızca kullanıcı `"release et"` / `"versiyon yükselt"` derse.
- **Commit** kullanıcı manuel yapacak — AI otomatik commit atmaz.

Detay: [docs/conventions.md](docs/conventions.md).

---

## 7. Release Prosedürü

Kullanıcı `"github release yap"` (veya benzeri) dediğinde sırayla uygulanır.
Tam adım listesi: [docs/release.md](docs/release.md).
