# NuGet Paket Referansı

## AgTarama (ana proje)

| Paket | Versiyon | Amaç |
|---|---|---|
| `System.Management` | 9.0.5 | WMI sorguları (NIC/sistem bilgisi) |
| `QuestPDF` | 2024.12.* | Cihaz Tara PDF raporu (A4 yatay, 11 sütun) |
| `ClosedXML` | 0.102.* | Cihaz Tara XLSX export |
| `SharpPcap` | 6.3.0 | `PassivePacketSniffer` listener — pasif MAC keşfi |
| `PacketDotNet` | 1.4.7 | SharpPcap paket parser |
| `Obfuscar` | 2.2.38 | Release-only post-build obfuscator (`ContinueOnError=false`) |

**Kaldırılan:** `Lextm.SharpSnmpLib` — SNMPv1 artık `SnmpFingerprintService` + `CommandRouter` içinde manuel ASN.1 DER ile yapılıyor (NuGet bağımsız).

## AgTarama.Tests

| Paket | Versiyon | Amaç |
|---|---|---|
| `Microsoft.NET.Test.Sdk` | 17.11.1 | Test host |
| `xunit` | 2.9.2 | Test framework |
| `xunit.runner.visualstudio` | 2.8.2 | VS / `dotnet test` runner |
| `coverlet.collector` | 6.0.2 | Code coverage |

## Önemli Paket Notları

- **QuestPDF lisans:** Community license statik constructor'da set edilir (`PdfReportService` içinde `QuestPDF.Settings.License = LicenseType.Community`).
- **SharpPcap:** Npcap sürücüsü gerekli (`PcapHelper.IsNpcapAvailable`); Npcap yoksa `PassivePacketSniffer` listener atlanır.
- **Obfuscar:** Sadece `Configuration=Release` koşulunda referans edilir; Debug build'de NuGet çekilmez.
- **Wildcard versiyon (`2024.12.*`, `0.102.*`):** restore sırasında son patch çekilir; reproducible build için CI üzerinde `--locked-mode` veya `packages.lock.json` düşünülebilir (henüz uygulanmadı).
- **NU1903 uyarısı:** `System.IO.Packaging 6.0.0` transitif (Test SDK üzerinden) — yüksek önem dereceli CVE; ana projede etkisi yok. [decisions.md > Teknik Borç](decisions.md) altında listeli.

## Yeni Paket Ekleme

```powershell
dotnet add AgTarama/AgTarama.csproj package <PaketAdı>
dotnet add AgTarama/AgTarama.csproj package <PaketAdı> --version x.y.z
```
