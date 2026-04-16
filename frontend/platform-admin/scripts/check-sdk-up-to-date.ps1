param(
    [string]$OpenApiUrl = "http://localhost:5000/swagger/v1/swagger.json",
    [string]$SdkFile = "../src/api/sdk/unicore-sdk.ts"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedSdkFile = if ([System.IO.Path]::IsPathRooted($SdkFile)) { $SdkFile } else { Join-Path $scriptDir $SdkFile }
$isHttpUrl = $OpenApiUrl.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase) -or $OpenApiUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)
$resolvedOpenApiSpec = if ($isHttpUrl) { $OpenApiUrl } elseif ([System.IO.Path]::IsPathRooted($OpenApiUrl)) { $OpenApiUrl } else { Join-Path $scriptDir $OpenApiUrl }

if (-not (Test-Path $resolvedSdkFile)) {
    throw "SDK file not found: $resolvedSdkFile. Run ./generate-sdk.ps1 first."
}

$tempFile = Join-Path $env:TEMP ("unicore-sdk-" + [Guid]::NewGuid() + ".ts")
try {
    npx --yes openapi-typescript@latest $resolvedOpenApiSpec -o $tempFile | Out-Null

    $existingHash = (Get-FileHash $resolvedSdkFile -Algorithm SHA256).Hash
    $newHash = (Get-FileHash $tempFile -Algorithm SHA256).Hash
    if ($existingHash -ne $newHash) {
        throw "SDK is out of date. Re-run ./generate-sdk.ps1 and commit updated SDK."
    }

    Write-Host "SDK is up to date."
}
finally {
    if ([System.IO.File]::Exists($tempFile)) {
        Remove-Item $tempFile -Force
    }
}
