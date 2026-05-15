# AGENTS.md — Proje Master Index

> Bu dosya AI agent'larının projeye hızlı giriş noktasıdır.
> Detaylı referans bilgi `docs/` klasöründe konuya göre ayrılmıştır.
> Son güncelleme: 2026-05-15 (v0.2.0 — #10 bant grafiği, #12 QuestPDF/ClosedXML, #13 F12 konsol, #14 lisans UI, #11 Wi-Fi tarama)

---

## 1. Proje Kimliği

| Alan | Değer |
|---|---|
| Ad | Network Sniffer (AgTarama) |
| Tip | WPF Desktop Uygulaması |
| Hedef | .NET 10 (`net10.0-windows`), `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable` |
| csproj ek | `tools\**\*` ve `Req\**\*` → `CopyToOutputDirectory=PreserveNewest` |
| Output | `WinExe` |
| Namespace | `AgTarama` |
| Sürüm | v0.2.0 |
| Branch | `guvenlik-guncellestirmeleri-zirtpirt` (main: `main`) |
| Git user | Crakkadmr |
| Kök yol | `C:\Projects\AG TARAMA PROGRAMI\AgTarama` |

---

## 2. Proje Amacı

WPF tabanlı **Network Sniffer** markalı chatbot arayüzlü ağ tarama ve paket yakalama uygulaması. tshark ile paket yakalar, Wireshark Portable ile analiz eder. Ek araçlar: ping, port tara (banner tespiti), traceroute, DNS, ARP, Wake-on-LAN, bant genişliği monitörü (geçmiş grafik + istatistik), Cihaz Tara (çok protokollü keşif: ONVIF, SSDP, mDNS, Ping Sweep, NetBIOS, Advanced IP Scanner zenginleştirme; QuestPDF/ClosedXML export), Wi-Fi Tarama (SSID/BSSID/sinyal/kanal, Evil-Twin tespiti), F12 komut konsolu (CommandRouter, 15 komut, `&&` zincirleme), favori IP, geçmiş, lisanslama (Supabase).

---

## 3. Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build                   # Debug build
dotnet run                     # Çalıştır
dotnet build -c Release        # Release build
```

---

## 4. Dokümantasyon Haritası

| Ne yapıyorsun? | Oku |
|---|---|
| XAML, stil, renk, buton, sekme değişikliği | [docs/ui.md](docs/ui.md) |
| Service ekle veya değiştir | [docs/services.md](docs/services.md) |
| MainWindow partial içinde metot bul / değiştir | [docs/partials.md](docs/partials.md) |
| Lisans, Supabase, güvenlik, güncelleme | [docs/licensing.md](docs/licensing.md) |
| Mimari, klasör yapısı, harici bağımlılıklar, kurallar | [docs/architecture.md](docs/architecture.md) |

---

## 5. Dokümantasyon Güncelleme Politikası

**Markdown dosyaları (AGENTS.md, docs/*.md) her değişiklikte otomatik güncellenmez.**
Sadece kullanıcı açıkça `"md güncelle"` veya `"AGENTS.md'yi güncelle"` dediğinde güncellenir.
Kod değişiklikleri yapılırken bu dosyalara dokunulmaz — bilgi git diff ve kaynak dosyalardan elde edilebilir.

Kullanıcı `"md güncelle"` dediğinde: hangi alt dosyaların güncel olmadığını analiz et, sadece etkilenen dosyaları düzenle.

**Bu kural hem Claude Code hem Codex CLI için geçerlidir.**

---

## 6. Geliştirme Kuralları (özet)

- `async/await` + `CancellationToken` zorunlu; UI thread bloke edilmemeli.
- Panel sonuçları kendi `XxxResultPanel`'e — ana chat'e **yazılmaz**. Chat: `MesajEkle("sonuc", ...)`.
- Sekme geçişi: `MainTabControl.SelectedIndex = TabXxx` (sabitler `MainWindow.xaml.cs`'de).
- Stil kaynakları yalnızca `MainWindow.xaml > Window.Resources`. `ActiveActionButton`, `ActionButton`'dan **SONRA**.
- Harici araç başlatma: `HariciAracBaslat(exe, ad)`. Toast: `ToastGoster(mesaj, hata:bool)`.
- Detaylı kurallar: [docs/architecture.md](docs/architecture.md)
