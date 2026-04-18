param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "staging", "prod")]
    [string]$Environment,
    [Parameter(Mandatory = $true)]
    [string]$BackendVersion,
    [Parameter(Mandatory = $true)]
    [string]$FrontendVersion,
    [string]$Notes = "",
    [switch]$InitDatabase,
    [switch]$SkipMigrations,
    [string]$DbHost,
    [int]$DbPort = 5432,
    [string]$DbAdminUser,
    [SecureString]$DbAdminPassword,
    [string]$DbAppUser,
    [SecureString]$DbAppPassword,
    [string]$DbName,
    [switch]$RunPreflight
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[deploy] $message" -ForegroundColor Green
}

function Invoke-ChildScript([string]$scriptPath, [object[]]$arguments = @()) {
    if (Get-Command pwsh -ErrorAction SilentlyContinue) {
        & pwsh $scriptPath @arguments
    }
    else {
        & powershell -ExecutionPolicy Bypass -File $scriptPath @arguments
    }
}

function Assert-InitDbParameters {
    if (-not $InitDatabase) {
        return
    }

    $missing = @()
    if ([string]::IsNullOrWhiteSpace($DbHost)) { $missing += "DbHost" }
    if ([string]::IsNullOrWhiteSpace($DbAdminUser)) { $missing += "DbAdminUser" }
    if ($null -eq $DbAdminPassword) { $missing += "DbAdminPassword" }
    if ([string]::IsNullOrWhiteSpace($DbAppUser)) { $missing += "DbAppUser" }
    if ($null -eq $DbAppPassword) { $missing += "DbAppPassword" }
    if ([string]::IsNullOrWhiteSpace($DbName)) { $missing += "DbName" }

    if ($missing.Count -gt 0) {
        throw "InitDatabase=true requires params: $($missing -join ', ')"
    }
}

Assert-InitDbParameters

$timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
Write-Step "Environment: $Environment"
Write-Step "Backend version: $BackendVersion"
Write-Step "Frontend version: $FrontendVersion"
if (-not [string]::IsNullOrWhiteSpace($Notes)) {
    Write-Step "Notes: $Notes"
}
Write-Step "InitDatabase: $InitDatabase"
Write-Step "SkipMigrations: $SkipMigrations"
Write-Step "RunPreflight: $RunPreflight"

if ($RunPreflight) {
    Write-Step "Run enterprise preflight checks"
    Invoke-ChildScript "$PSScriptRoot/preflight-enterprise.ps1"
    if ($LASTEXITCODE -ne 0) {
        throw "preflight failed"
    }
}

if ($InitDatabase) {
    Write-Step "Initialize PostgreSQL role/database"
    Invoke-ChildScript "$PSScriptRoot/init-postgres.ps1" @(
        "-Host", $DbHost,
        "-Port", $DbPort,
        "-AdminUser", $DbAdminUser,
        "-AdminPassword", $DbAdminPassword,
        "-AppUser", $DbAppUser,
        "-AppPassword", $DbAppPassword,
        "-DatabaseName", $DbName
    )
    if ($LASTEXITCODE -ne 0) {
        throw "database initialization failed"
    }
}

if (-not $SkipMigrations) {
    Write-Step "Apply EF Core migrations"
    dotnet ef database update --project "src/Platform.Infrastructure/Platform.Infrastructure.csproj" --startup-project "src/Platform.WebApi/Platform.WebApi.csproj"
    if ($LASTEXITCODE -ne 0) {
        throw "database migration failed"
    }
}

Write-Step "1) Pull release artifacts"
Write-Step "2) Apply DB migration (if enabled)"
Write-Step "3) Deploy backend + frontend"
Write-Step "4) Run health checks (/api/health/ready, /metrics)"
Write-Step "5) Run contract report with failOnBreaking=true"
Write-Host "Invoke example:" -ForegroundColor DarkGray
Write-Host 'curl "https://<host>/api/modules/contracts/report?protocolVersion=1.0.0&failOnBreaking=true"' -ForegroundColor DarkGray
Write-Step "Deploy checklist completed at $timestamp"
