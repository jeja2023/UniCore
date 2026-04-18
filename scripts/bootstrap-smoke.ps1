param(
    [string]$ProjectRoot = ".",
    [switch]$SkipFrontend,
    [switch]$SkipBackendTests
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[bootstrap-smoke] $message" -ForegroundColor Cyan
}

$resolvedProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$solutionPath = Join-Path $resolvedProjectRoot "UniCore.slnx"
$webApiProject = Join-Path $resolvedProjectRoot "src\Platform.WebApi\Platform.WebApi.csproj"
$frontendDir = Join-Path $resolvedProjectRoot "frontend"
$platformAdminDir = Join-Path $frontendDir "platform-admin"

if (-not (Test-Path -LiteralPath $webApiProject)) {
    throw "Cannot find WebApi project: $webApiProject"
}

Push-Location $resolvedProjectRoot
try {
    if (Test-Path -LiteralPath $solutionPath) {
        Write-Step "dotnet restore/build solution"
        dotnet restore $solutionPath | Out-Host
        dotnet build $solutionPath --configuration Release | Out-Host
    }
    else {
        Write-Step "solution not found, build WebApi project"
        dotnet restore $webApiProject | Out-Host
        dotnet build $webApiProject --configuration Release | Out-Host
    }

    if (-not $SkipBackendTests) {
        Write-Step "run backend tests"
        dotnet test --configuration Release --no-build | Out-Host
    }
    else {
        Write-Step "skip backend tests"
    }

    if (-not $SkipFrontend -and (Test-Path -LiteralPath $frontendDir) -and (Test-Path -LiteralPath $platformAdminDir)) {
        Write-Step "run frontend checks"
        Push-Location $frontendDir
        try {
            npm install | Out-Host
            npm run -w platform-admin modules:validate | Out-Host
            npm run -w platform-admin build | Out-Host
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Step "skip frontend checks"
    }
}
finally {
    Pop-Location
}

Write-Step "all smoke checks passed"
