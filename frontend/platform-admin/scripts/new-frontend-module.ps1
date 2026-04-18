param(
    [Parameter(Mandatory = $true)]
    [string]$Name,
    [Parameter(Mandatory = $true)]
    [string]$ModuleCode,
    [switch]$UseJavaScript
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$modulesRoot = Join-Path $workspaceRoot "modules"
$moduleDir = Join-Path $modulesRoot $Name

if (-not (Test-Path $modulesRoot)) {
    New-Item -Path $modulesRoot -ItemType Directory | Out-Null
}

if (Test-Path $moduleDir) {
    throw "Module directory already exists: $moduleDir"
}

New-Item -Path $moduleDir -ItemType Directory | Out-Null

$isJs = $UseJavaScript.IsPresent
$routeExt = if ($isJs) { "jsx" } else { "tsx" }
$scriptExt = if ($isJs) { "js" } else { "ts" }

$pkg = @"
{
  "name": "@unicore/$Name",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "exports": {
    "./routes": "./routes.$routeExt",
    "./api": "./api.$scriptExt",
    ".": "./index.$scriptExt"
  },
  "peerDependencies": {
    "react": "^18.3.1"
  },
  "devDependencies": {
    "@types/react": "^18.3.5"
  }
}
"@

$routesTsx = @"
import React from "react";

export type ModuleRouteDefinition = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export const moduleCode = "$ModuleCode";

export function ${Name}Home() {
  return (
    <div>
      <h3>$Name</h3>
      <div style={{ color: "#667085" }}>Module page template.</div>
    </div>
  );
}

export const routes = [
  {
    path: "/modules/$ModuleCode",
    element: <${Name}Home />,
    permission: "$ModuleCode.read",
  },
] as const satisfies readonly ModuleRouteDefinition[];
"@

$routesJsx = @"
import React from "react";

export const moduleCode = "$ModuleCode";

export function ${Name}Home() {
  return (
    <div>
      <h3>$Name</h3>
      <div style={{ color: "#667085" }}>Module page template.</div>
    </div>
  );
}

export const routes = [
  {
    path: "/modules/$ModuleCode",
    element: <${Name}Home />,
    permission: "$ModuleCode.read",
  },
];
"@

$api = @"
export async function ping() {
  return { ok: true };
}
"@

$manifest = @"
{
  "moduleCode": "$ModuleCode",
  "routes": [
    {
      "path": "/modules/$ModuleCode",
      "permission": "$ModuleCode.read"
    }
  ]
}
"@

$index = @"
export * as routes from "./routes";
export * as api from "./api";
"@

Set-Content -Path (Join-Path $moduleDir "package.json") -Value $pkg -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "manifest.json") -Value $manifest -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "routes.$routeExt") -Value ($(if ($isJs) { $routesJsx } else { $routesTsx })) -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "api.$scriptExt") -Value $api -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "index.$scriptExt") -Value $index -Encoding UTF8

$registryScript = Join-Path $PSScriptRoot "generate-module-registry.ps1"
$validationScript = Join-Path $PSScriptRoot "validate-frontend-modules.ps1"
& powershell -ExecutionPolicy Bypass -File $registryScript
& powershell -ExecutionPolicy Bypass -File $validationScript

Write-Host "Created frontend module: $moduleDir"
Write-Host "Module registry and validation completed."
Write-Host "Next step: run npm run -w platform-admin dev/build"
