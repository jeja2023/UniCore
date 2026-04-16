param(
    [string]$OpenApiUrl = "http://localhost:5000/swagger/v1/swagger.json",
    [string]$OpenApiFile = "../src/api/sdk/openapi.json",
    [string]$OutputFile = "../src/api/sdk/unicore-sdk.ts",
    [int]$RetryCount = 20,
    [int]$RetryDelaySeconds = 2
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedOpenApiFile = if ([System.IO.Path]::IsPathRooted($OpenApiFile)) { $OpenApiFile } else { Join-Path $scriptDir $OpenApiFile }
$resolvedOutputFile = if ([System.IO.Path]::IsPathRooted($OutputFile)) { $OutputFile } else { Join-Path $scriptDir $OutputFile }
$sdkDirectory = Split-Path -Parent $resolvedOutputFile

if (-not (Test-Path $sdkDirectory)) {
    New-Item -ItemType Directory -Path $sdkDirectory -Force | Out-Null
}

Write-Host "Exporting OpenAPI spec from $OpenApiUrl ..."
$success = $false
for ($i = 1; $i -le $RetryCount; $i++) {
    try {
        Invoke-WebRequest -Uri $OpenApiUrl -OutFile $resolvedOpenApiFile | Out-Null
        $success = $true
        break
    }
    catch {
        if ($i -eq $RetryCount) {
            throw
        }

        Write-Host "OpenAPI not ready (attempt $i/$RetryCount), retrying..."
        Start-Sleep -Seconds $RetryDelaySeconds
    }
}

if (-not $success) {
    throw "Failed to export OpenAPI spec from $OpenApiUrl"
}

Write-Host "OpenAPI exported to $resolvedOpenApiFile"
Write-Host "Generating SDK from exported OpenAPI file..."
npx --yes openapi-typescript@latest $resolvedOpenApiFile -o $resolvedOutputFile
Write-Host "SDK generated at $resolvedOutputFile"
