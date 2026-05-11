#requires -Version 5.1
# PostToolUse hook: Edit/Write sonrası kaynak dosya değiştiyse Claude'a
# DETAY.md guncellemesi gerektigini hatirlatir (additionalContext ile).

$ErrorActionPreference = 'Stop'

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
    $payload = $raw | ConvertFrom-Json
} catch {
    exit 0
}

$file = $null
if ($payload.tool_input -and $payload.tool_input.file_path) {
    $file = [string]$payload.tool_input.file_path
}
if (-not $file) { exit 0 }

# DETAY.md/CLAUDE.md/README.md/MEMORY/.claude kendisi degisirse tetikleme
if ($file -match '(?i)(DETAY|CLAUDE|README|MEMORY)\.md$') { exit 0 }
if ($file -match '(?i)\\\.claude\\') { exit 0 }
if ($file -match '(?i)\\(bin|obj)\\') { exit 0 }

# Yalnizca kaynak/proje dosyalari icin tetikle
$kaynakMi = $file -match '(?i)\.(cs|xaml|csproj|sln)$'
if (-not $kaynakMi) { exit 0 }

$mesaj = "Kaynak dosya degistirildi: $file`n" +
        "CLAUDE.md kurali geregi DETAY.md'yi ayni turda guncelle:`n" +
        " - 'Son guncelleme:' tarihini bugune cek`n" +
        " - Degisikligi yansitan bolumu duzenle (Klasor Yapisi / Mimari / 6.x metot haritasi / TODO / Git Durumu)`n" +
        " - Ilgili satir numaralari kaydiysa yaklasiklarini guncelle"

$out = @{
    hookSpecificOutput = @{
        hookEventName     = 'PostToolUse'
        additionalContext = $mesaj
    }
} | ConvertTo-Json -Compress -Depth 5

Write-Output $out
exit 0
