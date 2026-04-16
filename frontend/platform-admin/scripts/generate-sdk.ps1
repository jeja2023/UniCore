param(
    [string]$OpenApiUrl = "http://localhost:5000/swagger/v1/swagger.json",
    [string]$OutputFile = "../src/api/sdk/unicore-sdk.ts",
    [switch]$UseLocalPackage
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedOutputFile = if ([System.IO.Path]::IsPathRooted($OutputFile)) { $OutputFile } else { Join-Path $scriptDir $OutputFile }
$outputDirectory = Split-Path -Parent $resolvedOutputFile

if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Write-Host "Generating TypeScript SDK from $OpenApiUrl ..."
$command = if ($UseLocalPackage) { "openapi-typescript" } else { "openapi-typescript@latest" }
npx --yes $command $OpenApiUrl -o $resolvedOutputFile
Write-Host "SDK generated at $resolvedOutputFile"
