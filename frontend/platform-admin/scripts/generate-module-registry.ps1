param(
    [string]$ModulesRoot = "..\..\modules",
    [string]$OutputFile = "..\src\routes\moduleRegistry.generated.tsx"
)

$ErrorActionPreference = "Stop"

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

$moduleDirs = Get-ChildItem -LiteralPath $resolvedModulesRoot -Directory | Sort-Object Name
$imports = @()
$routeSpreads = @()
$manifestItems = @()
$invalidModules = @()
$index = 0
$outputUri = [System.Uri]((Resolve-Path -LiteralPath $outputDirectory).Path.TrimEnd('\') + '\')

foreach ($moduleDir in $moduleDirs) {
    $packageJsonPath = Join-Path $moduleDir.FullName "package.json"
    if (-not (Test-Path -LiteralPath $packageJsonPath)) {
        continue
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $routesPath = Resolve-ModuleExportPath -ModuleDir $moduleDir.FullName -BaseName "routes"
    $menuPath = Resolve-ModuleExportPath -ModuleDir $moduleDir.FullName -BaseName "menu"
    $permissionsPath = Resolve-ModuleExportPath -ModuleDir $moduleDir.FullName -BaseName "permissions"

    if ($null -eq $routesPath -or $null -eq $menuPath -or $null -eq $permissionsPath) {
        $missing = @()
        if ($null -eq $routesPath) { $missing += "routes" }
        if ($null -eq $menuPath) { $missing += "menu" }
        if ($null -eq $permissionsPath) { $missing += "permissions" }
        $invalidModules += ($moduleDir.Name + " (missing: " + ($missing -join ", ") + ")")
        continue
    }

    $routeAlias = "moduleRoutes$index"
    $menuAlias = "moduleMenus$index"
    $permissionsAlias = "modulePermissions$index"

    $routeImportPath = $outputUri.MakeRelativeUri([System.Uri]$routesPath).ToString()
    $routeImportPath = [System.Text.RegularExpressions.Regex]::Replace($routeImportPath, "\.(tsx|ts|jsx|js)$", "")
    if (-not $routeImportPath.StartsWith(".")) { $routeImportPath = "./$routeImportPath" }
    $menuImportPath = $outputUri.MakeRelativeUri([System.Uri]$menuPath).ToString()
    $menuImportPath = [System.Text.RegularExpressions.Regex]::Replace($menuImportPath, "\.(tsx|ts|jsx|js)$", "")
    if (-not $menuImportPath.StartsWith(".")) { $menuImportPath = "./$menuImportPath" }
    $permissionsImportPath = $outputUri.MakeRelativeUri([System.Uri]$permissionsPath).ToString()
    $permissionsImportPath = [System.Text.RegularExpressions.Regex]::Replace($permissionsImportPath, "\.(tsx|ts|jsx|js)$", "")
    if (-not $permissionsImportPath.StartsWith(".")) { $permissionsImportPath = "./$permissionsImportPath" }

    $imports += "import { routes as $routeAlias } from `"$routeImportPath`";"
    $imports += "import { menus as $menuAlias } from `"$menuImportPath`";"
    $imports += "import { permissions as $permissionsAlias } from `"$permissionsImportPath`";"
    $routeSpreads += "  ...$routeAlias,"
    $manifestItems += @"
  {
    sourceDir: "$($moduleDir.Name)",
    packageName: "$($packageJson.name)",
    version: "$($packageJson.version)",
    moduleCode: inferModuleCode($permissionsAlias),
    routes: $routeAlias,
    menus: $menuAlias,
    permissions: $permissionsAlias,
  },
"@
    $index++
}

if ($invalidModules.Count -gt 0) {
    throw ("The following frontend modules are invalid: " + ($invalidModules -join "; "))
}

$importBlock = if ($imports.Count -gt 0) { ($imports -join "`n") + "`n" } else { "" }
$routeSpreadBlock = if ($routeSpreads.Count -gt 0) { $routeSpreads -join "`n" } else { "" }
$manifestBlock = if ($manifestItems.Count -gt 0) { $manifestItems -join "`n" } else { "" }

$content = @"
import React from "react";

$importBlock
export type ModuleRoute = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export type FrontendModuleMenu = {
  key: string;
  title: string;
  path: string;
  permission?: string | null;
};

export type FrontendModuleManifest = {
  sourceDir: string;
  packageName: string;
  version: string;
  moduleCode: string | null;
  routes: ReadonlyArray<ModuleRoute>;
  menus: ReadonlyArray<FrontendModuleMenu>;
  permissions: Record<string, string>;
};

function inferModuleCode(permissions: Record<string, string>): string | null {
  const values = Object.values(permissions ?? {}).filter(Boolean);
  const prefixes = Array.from(
    new Set(values.map((value) => String(value).split(".")[0]).filter(Boolean))
  );
  return prefixes.length === 1 ? prefixes[0] : null;
}

export const moduleRoutes: ModuleRoute[] = [
$routeSpreadBlock
];

export const frontendModules: FrontendModuleManifest[] = [
$manifestBlock
];
"@

[System.IO.File]::WriteAllText($resolvedOutputFile, $content, [System.Text.Encoding]::UTF8)
Write-Host ("Generated module registry: " + $resolvedOutputFile)
