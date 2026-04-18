param(
    [string]$OpenApiUrl = "http://localhost:5000/swagger/v1/swagger.json",
    [string]$SdkFile = "../src/api/sdk/unicore-sdk.ts"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedSdkFile = if ([System.IO.Path]::IsPathRooted($SdkFile)) { $SdkFile } else { Join-Path $scriptDir $SdkFile }
$packageJsonPath = Join-Path (Split-Path -Parent $scriptDir) "package.json"
$isHttpUrl = $OpenApiUrl.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase) -or $OpenApiUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)
$resolvedOpenApiSpec = if ($isHttpUrl) { $OpenApiUrl } elseif ([System.IO.Path]::IsPathRooted($OpenApiUrl)) { $OpenApiUrl } else { Join-Path $scriptDir $OpenApiUrl }

if (-not (Test-Path $resolvedSdkFile)) {
    throw "SDK file not found: $resolvedSdkFile. Run ./generate-sdk.ps1 first."
}

$packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
$openApiTypescriptVersion = $packageJson.devDependencies."openapi-typescript"
if ([string]::IsNullOrWhiteSpace($openApiTypescriptVersion)) {
    throw "devDependencies.openapi-typescript is not configured in $packageJsonPath."
}
$resolvedVersion = $openApiTypescriptVersion.TrimStart("^", "~")

$tempRoot = if (-not [string]::IsNullOrWhiteSpace($env:TEMP)) {
    $env:TEMP
}
elseif (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    $env:RUNNER_TEMP
}
elseif (-not [string]::IsNullOrWhiteSpace($env:TMPDIR)) {
    $env:TMPDIR
}
else {
    [System.IO.Path]::GetTempPath()
}

if (-not (Test-Path -LiteralPath $tempRoot)) {
    New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null
}

$tempFile = Join-Path $tempRoot ("unicore-sdk-" + [Guid]::NewGuid() + ".ts")
try {
    npx --yes ("openapi-typescript@" + $resolvedVersion) $resolvedOpenApiSpec -o $tempFile | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate SDK from $resolvedOpenApiSpec. Ensure backend Swagger is reachable and retry."
    }

    if (-not (Test-Path -LiteralPath $tempFile)) {
        throw "SDK generation did not produce output at $tempFile. Ensure backend Swagger is reachable and retry."
    }

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
