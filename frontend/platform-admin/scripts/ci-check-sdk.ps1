param(
    [string]$OpenApiUrl = "http://localhost:5000/swagger/v1/swagger.json"
)

$ErrorActionPreference = "Stop"

Write-Host "::group::SDK precheck"
try {
    $response = Invoke-WebRequest -Uri $OpenApiUrl -Method Get -TimeoutSec 10
    Write-Host "Swagger ready. HTTP status: $($response.StatusCode)"
}
catch {
    Write-Host "::error::Cannot reach Swagger: $OpenApiUrl"
    Write-Host "::error::Ensure backend is running and listening on the target port."
    throw
}
finally {
    Write-Host "::endgroup::"
}

Write-Host "::group::SDK sync check"
try {
    & "./scripts/check-sdk-up-to-date.ps1" -OpenApiUrl $OpenApiUrl
    Write-Host "SDK sync check passed."
}
catch {
    Write-Host "::error::SDK check failed. If API changed, run ./scripts/generate-sdk.ps1 and commit generated files."
    throw
}
finally {
    Write-Host "::endgroup::"
}
