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
# Keep aligned with check-sdk-up-to-date.ps1 / CI (TrimStart pins semver range to minor version).
$resolvedVersion = $openApiTypescriptVersion.TrimStart("^~")

Write-Host "Generating TypeScript SDK from $OpenApiUrl using openapi-typescript@$resolvedVersion ..."
npx --yes ("openapi-typescript@" + $resolvedVersion) $OpenApiUrl -o $resolvedOutputFile
if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate SDK from $OpenApiUrl. Ensure backend Swagger is reachable and retry."
}

if (-not (Test-Path -LiteralPath $resolvedOutputFile)) {
    throw "SDK generation did not produce output at $resolvedOutputFile."
}

Write-Host "SDK generated at $resolvedOutputFile"
