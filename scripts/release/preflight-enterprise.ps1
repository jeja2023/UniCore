param(
    [switch]$SkipFrontend,
    [switch]$SkipBackendTests
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[preflight] $message" -ForegroundColor Cyan
}

Write-Step "Start enterprise preflight checks"

Write-Step "1) Backend restore/build"
dotnet restore "UniCore.slnx"
dotnet build "UniCore.slnx" --configuration Release --no-restore

if (-not $SkipBackendTests) {
    Write-Step "2) Backend integration tests"
    dotnet test "tests/Platform.WebApi.IntegrationTests/Platform.WebApi.IntegrationTests.csproj" --configuration Release --no-build
}

if (-not $SkipFrontend) {
    Write-Step "3) Frontend install/lint/build/test"
    Push-Location "frontend"
    try {
        npm ci
        npm run lint
        npm run build
        npm run test
    }
    finally {
        Pop-Location
    }
}

Write-Step "4) Container image build check"
    docker build -t unicore-webapi:preflight .

Write-Step "5) Migration script generation check"
dotnet ef migrations script --idempotent --project "src/Platform.Infrastructure/Platform.Infrastructure.csproj" --startup-project "src/Platform.WebApi/Platform.WebApi.csproj" | Out-Null

Write-Step "Enterprise preflight completed"
