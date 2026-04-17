param(
    [string]$ModulesRoot = "..\..\modules"
)

$ErrorActionPreference = "Stop"

$resolvedModulesRoot = Resolve-Path (Join-Path $PSScriptRoot $ModulesRoot)
$moduleDirs = Get-ChildItem -LiteralPath $resolvedModulesRoot -Directory | Sort-Object Name
$errors = New-Object System.Collections.Generic.List[string]

function Find-ModuleFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleDir,
        [Parameter(Mandatory = $true)]
        [string]$BaseName
    )

    foreach ($extension in @(".tsx", ".ts", ".jsx", ".js")) {
        $candidate = Join-Path $ModuleDir ($BaseName + $extension)
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

foreach ($moduleDir in $moduleDirs) {
    $packageJsonPath = Join-Path $moduleDir.FullName "package.json"
    if (-not (Test-Path -LiteralPath $packageJsonPath)) {
        continue
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json

    if (-not $packageJson.name -or -not ([string]$packageJson.name).StartsWith("@unicore/")) {
        $errors.Add($moduleDir.Name + ": package.json name must start with @unicore/")
    }

    if (-not $packageJson.version) {
        $errors.Add($moduleDir.Name + ": package.json version is required")
    }

    foreach ($baseName in @("routes", "menu", "permissions", "api", "index")) {
        $file = Find-ModuleFile -ModuleDir $moduleDir.FullName -BaseName $baseName
        if ($null -eq $file) {
            $errors.Add($moduleDir.Name + ": missing file for export '" + $baseName + "'")
        }
    }

    if ($null -eq $packageJson.exports) {
        $errors.Add($moduleDir.Name + ": package.json exports is required")
        continue
    }

    foreach ($exportKey in @("./routes", "./menu", "./permissions", "./api", ".")) {
        if ($null -eq $packageJson.exports.$exportKey) {
            $errors.Add($moduleDir.Name + ": package.json exports is missing '" + $exportKey + "'")
        }
    }
}

if ($errors.Count -gt 0) {
    throw ("Frontend module validation failed:`n - " + ($errors -join "`n - "))
}

Write-Host "Frontend module validation passed."
