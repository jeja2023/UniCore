param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "staging", "prod")]
    [string]$Environment,
    [Parameter(Mandatory = $true)]
    [string]$TargetBackendVersion,
    [Parameter(Mandatory = $true)]
    [string]$TargetFrontendVersion,
    [string]$Reason = ""
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[rollback] $message" -ForegroundColor Yellow
}

$timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
Write-Step "Environment: $Environment"
Write-Step "Target backend version: $TargetBackendVersion"
Write-Step "Target frontend version: $TargetFrontendVersion"
if (-not [string]::IsNullOrWhiteSpace($Reason)) {
    Write-Step "Reason: $Reason"
}

Write-Step "1) Freeze current release pipeline"
Write-Step "2) Switch backend artifact to target version"
Write-Step "3) Switch frontend artifact to target version"
Write-Step "4) Re-run readiness checks"
Write-Step "5) Run bootstrap smoke on rollback environment"
Write-Step "Rollback checklist completed at $timestamp"
