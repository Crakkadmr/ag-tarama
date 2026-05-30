# GitHub Release Prosedürü

Kullanıcı **"github release yap"** (veya benzeri: "release et", "yayınla", "versiyon yükselt") dediğinde adımları sırayla ve eksiksiz uygula.

## 1. Versiyon Arttırma

1. `AgTarama/AgTarama.csproj` dosyasındaki `<Version>`, `<AssemblyVersion>`, `<FileVersion>` değerlerini **minor basamağı 0.1 arttırarak** güncelle.
   - Örnek: `0.4.0` → `0.5.0`
2. `AGENTS.md` §1 tablosundaki `Sürüm` satırını ve `docs/CHANGELOG.md` üst başlığını güncelle.
3. Değişikliği commit et:
   ```
   chore: bump version to vX.Y.Z
   ```

> **Önemli:** Versiyon kullanıcı açıkça istemeden değişmez. "md güncelle" gibi diğer komutlar versiyona dokunmaz.

## 2. Release Build

```powershell
cd "C:\Projects\AG TARAMA PROGRAMI"
dotnet build AgTarama.slnx -c Release
```

Release post-build:
- `VerifyBundledToolHashes` — `tools/security/verify-bundled-hashes.ps1` ile bundled tool hash doğrulaması (allowlist: `tools/security/hashes.allowlist.sha256`).
- `ObfuscarPostBuild` — `obfuscar.xml` ile output obfuscate (`ContinueOnError=false`).

Build başarılı olmazsa durakla, kullanıcıya hata mesajını ilet, devam etme.

## 3. ZIP Oluştur

```powershell
$ver = "X.Y.Z"   # yeni versiyon
$src = "AgTarama\bin\Release\net10.0-windows"
$zip = "AgTarama\bin\AgTarama-v$ver.zip"
Compress-Archive -Path "$src\*" -DestinationPath $zip -CompressionLevel Optimal
```

## 4. SHA256 Dosyası Oluştur

```powershell
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$shaFile = "AgTarama\bin\AgTarama-v$ver.zip.sha256"
[System.IO.File]::WriteAllText($shaFile, "$hash  AgTarama-v$ver.zip", [System.Text.UTF8Encoding]::new($false))
```

> **Zorunlu:** `UpdateService` SHA dosyası olmayan release'lerde güncelleme bulamaz (`return null`).

## 5. GitHub Release Oluştur

```bash
gh release create "vX.Y.Z" "AgTarama/bin/AgTarama-vX.Y.Z.zip" "AgTarama/bin/AgTarama-vX.Y.Z.zip.sha256" \
  --repo Crakkadmr/ag-tarama \
  --title "vX.Y.Z — <kısa özet>" \
  --notes "<release notları>" \
  --latest
```

- `--latest` mutlaka ekle (`UpdateService` `/releases/latest` endpoint'ini kullanır).
- Release notlarına en az "Kurulum" adımı ekle.

## 6. Doğrulama

```bash
gh release view vX.Y.Z --repo Crakkadmr/ag-tarama --json assets --jq '.assets[].name'
```

Çıktıda hem `.zip` hem `.zip.sha256` görünmeli. İkisi de varsa işlemi kullanıcıya bildir.

## 7. İmza Pinning (opsiyonel ama önerilen)

Üretim deploy için kullanıcı tarafında:

```powershell
[System.Environment]::SetEnvironmentVariable("AGT_UPDATE_SIGNER_THUMBPRINT", "<sha1-thumbprint>", "User")
```

Set edilmemişse `UpdateService` imza doğrulamasını atlar ve log uyarısı yazar (`docs/licensing.md`).
