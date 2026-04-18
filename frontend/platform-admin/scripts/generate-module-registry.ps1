param(
    [string]$ModulesRoot = "..\..\modules",
    [string]$OutputFile = "..\src\routes\moduleRegistry.generated.tsx"
)

$ErrorActionPreference = "Stop"

# 强制控制台与管道使用 UTF-8，降低跨进程输出乱码概率
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function New-UnicodeText {
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$CodePoints
    )

    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

$resolvedModulesRoot = Resolve-Path (Join-Path $PSScriptRoot $ModulesRoot)
$resolvedOutputFile = Join-Path $PSScriptRoot $OutputFile
$outputDirectory = Split-Path -Parent $resolvedOutputFile

if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

function Resolve-ModuleExportPath {
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

function Get-RelativeImportPathForRegistry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromOutputDirectory,
        [Parameter(Mandatory = $true)]
        [string]$ToRoutesFile
    )

    $fromDir = [System.IO.Path]::GetFullPath($FromOutputDirectory)
    $toFile = [System.IO.Path]::GetFullPath($ToRoutesFile)

    Push-Location -LiteralPath $fromDir
    try {
        $relative = Resolve-Path -LiteralPath $toFile -Relative
    }
    finally {
        Pop-Location
    }

    return $relative.Replace('\', '/')
}

$moduleDirs = Get-ChildItem -LiteralPath $resolvedModulesRoot -Directory | Sort-Object Name
$manifestItems = @()
$lazyRouteItems = @()
$invalidModules = @()
$routePathOwners = @{}
$moduleCodeOwners = @{}
$index = 0
$outputDirectoryFull = [System.IO.Path]::GetFullPath($outputDirectory)

foreach ($moduleDir in $moduleDirs) {
    $packageJsonPath = Join-Path $moduleDir.FullName "package.json"
    if (-not (Test-Path -LiteralPath $packageJsonPath)) {
        continue
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $routesPath = Resolve-ModuleExportPath -ModuleDir $moduleDir.FullName -BaseName "routes"

    if ($null -eq $routesPath) {
        $invalidModules += ($moduleDir.Name + " (missing: routes)")
        continue
    }

    $manifestPath = Join-Path $moduleDir.FullName "manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $invalidModules += ($moduleDir.Name + " (missing: manifest.json)")
        continue
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.moduleCode) {
        $invalidModules += ($moduleDir.Name + " (manifest missing moduleCode)")
        continue
    }
    if ($null -eq $manifest.routes -or $manifest.routes.Count -eq 0) {
        $invalidModules += ($moduleDir.Name + " (manifest missing routes)")
        continue
    }

    if ($moduleCodeOwners.ContainsKey($manifest.moduleCode)) {
        $invalidModules += ($moduleDir.Name + " (duplicate moduleCode: " + $manifest.moduleCode + ")")
        continue
    }
    $moduleCodeOwners[$manifest.moduleCode] = $moduleDir.Name

    foreach ($route in $manifest.routes) {
        if ([string]::IsNullOrWhiteSpace($route.path)) {
            $invalidModules += ($moduleDir.Name + " (manifest has empty route path)")
            continue
        }

        if ($routePathOwners.ContainsKey($route.path)) {
            $invalidModules += ($moduleDir.Name + " (duplicate route path: " + $route.path + ")")
        } else {
            $routePathOwners[$route.path] = $moduleDir.Name
        }
    }

    $routeImportPath = Get-RelativeImportPathForRegistry -FromOutputDirectory $outputDirectoryFull -ToRoutesFile $routesPath
    $routeImportPath = [System.Text.RegularExpressions.Regex]::Replace($routeImportPath, "\.(tsx|ts|jsx|js)$", "")
    if (-not $routeImportPath.StartsWith(".")) { $routeImportPath = "./$routeImportPath" }

    foreach ($manifestRoute in $manifest.routes) {
        $lazyRouteItems += @"
  {
    moduleCode: "$($manifest.moduleCode)",
    path: "$($manifestRoute.path)",
    permission: $(if ([string]::IsNullOrWhiteSpace($manifestRoute.permission)) { "null" } else { "`"$($manifestRoute.permission)`"" }),
    loadRoutes: async () => (await import("$routeImportPath")).routes as ReadonlyArray<ModuleRoute>,
  },
"@
    }
    $manifestItems += @"
  {
    sourceDir: "$($moduleDir.Name)",
    packageName: "$($packageJson.name)",
    version: "$($packageJson.version)",
    moduleCode: "$($manifest.moduleCode)",
    routes: [
$(($manifest.routes | ForEach-Object {
@"
      {
        path: "$($_.path)",
        permission: $(if ([string]::IsNullOrWhiteSpace($_.permission)) { "null" } else { "`"$($_.permission)`"" }),
      },
"@
}) -join "`n")
    ],
    routePaths: collectRoutePaths([
$(($manifest.routes | ForEach-Object { "      `"$($_.path)`"," }) -join "`n")
    ]),
    routePermissions: collectRoutePermissions([
$(($manifest.routes | ForEach-Object {
if ([string]::IsNullOrWhiteSpace($_.permission)) { "" } else { "      `"$($_.permission)`"," }
}) -join "`n")
    ]),
  },
"@
    $index++
}

if ($invalidModules.Count -gt 0) {
    $invalidPrefix = New-UnicodeText -CodePoints @(0x4EE5, 0x4E0B, 0x524D, 0x7AEF, 0x6A21, 0x5757, 0x4E0D, 0x5408, 0x6CD5)
    throw ($invalidPrefix + ": " + ($invalidModules -join "; "))
}

$manifestBlock = if ($manifestItems.Count -gt 0) { $manifestItems -join "`n" } else { "" }
$lazyRouteBlock = if ($lazyRouteItems.Count -gt 0) { $lazyRouteItems -join "`n" } else { "" }

$content = @"
import React from "react";

export type ModuleRoute = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export type FrontendModuleManifest = {
  sourceDir: string;
  packageName: string;
  version: string;
  moduleCode: string;
  routes: ReadonlyArray<Pick<ModuleRoute, "path" | "permission">>;
  routePaths: ReadonlyArray<string>;
  routePermissions: ReadonlyArray<string>;
};

export type LazyModuleRouteEntry = {
  moduleCode: string;
  path: string;
  permission?: string | null;
  loadRoutes: () => Promise<ReadonlyArray<ModuleRoute>>;
};

function collectRoutePaths(paths: ReadonlyArray<string>): string[] {
  return Array.from(new Set(paths.filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function collectRoutePermissions(permissions: ReadonlyArray<string>): string[] {
  return Array.from(new Set(permissions.filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

export const lazyModuleRouteEntries: LazyModuleRouteEntry[] = [
$lazyRouteBlock
];

export const frontendModules: FrontendModuleManifest[] = [
$manifestBlock
];
"@

[System.IO.File]::WriteAllText($resolvedOutputFile, $content, [System.Text.Encoding]::UTF8)
$successPrefix = New-UnicodeText -CodePoints @(0x6A21, 0x5757, 0x6CE8, 0x518C, 0x8868, 0x5DF2, 0x751F, 0x6210)
Write-Host ($successPrefix + ": " + $resolvedOutputFile)
