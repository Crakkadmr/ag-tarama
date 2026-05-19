# AgTarama — Proje Bağlamı

## Ne Yapıyor

WPF tabanlı **Network Sniffer** markalı, chatbot arayüzlü ağ tarama ve paket yakalama uygulaması. tshark ile pcap yakalama, Wireshark Portable ile analiz; ping/port/traceroute/DNS/ARP/WoL/bant genişliği; cihaz tara (ONVIF, SSDP, mDNS, SNMP, MNDP, Ubiquiti, HTTP fingerprint); Wi-Fi tarama (Evil-Twin); F12 komut konsolu; AI sohbet + pcap/cihaz analizi; Supabase lisanslama.

## Teknik Kimlik

| Alan | Değer |
|---|---|
| Tip | WPF Desktop Uygulaması (Single Window) |
| TargetFramework | `net10.0-windows` |
| C# | 13 (`ImplicitUsings=enable`, `Nullable=enable`) |
| SDK | `Microsoft.NET.Sdk` (`UseWPF=true`) |
| OutputType | `WinExe` |
| Solution | `C:\Projects\AG TARAMA PROGRAMI\AgTarama.slnx` (.NET 10 slnx format) |
| Namespace | `AgTarama` |
| Sürüm | v0.4.0 |
| Branch | `bugveyeniozellikler` (main: `main`) |
| Git user | Crakkadmr |
| Repo | `Crakkadmr/ag-tarama` |
| Mimari | Tek pencere, MVVM yok; `MainWindow` + 12 partial + `Services/` |
| Test | xUnit 2.9.2 (`AgTarama.Tests`) — 48 test |

## Projeler

| Proje | Tip | Yol |
|---|---|---|
| `AgTarama` | WinExe (WPF) | `AgTarama/AgTarama.csproj` |
| `AgTarama.Tests` | xUnit | `AgTarama.Tests/AgTarama.Tests.csproj` |

`AgTarama.Tests` → `<ProjectReference Include="..\AgTarama\AgTarama.csproj" />` + `AgTarama.csproj`'da `InternalsVisibleTo("AgTarama.Tests")` ile internal türlere erişir.

## csproj Özellikleri

```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net10.0-windows</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<UseWPF>true</UseWPF>
<ApplicationIcon>app.ico</ApplicationIcon>
```

- `tools\**\*` ve `Req\**\*` → `CopyToOutputDirectory=PreserveNewest` (runtime araçları output'a kopyalanır).
- Release post-build target: `ObfuscarPostBuild` (Obfuscar 2.2.38) + `VerifyBundledToolHashes` (hash allowlist doğrulama).

## Komutlar

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"

# Build / Run
dotnet build AgTarama.slnx                          # Debug
dotnet build AgTarama.slnx -c Release               # Release + Obfuscar
dotnet run --project AgTarama\AgTarama.csproj

# Test
dotnet test AgTarama.slnx --nologo
.\test.ps1                                           # (Faz 3 sonrası)
```

Release prosedürü: [release.md](release.md).

## Persist Edilen Dosyalar (`%APPDATA%\AgTarama\`)

| Dosya | İçerik |
|---|---|
| `settings.json` | `AppSettings` (genel + AI + Wi-Fi) |
| `favorites.json` | Favori IP'ler (normalized) |
| `history\*.json` | Tarama geçmişi |
| `logs\YYYYMMDD.log` | Günlük log |
| `ai.vault` | DPAPI+AES şifrelenmiş AI API key (binary) |
| `ai.usage.json` | Günlük/aylık AI token sayacı |
| `tg.dat` | TrustedTime floor (AES-CBC+HMAC) |
| `license.cache` | Lisans önbelleği (AES) |

## Ortam Değişkenleri

| Anahtar | Zorunlu | Açıklama |
|---|---|---|
| `AGT_UPDATE_SIGNER_THUMBPRINT` | hayır | Update ZIP imza thumbprint pinning; set değilse imza doğrulaması atlanır + log uyarısı |

## Harici Runtime Bağımlılıkları

| Dosya | Yer | Otomatik mi? |
|---|---|---|
| `tshark.exe` | `tools\WiresharkPortable64\App\Wireshark\` | Manuel |
| `npcap-1.88.exe` | `Req\` | İlk açılış UAC |
| `advanced_ip_scanner_console.exe` | `tools\Ip_Scanner\` | Cihaz Tara içinden |
| `sadptool.exe` | `tools\sadp\` | Manuel |
