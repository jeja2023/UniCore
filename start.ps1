param(
    [switch]$SkipInstall,
    [switch]$VerboseCheck,
    [switch]$MultiWindow
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendDir = Join-Path $repoRoot "frontend"
$frontendWorkspacePackage = Join-Path $frontendDir "package.json"
$platformAdminPackage = Join-Path $frontendDir "platform-admin\package.json"
$backendProject = Join-Path $repoRoot "src\Platform.WebApi\Platform.WebApi.csproj"

function Write-Check([string]$message) {
    if ($VerboseCheck) {
        Write-Host "[check] $message" -ForegroundColor DarkCyan
    }
}

Write-Check "repoRoot=$repoRoot"
Write-Check "frontendDir=$frontendDir"
Write-Check "backendProject=$backendProject"

if (-not (Test-Path -LiteralPath $backendProject)) {
    throw "Backend project not found: $backendProject"
}
Write-Check "backend project exists"

if (-not (Test-Path -LiteralPath $frontendDir)) {
    throw "Frontend directory not found: $frontendDir"
}
Write-Check "frontend directory exists"

if (-not (Test-Path -LiteralPath $frontendWorkspacePackage)) {
    throw "Frontend workspace package.json not found: $frontendWorkspacePackage. Please run this script from repository root."
}
Write-Check "frontend workspace package exists"

if (-not (Test-Path -LiteralPath $platformAdminPackage)) {
    throw "platform-admin package.json not found: $platformAdminPackage"
}
Write-Check "platform-admin package exists"

if (-not $SkipInstall) {
    Write-Host "Running npm install in frontend..." -ForegroundColor Cyan
    Set-Location -LiteralPath $frontendDir
    npm install
    Set-Location -LiteralPath $repoRoot
}
if ($SkipInstall) {
    Write-Host "Skip npm install." -ForegroundColor Yellow
}

function Stop-BackendProcessOnPort([int]$Port) {
    $portHolders = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($processId in $portHolders) {
        if ($processId -eq $PID) {
            continue
        }

        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            continue
        }

        $isBackendProcess =
            $process.ProcessName -eq "Platform.WebApi" -or
            $process.Path -like "*Platform.WebApi*"

        if ($isBackendProcess) {
            Write-Host "Stopping existing backend process on port $Port (PID: $processId)..." -ForegroundColor Yellow
            Stop-Process -Id $processId -Force
        }
        else {
            Write-Host "Port $Port is occupied by '$($process.ProcessName)' (PID: $processId). Please free the port manually." -ForegroundColor Red
            throw "Port $Port is not available."
        }
    }
}

Stop-BackendProcessOnPort -Port 5000

if (-not $MultiWindow) {
    Write-Host "Starting backend in current terminal mode..." -ForegroundColor Green
    Write-Check "backend command: dotnet run --project $backendProject"

    $runDir = Join-Path $repoRoot ".run"
    if (-not (Test-Path -LiteralPath $runDir)) {
        New-Item -ItemType Directory -Path $runDir | Out-Null
    }

    $backendStdOut = Join-Path $runDir "backend.out.log"
    $backendStdErr = Join-Path $runDir "backend.err.log"

    $backendProc = Start-Process -FilePath "dotnet" `
        -WorkingDirectory $repoRoot `
        -ArgumentList @("run", "--project", $backendProject) `
        -RedirectStandardOutput $backendStdOut `
        -RedirectStandardError $backendStdErr `
        -PassThru

    Start-Sleep -Seconds 2
    if ($backendProc.HasExited) {
        throw "Backend failed to start. Check logs: $backendStdOut / $backendStdErr"
    }

    Write-Host "Backend started (PID: $($backendProc.Id)). Logs: $backendStdOut" -ForegroundColor DarkGray
    Write-Host "Starting frontend in current terminal..." -ForegroundColor Green
    Write-Check "frontend command: npm run -w platform-admin dev"

    try {
        Set-Location -LiteralPath $frontendDir
        npm run -w platform-admin dev
    }
    finally {
        Set-Location -LiteralPath $repoRoot
        if (-not $backendProc.HasExited) {
            Write-Host "Stopping backend process (PID: $($backendProc.Id))..." -ForegroundColor Yellow
            Stop-Process -Id $backendProc.Id -Force
        }
    }
}
else {
    Write-Host "Starting backend window..." -ForegroundColor Green
    Write-Check "backend command: dotnet run --project $backendProject"
    Start-Process -FilePath "powershell.exe" -WorkingDirectory $repoRoot -ArgumentList "-NoExit", "-Command", "dotnet run --project '$backendProject'" | Out-Null

    Write-Host "Starting frontend window..." -ForegroundColor Green
    Write-Check "frontend command: npm run -w platform-admin dev"
    Start-Process -FilePath "powershell.exe" -WorkingDirectory $frontendDir -ArgumentList "-NoExit", "-Command", "npm run -w platform-admin dev" | Out-Null

    Write-Host "Started backend + frontend." -ForegroundColor Green
}

Write-Host "Fast mode: & .\start.ps1 -SkipInstall" -ForegroundColor DarkGray
Write-Host "Multi-window mode: & .\start.ps1 -MultiWindow" -ForegroundColor DarkGray
Write-Host "Frontend dev (no module sync): cd frontend; npm run dev:fast" -ForegroundColor DarkGray
Write-Host "Debug mode: & .\start.ps1 -VerboseCheck" -ForegroundColor DarkGray
