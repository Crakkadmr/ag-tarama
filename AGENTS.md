# AGENTS.md — Proje Master Index

> Bu dosya AI agent'larinin projeye hizli giris noktasidir.
> Detayli referans bilgi `docs/` klasorunde konuya gore ayrilmistir.
> Son guncelleme: 2026-05-15 (v0.2.0 — Cihaz Tara genisleme: 5 yeni protokol, subnet chip picker, confidence scoring)

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
| Surum | v0.2.0 |
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
- Cihaz Tara (ONVIF+WSD, SSDP, mDNS/25-servis, Ping Sweep, NetBIOS, Advanced IP Scanner, Ubiquiti Discovery UDP-10001, MikroTik MNDP UDP-5678, SNMP sysDescr UDP-161, HTTP Fingerprint vendor-specific endpoint'ler; subnet chip picker; confidence score; QuestPDF/ClosedXML export)
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

## 5. GitHub Release Prosedürü

Kullanici **"github release yap"** (veya benzeri: "release et", "yayınla") dediginde asagidaki adimlari **sirayla ve eksiksiz** uy­gu­la:

### 5.1 Versiyon Arttirma

1. `AgTarama.csproj` dosyasindaki `<Version>`, `<AssemblyVersion>`, `<FileVersion>` degerlerini **minor basamagi 0.1 arttirarak** guncelle.
   - Ornek: `0.2.0` → `0.3.0`
2. `AGENTS.md` §1 tablosundaki `Surum` satirini ve §7 basligini guncelle.
3. Degisikligi commit et:
   ```
   chore: bump version to vX.Y.Z
   ```

### 5.2 Release Build

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"
dotnet build -c Release
```

Build basarili olmazsa duraksayip kullaniciya hata mesajini ilet; devam etme.

### 5.3 ZIP Olustur

```powershell
$ver = "X.Y.Z"   # yeni versiyon
$src = "bin\Release\net10.0-windows"
$zip = "bin\AgTarama-v$ver.zip"
Compress-Archive -Path "$src\*" -DestinationPath $zip -CompressionLevel Optimal
```

### 5.4 SHA256 Dosyasi Olustur

```powershell
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$shaFile = "bin\AgTarama-v$ver.zip.sha256"
[System.IO.File]::WriteAllText($shaFile, "$hash  AgTarama-v$ver.zip", [System.Text.UTF8Encoding]::new($false))
```

> **Zorunlu:** UpdateService, SHA dosyasi olmayan release'lerde guncelleme bulamaz (`return null`).

### 5.5 GitHub Release Olustur

```bash
gh release create "vX.Y.Z" "bin/AgTarama-vX.Y.Z.zip" "bin/AgTarama-vX.Y.Z.zip.sha256" \
  --repo Crakkadmr/ag-tarama \
  --title "vX.Y.Z — <kisa ozet>" \
  --notes "<release notlari>" \
  --latest
```

- `--latest` mutlaka ekle (UpdateService `/releases/latest` endpoint'ini kullanir).
- Release notlarina en az "Kurulum" adimi ekle.

### 5.6 Dogrulama

```bash
gh release view vX.Y.Z --repo Crakkadmr/ag-tarama --json assets --jq '.assets[].name'
```

Ciktida hem `.zip` hem `.zip.sha256` gozukmeliydi. Ikisi de varsa islemi kullaniciya bildir.

---

## 6. Dokumantasyon Guncelleme Politikasi

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

## 7. Son Degisiklik Notu (2026-05-15) — v0.2.0

**Cihaz Tara buyuk genisleme:**
- 5 yeni keşif protokolü: Ubiquiti Discovery (UDP 10001 TLV), MikroTik MNDP (UDP 5678 TLV), SNMP sysDescr/sysName (UDP 161 ASN.1), HTTP vendor-specific endpoint fingerprinting, WSD `wsdp:Device` probe.
- 5 yeni servis dosyasi: `UbiquitiDiscoveryService.cs`, `MndpDiscoveryService.cs`, `SnmpFingerprintService.cs`, `OuiVendorLookup.cs`, `HttpFingerprintService.cs`.
- Subnet chip picker: NIC'lere gore ToggleButton chip'leri (WrapPanel), "Derin tara" CheckBox, ⟳ yenile butonu.
- Confidence score (`Guven` 0-100): DataGrid'de yeni sutun.
- `KesifKaynaklari HashSet<string>`: hangi protokolun cihaziı keşfettigini izler.
- mDNS servis listesi 12 → 25 servise genisletildi.
- `MarkaTablosu` ~40 → ~100 anahtar kelimeye genisletildi.
- `KimlikBelirle` heuristikleri iyilestirildi: Linux IoT, Akilli Cihaz, Router DNS/DHCP, yazici sikilasmasi.
- `GuvenSkoru()` yeni metodu.
- `KesifSira()` yeni metodu.
- NetbiosService: `nbtstat`/`ping` ciktisi icin OEM kod sayfasi kodlamasi duzeltildi.
- HTTP 200 kontrolu `HttpBannerOku`'ya eklendi.
- `AcikPortlar` race condition duzeltildi (`KameraWebUrlSec`).
- CIDR aralik dogrulamasi genisetildi: /16-/30.
- Filtre secenekleri genisletildi: Bilinmiyor, Linux IoT, Router/AP, Erisim Noktasi, Switch, Akilli TV, Akilli Cihaz, Telefon, Hoparlor.
- DataGrid `Kesif` sutunu 130px, yeni `Guven` sutunu 60px.
- Sag tik menusune "Bu cihazi yeniden tara" eklendi.
