param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,
    [Parameter(Mandatory = $true)]
    [string]$AllowListPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AllowListPath)) {
    throw "Allowlist file not found: $AllowListPath"
}

$lines = Get-Content -LiteralPath $AllowListPath |
    Where-Object { $_.Trim().Length -gt 0 -and -not $_.Trim().StartsWith("#") }

if ($lines.Count -eq 0) {
    throw "Allowlist file is empty: $AllowListPath"
}

$failed = $false
foreach ($line in $lines) {
    $parts = $line -split "\s+", 2
    if ($parts.Count -ne 2) {
        throw "Invalid allowlist line: $line"
    }

    $expected = $parts[0].Trim().ToUpperInvariant()
    $relPath = $parts[1].Trim()
    $absPath = Join-Path $ProjectRoot $relPath

    if (-not (Test-Path -LiteralPath $absPath)) {
        Write-Error "Missing file: $relPath"
        $failed = $true
        continue
    }

    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $absPath).Hash.ToUpperInvariant()
    if ($actual -ne $expected) {
        Write-Error "Hash mismatch: $relPath expected=$expected actual=$actual"
        $failed = $true
    }
}

if ($failed) {
    throw "Bundled tool hash verification failed."
}

Write-Host "Bundled tool hash verification succeeded."
