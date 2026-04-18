param(
    [string]$OpenApiUrl = "http://localhost:5000/swagger/v1/swagger.json",
    [string]$OutputFile = "../src/api/sdk/unicore-sdk.ts"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedOutputFile = if ([System.IO.Path]::IsPathRooted($OutputFile)) { $OutputFile } else { Join-Path $scriptDir $OutputFile }
$outputDirectory = Split-Path -Parent $resolvedOutputFile

if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$packageJsonPath = Join-Path (Split-Path -Parent $scriptDir) "package.json"
$packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
$openApiTypescriptVersion = $packageJson.devDependencies."openapi-typescript"
if ([string]::IsNullOrWhiteSpace($openApiTypescriptVersion)) {
    throw "devDependencies.openapi-typescript is not configured in $packageJsonPath."
}
# 必须与 check-sdk-up-to-date.ps1 / CI 保持一致（语义化版本范围通过 TrimStart 固定到次版本）。
$resolvedVersion = $openApiTypescriptVersion.TrimStart("^~")

Write-Host "正在基于 $OpenApiUrl 使用 openapi-typescript@$resolvedVersion 生成 TypeScript SDK ..."
npx --yes ("openapi-typescript@" + $resolvedVersion) $OpenApiUrl -o $resolvedOutputFile
Write-Host "SDK 已生成到 $resolvedOutputFile"
