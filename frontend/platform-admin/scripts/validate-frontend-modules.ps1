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

function Validate-ModuleStylingRules {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo]$ModuleDir,
        [System.Collections.Generic.List[string]]$Errors
    )

    $styleImportPattern = "import\s+[""'][^""']+\.(css|scss|sass|less)[""']"
    $inlineStylePattern = 'style\s*=\s*\{\{'
    $viewFiles = Get-ChildItem -LiteralPath $ModuleDir.FullName -Recurse -File -Include *.tsx,*.jsx
    foreach ($viewFile in $viewFiles) {
        $content = Get-Content -LiteralPath $viewFile.FullName -Raw

        if ($content -match $styleImportPattern) {
            $relativePath = $viewFile.FullName.Substring($ModuleDir.FullName.Length + 1)
            $Errors.Add($ModuleDir.Name + ": file '" + $relativePath + "' cannot import local style files; use global styles/components instead")
        }

        if ($content -match $inlineStylePattern) {
            $relativePath = $viewFile.FullName.Substring($ModuleDir.FullName.Length + 1)
            $Errors.Add($ModuleDir.Name + ": file '" + $relativePath + "' cannot use inline style; use global classes/components instead")
        }
    }
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
        $moduleCodePattern = "export\s+const\s+moduleCode\s*=\s*[""'](?<moduleCode>[^""']+)[""']"
        if ($routesContent -notmatch $moduleCodePattern) {
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

    Validate-ModuleStylingRules -ModuleDir $moduleDir -Errors $errors
}

if ($errors.Count -gt 0) {
    throw ("Frontend module validation failed:`n - " + ($errors -join "`n - "))
}

Write-Host "Frontend module validation passed."
