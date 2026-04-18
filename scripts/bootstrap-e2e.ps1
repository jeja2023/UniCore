param(
    [string]$ProjectNamePrefix = "BootstrapCi",
    [string]$DestinationRoot = "",
    [string[]]$ModuleCodes = @("order", "crm"),
    [switch]$SkipTemplateInstall,
    [switch]$KeepTemporaryProject,
    [switch]$CleanupOnFailure
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[bootstrap-e2e] $message" -ForegroundColor Cyan
}

function Wait-HttpReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [int]$MaxAttempts = 40,
        [int]$SleepSeconds = 2
    )

    for ($i = 1; $i -le $MaxAttempts; $i++) {
        try {
            Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5 | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds $SleepSeconds
        }
    }

    throw "HTTP endpoint not ready after $MaxAttempts attempts: $Url"
}

function Get-StringField {
    param(
        [Parameter(Mandatory = $true)]
        $Object,
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        if ($null -ne $Object.PSObject.Properties[$name]) {
            return [string]$Object.$name
        }
    }

    return ""
}

function Expand-ModuleCodes([string[]]$values) {
    $result = @()
    foreach ($value in $values) {
        if ([string]::IsNullOrWhiteSpace($value)) {
            continue
        }

        $parts = $value.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)
        foreach ($part in $parts) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $result += $trimmed
            }
        }
    }
    return @($result | Sort-Object -Unique)
}

$ModuleCodes = Expand-ModuleCodes -values $ModuleCodes
if ($ModuleCodes.Count -lt 2) {
    throw "At least two module codes are required for bootstrap e2e."
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
        $DestinationRoot = $env:RUNNER_TEMP
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:TEMP)) {
        $DestinationRoot = $env:TEMP
    }
    else {
        $DestinationRoot = (Get-Location).Path
    }
}

if (-not (Test-Path -LiteralPath $DestinationRoot)) {
    New-Item -Path $DestinationRoot -ItemType Directory -Force | Out-Null
}

$shortGuid = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$projectName = "$ProjectNamePrefix-$shortGuid"
$projectDir = Join-Path $DestinationRoot $projectName
$reportRoot = if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    Join-Path $env:RUNNER_TEMP "bootstrap-e2e-artifacts"
}
else {
    Join-Path $DestinationRoot "bootstrap-e2e-artifacts"
}
$reportDir = Join-Path $reportRoot $projectName
$reportJsonPath = Join-Path $reportDir "report.json"
$summaryPath = Join-Path $reportDir "summary.md"
$logCopyDir = Join-Path $reportDir "logs"

New-Item -Path $logCopyDir -ItemType Directory -Force | Out-Null

$backendUrl = "http://127.0.0.1:5010"
$swaggerUrl = "$backendUrl/swagger/v1/swagger.json"
$contractsUrl = "$backendUrl/api/modules/contracts"
$contractsReportUrl = "$backendUrl/api/modules/contracts/report?protocolVersion=1.0.0&failOnBreaking=true"
$backendStdOut = Join-Path $projectDir "bootstrap-e2e-backend.out.log"
$backendStdErr = Join-Path $projectDir "bootstrap-e2e-backend.err.log"

$backendProc = $null
$e2eSucceeded = $false
$failureMessage = ""
$availableModuleCodes = @()
$startedAt = [DateTimeOffset]::UtcNow

try {
    Write-Step "Create temporary project: $projectName"
    $newProjectScript = Join-Path $repoRoot "new-project.ps1"
    $newProjectArgs = @{
        ProjectName = $projectName
        DestinationRoot = $DestinationRoot
        ModuleCodes = $ModuleCodes
    }
    if ($SkipTemplateInstall) {
        $newProjectArgs.SkipTemplateInstall = $true
    }
    & $newProjectScript @newProjectArgs

    if (-not (Test-Path -LiteralPath $projectDir)) {
        throw "Bootstrap project directory was not created: $projectDir"
    }

    Write-Step "Run smoke checks for generated project"
    $smokeScript = Join-Path $repoRoot "scripts\bootstrap-smoke.ps1"
    & $smokeScript -ProjectRoot $projectDir -SkipFrontend

    $webApiProject = Join-Path $projectDir "src\Platform.WebApi\Platform.WebApi.csproj"
    Write-Step "Start generated backend for contract checks"
    $env:UseInMemoryDatabase = "true"
    $env:Swagger__EnableInNonDevelopment = "true"
    $env:Seed__AdminPassword = "Ci_Admin_12345!"
    $backendProc = Start-Process -FilePath "dotnet" `
        -WorkingDirectory $projectDir `
        -ArgumentList @("run", "--project", $webApiProject, "--urls", $backendUrl) `
        -RedirectStandardOutput $backendStdOut `
        -RedirectStandardError $backendStdErr `
        -PassThru

    Wait-HttpReady -Url $swaggerUrl

    $loginBody = @{
        username = "admin"
        password = "Ci_Admin_12345!"
        tenantId = "default"
    } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod -Method Post -Uri "$backendUrl/api/auth/login" -ContentType "application/json" -Body $loginBody
    $token = Get-StringField -Object $loginResponse.data -Names @("accessToken", "AccessToken")
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Failed to obtain access token from generated backend."
    }

    $headers = @{ Authorization = "Bearer $token" }
    $contractsResponse = Invoke-RestMethod -Method Get -Uri $contractsUrl -Headers $headers
    $contracts = @($contractsResponse.data)
    $availableModuleCodes = @(
        $contracts |
        ForEach-Object { Get-StringField -Object $_ -Names @("moduleCode", "ModuleCode") } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
    )

    foreach ($expectedCode in $ModuleCodes) {
        if ($expectedCode -notin $availableModuleCodes) {
            throw "Expected module '$expectedCode' was not found in generated backend contracts. Actual: $($availableModuleCodes -join ', ')"
        }
    }

    $reportResponse = Invoke-RestMethod -Method Get -Uri $contractsReportUrl -Headers $headers
    $reportData = $reportResponse.data
    if ($null -eq $reportData) {
        throw "Module contract report did not return data."
    }

    $isValid = $false
    $isValidRaw = Get-StringField -Object $reportData -Names @("isValid", "IsValid")
    if (-not [bool]::TryParse($isValidRaw, [ref]$isValid) -or -not $isValid) {
        throw "Module contract report returned invalid result for generated project."
    }

    $e2eSucceeded = $true
    Write-Step "Contract checks passed: $($ModuleCodes -join ', ')"
}
catch {
    $failureMessage = $_.Exception.Message
    throw
}
finally {
    if ($null -ne $backendProc -and -not $backendProc.HasExited) {
        Write-Step "Stop generated backend (PID=$($backendProc.Id))"
        Stop-Process -Id $backendProc.Id -Force
    }

    Remove-Item Env:UseInMemoryDatabase -ErrorAction SilentlyContinue
    Remove-Item Env:Swagger__EnableInNonDevelopment -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__AdminPassword -ErrorAction SilentlyContinue

    $copiedLogs = @()
    foreach ($path in @($backendStdOut, $backendStdErr)) {
        if (Test-Path -LiteralPath $path) {
            $target = Join-Path $logCopyDir ([System.IO.Path]::GetFileName($path))
            Copy-Item -LiteralPath $path -Destination $target -Force
            $copiedLogs += $target
        }
    }

    $shouldCleanup = $false
    if ($e2eSucceeded -and -not $KeepTemporaryProject) {
        $shouldCleanup = $true
    }
    elseif (-not $e2eSucceeded -and $CleanupOnFailure -and -not $KeepTemporaryProject) {
        $shouldCleanup = $true
    }

    if ($shouldCleanup -and (Test-Path -LiteralPath $projectDir)) {
        Write-Step "Clean up temporary project directory"
        Remove-Item -LiteralPath $projectDir -Recurse -Force
    }
    elseif (-not $e2eSucceeded -and (Test-Path -LiteralPath $projectDir)) {
        Write-Step "E2E failed, temporary project kept for diagnostics: $projectDir"
    }

    $keptTemporaryProject = (Test-Path -LiteralPath $projectDir)
    $finishedAt = [DateTimeOffset]::UtcNow
    $reportObject = [ordered]@{
        projectName = $projectName
        projectDir = $projectDir
        moduleCodes = $ModuleCodes
        detectedBackendModuleCodes = $availableModuleCodes
        backendUrl = $backendUrl
        succeeded = $e2eSucceeded
        failureMessage = $failureMessage
        startedAt = $startedAt.ToString("O")
        finishedAt = $finishedAt.ToString("O")
        keptTemporaryProject = $keptTemporaryProject
        cleanupOnFailure = $CleanupOnFailure.IsPresent
        skipTemplateInstall = $SkipTemplateInstall.IsPresent
        copiedLogFiles = $copiedLogs
    }

    $reportObject | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportJsonPath -Encoding UTF8

    $statusIcon = if ($e2eSucceeded) { "[PASS]" } else { "[FAIL]" }
    $summaryLines = @(
        "## bootstrap-e2e $statusIcon",
        "",
        "- Project: $projectName",
        "- Module codes: $($ModuleCodes -join ', ')",
        "- Succeeded: $e2eSucceeded",
        "- Kept temporary project: $keptTemporaryProject",
        "- Report json: $reportJsonPath"
    )
    if (-not [string]::IsNullOrWhiteSpace($failureMessage)) {
        $summaryLines += "- Failure: $failureMessage"
    }
    if ($copiedLogs.Count -gt 0) {
        $summaryLines += "- Copied logs: $($copiedLogs -join ', ')"
    }
    $summaryLines -join [Environment]::NewLine | Set-Content -LiteralPath $summaryPath -Encoding UTF8
}

Write-Step "bootstrap e2e completed successfully"
