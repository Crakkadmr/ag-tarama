# AgTarama Master Refactor Planı

> Tam plan: `C:\Users\DMR\.claude\plans\projeyi-komple-analiz-et-splendid-fern.md`
> Bu dosya: faz özetleri + checklist. Tarih: 2026-05-19.

## Context

AgTarama (.NET 10 WPF, v0.4.0). Sorunlar:
1. Kökte 5 büyük eski rapor MD (bugtest, snif-test, gelistirme, cihaz-tara-refactor, csharp-project-analysis-plan) — çoğu çözüldü ama AI bunları "güncel" sanıyor.
2. `docs/` 5 dosya — `project.md`, `conventions.md`, `nuget-packages.md`, `tasks.md`, `decisions.md`, `release.md`, `testing.md` yok.
3. `.sln` yok → tek komutla build/test çalışmıyor.
4. Test kapsama dar (48 test); bugtest.md P0/P1/P2 fix'leri için regresyon yok.
5. `MainWindow.DeviceScan.cs` 1414, `MainWindow.NetworkTools.cs` 901 satır — şişmiş partial'lar.
6. `bin/` 2.0 GB; temizleme dokümante değil.

Hedef: Codex+Claude çift uyumlu granular doc ağı, tek-komut test ortamı, partial→services refactor.

---

## Faz 0 — Solution & Hazırlık

- [x] `C:\Projects\AG TARAMA PROGRAMI\AgTarama.sln` oluştur (AgTarama + AgTarama.Tests).
- [x] Kök `.gitignore` (`**/bin/`, `**/obj/`, `*.user`, `.vs/`).
- [x] `dotnet build AgTarama.sln` + `dotnet test AgTarama.sln` doğrula (48 test).

## Faz 1 — MD Restructure

**Yeni `docs/` şeması (5 → 15 dosya):**

| Dosya | Durum |
|---|---|
| `docs/README.md` | yeni |
| `docs/project.md` | yeni (AGENTS §1-3) |
| `docs/architecture.md` | güncelle |
| `docs/conventions.md` | yeni |
| `docs/nuget-packages.md` | yeni |
| `docs/services.md` | güncelle (Discovery + Ai çıkar) |
| `docs/services-discovery.md` | yeni |
| `docs/services-ai.md` | yeni |
| `docs/partials.md` | güncelle |
| `docs/ui.md` | mevcut |
| `docs/licensing.md` | mevcut |
| `docs/tasks.md` | yeni |
| `docs/testing.md` | yeni (Faz 3 ile) |
| `docs/decisions.md` | yeni |
| `docs/release.md` | yeni (AGENTS §5 taşı) |
| `docs/CHANGELOG.md` | yeni (AGENTS §7,§8 taşı) |

**Silinecek (destile et → docs/'a aktar → sil):**
- [x] `bugtest.md`
- [x] `snif-test.md`
- [x] `cihaz-tara-refactor.md`
- [x] `gelistirme.md`
- [x] `csharp-project-analysis-plan.md`

**Kök güncellemeler:**
- [x] `AGENTS.md` 223 → ~80 satır (Codex master index; §5 release ve §7-8 changelog taşınır).
- [x] `CLAUDE.md` — `AGENTS.md` + `docs/README.md`'ye yönlendir.
- [x] `README.md` doc linkleri tazele.

**Doğrulama:** Kökte `.md` = 4 dosya (`AGENTS.md`, `CLAUDE.md`, `README.md`, `master-refactor.md`).

## Faz 2 — Proje Temizliği

- [x] `bin/`/`obj/` temizleme komutu `docs/tasks.md`'de.
- [x] `app.ico` çift kayıt kontrol (ApplicationIcon + Resource).
- [x] `tools/security/` korunur.
- [x] `supabase/` içerik kontrolü.

## Faz 3 — Test Ortamı

- [x] Kök `test.ps1` oluştur (param: `-Coverage`, `-Filter`).
- [x] `CidrParser` extract et (`Services/Net/CidrParser.cs`) — `MainWindow.DeviceScan.cs > SubnetGirdisiniCoz` taşı.
- [x] Yeni test sınıfları:
  - [x] `AiUsageMeterTests.cs` (lock, paralel race)
  - [x] `AiClientTests.cs` (iptal propagate, retry)
  - [x] `CidrParserTests.cs` (/31, /32, /30)
  - [x] `SettingsValidatorTests.cs` (https zorunlu)
  - [x] `SafeExtractZipTests.cs` (Zip Slip, limit)
  - [x] `DeviceClassifierTests.cs` (routerboard, TP-Link OUI)
- [x] `OuiVendorLookupTests.cs` genişlet (00:00:00 phantom guard ekle).
- [x] Hedef: 48 → ~110 test, hepsi yeşil.
- [x] `docs/testing.md` yaz.

## Faz 4 — Mimari Refactor

**`Partials/MainWindow.DeviceScan.cs` 1414 → ~400 satır:**
- [x] `Services/Net/CidrParser.cs` (Faz 3'te)
- [x] `Services/DeviceExportService.cs` (CSV/XLSX/PDF; PdfReportService base)
- [x] `Partials/MainWindow.DeviceScan.Ai.cs` (AI chip + window)
- [x] `Partials/MainWindow.DeviceScan.Row.cs` (KameraSatir)
- [x] `Partials/MainWindow.DeviceScan.SubnetPicker.cs`

**`Partials/MainWindow.NetworkTools.cs` 901 → ~350 satır:**
- [x] `Partials/MainWindow.Tools.Ping.cs`
- [x] `Partials/MainWindow.Tools.PortScan.cs`
- [x] `Partials/MainWindow.Tools.Misc.cs` (Traceroute/DNS/ARP/WoL)
- [x] `MesajEkle` → `MainWindow.UI.cs`'e taşı.

**Kurallar:** Aynı `public partial class MainWindow`; services UI-bağımsız; **MVVM YAPMA**.

**Doğrulama:** Hiçbir partial > 600 satır.

## Faz 5 — Finalize

- [x] `AGENTS.md` §4 doc haritasını 15-dosyalı şemaya uyarla.
- [x] `docs/CHANGELOG.md` "v0.4.1 — doc/test/architecture refactor" girdisi (versiyon BUMPLAMA).
- [x] **Commit yapma** — kullanıcı manuel test edip kendi commit'leyecek.

---

## End-to-End Doğrulama

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"
dotnet build AgTarama.sln                          # 0 hata
.\test.ps1                                          # ~110 test yeşil
dotnet run --project AgTarama\AgTarama.csproj      # UI açılır
```

Manuel: Cihaz Tara (/24) + export, Wi-Fi sekmesi, F12 konsol, AI sohbet.

## Sıralama

Faz 0 → (1 ∥ 2) → 3 → 4 → 5. Faz 3 Faz 4'ten önce (regresyon ağı).
