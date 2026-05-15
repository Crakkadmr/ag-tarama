# AGENTS.md — Proje Master Index

> Bu dosya AI agent'larinin projeye hizli giris noktasidir.
> Detayli referans bilgi `docs/` klasorunde konuya gore ayrilmistir.
> Son guncelleme: 2026-05-15 (v0.2.1 — Risk sutunu kaldirildi)

---

## 1. Proje Kimligi

| Alan | Deger |
|---|---|
| Ad | Network Sniffer (AgTarama) |
| Tip | WPF Desktop Uygulamasi |
| Hedef | .NET 10 (`net10.0-windows`), `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable` |
| csproj ek | `tools\**\*` ve `Req\**\*` -> `CopyToOutputDirectory=PreserveNewest` |
| Output | `WinExe` |
| Namespace | `AgTarama` |
| Surum | v0.2.1 |
| Branch | `guvenlik-guncellestirmeleri-zirtpirt` (main: `main`) |
| Git user | Crakkadmr |
| Kok yol | `C:\Projects\AG TARAMA PROGRAMI\AgTarama` |

---

## 2. Proje Amaci

WPF tabanli **Network Sniffer** markali chatbot arayuzlu ag tarama ve paket yakalama uygulamasi.

Ana ozellikler:
- Paket yakalama (tshark) ve Wireshark Portable ile analiz
- Ping, Port Tara (banner), Traceroute, DNS, ARP, Wake-on-LAN
- Bant genisligi monitoru (gecmis grafik + istatistik)
- Cihaz Tara (ONVIF, SSDP, mDNS, Ping Sweep, NetBIOS, Advanced IP Scanner zenginlestirme; QuestPDF/ClosedXML export)
- Wi-Fi Tarama (SSID/BSSID/sinyal/kanal, Evil-Twin tespiti)
- F12 komut konsolu (CommandRouter, `&&` zincirleme)
- Favori IP, gecmis, lisanslama (Supabase)

---

## 3. Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build                   # Debug build
dotnet run                     # Calistir
dotnet build -c Release        # Release build
```

---

## 4. Dokumantasyon Haritasi

| Ne yapiyorsun? | Oku |
|---|---|
| XAML, stil, renk, buton, sekme degisikligi | [docs/ui.md](docs/ui.md) |
| Service ekle veya degistir | [docs/services.md](docs/services.md) |
| MainWindow partial icinde metot bul / degistir | [docs/partials.md](docs/partials.md) |
| Lisans, Supabase, guvenlik, guncelleme | [docs/licensing.md](docs/licensing.md) |
| Mimari, klasor yapisi, harici bagimliliklar, kurallar | [docs/architecture.md](docs/architecture.md) |

---

## 5. Dokumantasyon Guncelleme Politikasi

**Markdown dosyalari (AGENTS.md, docs/*.md) her degisiklikte otomatik guncellenmez.**
Sadece kullanici acikca `"md guncelle"` veya `"AGENTS.md'yi guncelle"` dediginde guncellenir.
Kod degisiklikleri yapilirken bu dosyalara dokunulmaz.

Kullanici `"md guncelle"` dediginde:
- Hangi alt dosyalarin guncel olmadigini analiz et
- Sadece etkilenen dosyalari duzenle

---

## 6. Gelistirme Kurallari (ozet)

- `async/await` + `CancellationToken` zorunlu; UI thread bloke edilmemeli.
- Panel sonuclari kendi `XxxResultPanel`'e; ana chat'e yazilmaz.
- Sekme gecisi: `MainTabControl.SelectedIndex = TabXxx`.
- Stil kaynaklari yalnizca `MainWindow.xaml > Window.Resources`.
- Harici arac baslatma: `HariciAracBaslat(exe, ad)`.
- Toast: `ToastGoster(mesaj, hata:bool)`.

---

## 7. Son Degisiklik Notu (2026-05-15)

- Cihaz Tara ekranindan `Risk` sutunu kaldirildi.
- Cihaz Tara risk puani hesaplama mantigi kaldirildi.
- Cihaz Tara export/PDF ciktilarindan risk alanlari kaldirildi.
