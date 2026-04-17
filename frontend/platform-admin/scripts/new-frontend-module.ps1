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
    throw "模块目录已存在：$moduleDir"
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
    "./menu": "./menu.$scriptExt",
    "./permissions": "./permissions.$scriptExt",
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

$permissions = @"
export const permissions = {
  read: "$ModuleCode.read",
  write: "$ModuleCode.write",
} as const;
"@

$menu = @"
import { permissions } from "./permissions";

export const menus = [
  {
    key: "$ModuleCode.home",
    title: "$Name",
    path: "/modules/$ModuleCode",
    permission: permissions.read,
  },
] as const;
"@

$routesTsx = @"
import React from "react";

export function ${Name}Home() {
  return (
    <div>
      <h3>$Name</h3>
      <div style={{ color: "#667085" }}>模块页面模板。</div>
    </div>
  );
}

export const routes = [
  {
    path: "/modules/$ModuleCode",
    element: <${Name}Home />,
    permission: "$ModuleCode.read",
  },
] as const;
"@

$routesJsx = @"
import React from "react";

export function ${Name}Home() {
  return (
    <div>
      <h3>$Name</h3>
      <div style={{ color: "#667085" }}>模块页面模板。</div>
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

$index = @"
export * as routes from "./routes";
export * as menu from "./menu";
export * as permissions from "./permissions";
export * as api from "./api";
"@

Set-Content -Path (Join-Path $moduleDir "package.json") -Value $pkg -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "permissions.$scriptExt") -Value $permissions -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "menu.$scriptExt") -Value $menu -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "routes.$routeExt") -Value ($(if ($isJs) { $routesJsx } else { $routesTsx })) -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "api.$scriptExt") -Value $api -Encoding UTF8
Set-Content -Path (Join-Path $moduleDir "index.$scriptExt") -Value $index -Encoding UTF8

$registryScript = Join-Path $PSScriptRoot "generate-module-registry.ps1"
$validationScript = Join-Path $PSScriptRoot "validate-frontend-modules.ps1"
& powershell -ExecutionPolicy Bypass -File $registryScript
& powershell -ExecutionPolicy Bypass -File $validationScript

Write-Host "已创建前端模块：$moduleDir"
Write-Host "模块注册表与模块校验已自动完成。"
Write-Host "下一步：直接运行 npm run -w platform-admin dev/build。"

