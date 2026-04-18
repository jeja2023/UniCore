param(
    [string]$ModulesRoot = "..\..\modules"
)

$ErrorActionPreference = "Stop"

$resolvedModulesRoot = Resolve-Path (Join-Path $PSScriptRoot $ModulesRoot)
$moduleDirs = Get-ChildItem -LiteralPath $resolvedModulesRoot -Directory | Sort-Object Name
$errors = New-Object System.Collections.Generic.List[string]
$moduleCodes = @{}
$routePaths = @{}

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

    foreach ($baseName in @("routes", "api", "index")) {
        $file = Find-ModuleFile -ModuleDir $moduleDir.FullName -BaseName $baseName
        if ($null -eq $file) {
            $errors.Add($moduleDir.Name + ": missing file for export '" + $baseName + "'")
        }
    }

    $manifestPath = Join-Path $moduleDir.FullName "manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $errors.Add($moduleDir.Name + ": missing required manifest.json")
        continue
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.moduleCode) {
        $errors.Add($moduleDir.Name + ": manifest.json must define moduleCode")
    } elseif ($moduleCodes.ContainsKey($manifest.moduleCode)) {
        $errors.Add($moduleDir.Name + ": duplicate moduleCode '" + $manifest.moduleCode + "' (already used by " + $moduleCodes[$manifest.moduleCode] + ")")
    } else {
        $moduleCodes[$manifest.moduleCode] = $moduleDir.Name
    }

    if ($null -eq $manifest.routes -or $manifest.routes.Count -eq 0) {
        $errors.Add($moduleDir.Name + ": manifest.json routes must contain at least one route")
    } else {
        foreach ($route in $manifest.routes) {
            if ([string]::IsNullOrWhiteSpace($route.path)) {
                $errors.Add($moduleDir.Name + ": manifest route path cannot be empty")
                continue
            }

            if ($routePaths.ContainsKey($route.path)) {
                $errors.Add($moduleDir.Name + ": duplicate route path '" + $route.path + "' (already used by " + $routePaths[$route.path] + ")")
            } else {
                $routePaths[$route.path] = $moduleDir.Name
            }
        }
    }

    $routesPath = Find-ModuleFile -ModuleDir $moduleDir.FullName -BaseName "routes"
    if ($null -ne $routesPath) {
        $routesContent = Get-Content -LiteralPath $routesPath -Raw
        if ($routesContent -notmatch 'export\s+const\s+moduleCode\s*=\s*["''](?<moduleCode>[^"'']+)["'']') {
            $errors.Add($moduleDir.Name + ": routes export must define 'moduleCode'")
        } elseif ($manifest.moduleCode -and $Matches["moduleCode"] -ne $manifest.moduleCode) {
            $errors.Add($moduleDir.Name + ": routes moduleCode does not match manifest.json moduleCode")
        }
    }

    if ($null -eq $packageJson.exports) {
        $errors.Add($moduleDir.Name + ": package.json exports is required")
        continue
    }

    foreach ($exportKey in @("./routes", "./api", ".")) {
        if ($null -eq $packageJson.exports.$exportKey) {
            $errors.Add($moduleDir.Name + ": package.json exports is missing '" + $exportKey + "'")
        }
    }
}

if ($errors.Count -gt 0) {
    throw ("Frontend module validation failed:`n - " + ($errors -join "`n - "))
}

Write-Host "Frontend module validation passed."
