param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "staging", "prod")]
    [string]$Environment,
    [Parameter(Mandatory = $true)]
    [string]$BackendVersion,
    [Parameter(Mandatory = $true)]
    [string]$FrontendVersion,
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[deploy] $message" -ForegroundColor Green
}

$timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
Write-Step "Environment: $Environment"
Write-Step "Backend version: $BackendVersion"
Write-Step "Frontend version: $FrontendVersion"
if (-not [string]::IsNullOrWhiteSpace($Notes)) {
    Write-Step "Notes: $Notes"
}

Write-Step "1) Pull release artifacts"
Write-Step "2) Apply DB migration (if enabled)"
Write-Step "3) Deploy backend + frontend"
Write-Step "4) Run health checks (/api/health/ready, /metrics)"
Write-Step "5) Run contract report with failOnBreaking=true"
Write-Host "Invoke example:" -ForegroundColor DarkGray
Write-Host "curl ""https://<host>/api/modules/contracts/report?protocolVersion=1.0.0&failOnBreaking=true""" -ForegroundColor DarkGray
Write-Step "Deploy checklist completed at $timestamp"
