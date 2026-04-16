$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$requiredDirs = @(
    "src/design/tokens",
    "src/design/theme",
    "src/components/base",
    "src/components/patterns",
    "src/docs/design-system"
)

$missing = @()
foreach ($dir in $requiredDirs) {
    $full = Join-Path $root $dir
    if (-not (Test-Path $full)) {
        $missing += $dir
    }
}

if ($missing.Count -gt 0) {
    Write-Error ("Missing design-system directories: " + ($missing -join ", "))
}

$sourceRoot = Join-Path $root "src"
$files = Get-ChildItem -Path $sourceRoot -Recurse -File -Include *.ts,*.tsx,*.js,*.jsx |
    Where-Object { $_.FullName -notmatch "\\src\\docs\\design-system\\" -and $_.FullName -notmatch "\\src\\api\\sdk\\" }

$forbiddenHits = @()
foreach ($file in $files) {
    $matches = Select-String -Path $file.FullName -Pattern 'from\s+[''"]antd[''"]|from\s+[''"]@ant-design/icons[''"]' -AllMatches
    if ($matches) {
        $forbiddenHits += $file.FullName
    }
}

if ($forbiddenHits.Count -gt 0) {
    $unique = $forbiddenHits | Select-Object -Unique
    Write-Error ("Direct antd/@ant-design/icons imports detected: " + ($unique -join "; "))
}

Write-Host "Design system compliance check passed."

