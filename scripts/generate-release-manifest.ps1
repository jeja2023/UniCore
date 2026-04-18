param(
    [string]$OutputPath = "artifacts/release/release-manifest.json"
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return Split-Path -Parent $PSScriptRoot
    }

    $invocationPath = $MyInvocation.MyCommand.Path
    if (-not [string]::IsNullOrWhiteSpace($invocationPath)) {
        return Split-Path -Parent (Split-Path -Parent $invocationPath)
    }

    throw "Unable to resolve repository root: both PSScriptRoot and MyInvocation.MyCommand.Path are empty."
}

function Get-JsonVersion([string]$jsonPath) {
    if (-not (Test-Path -LiteralPath $jsonPath)) {
        return ""
    }

    $raw = Get-Content -LiteralPath $jsonPath -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return ""
    }

    $data = $raw | ConvertFrom-Json
    if ($null -ne $data.version) {
        return [string]$data.version
    }

    return ""
}

function Resolve-OutputPath([string]$repoRoot, [string]$pathValue) {
    if ([System.IO.Path]::IsPathRooted($pathValue)) {
        return $pathValue
    }
    return Join-Path $repoRoot $pathValue
}

$repoRoot = Resolve-RepoRoot
$resolvedOutput = Resolve-OutputPath -repoRoot $repoRoot -pathValue $OutputPath
$outputDir = Split-Path -Parent $resolvedOutput
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
}

$frontendAdminPackage = Join-Path $repoRoot "frontend/platform-admin/package.json"
$frontendWorkspacePackage = Join-Path $repoRoot "frontend/package.json"
$dotnetProjects = Get-ChildItem -Path (Join-Path $repoRoot "src") -Recurse -Filter "*.csproj" -File |
    Sort-Object FullName

$projectItems = @()
foreach ($project in $dotnetProjects) {
    $projectItems += [ordered]@{
        name = $project.BaseName
        path = $project.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
    }
}

$gitCommit = ""
try {
    $gitCommit = (git -C $repoRoot rev-parse HEAD 2>$null).Trim()
}
catch {
    $gitCommit = ""
}

$dotnetVersion = ""
try {
    $dotnetVersion = (dotnet --version 2>$null).Trim()
}
catch {
    $dotnetVersion = ""
}

$manifest = [ordered]@{
    generatedAt = [DateTimeOffset]::UtcNow.ToString("O")
    gitCommit = $gitCommit
    dotnetSdkVersion = $dotnetVersion
    protocolVersion = "1.0.0"
    frontend = [ordered]@{
        workspaceVersion = Get-JsonVersion -jsonPath $frontendWorkspacePackage
        platformAdminVersion = Get-JsonVersion -jsonPath $frontendAdminPackage
    }
    backendProjects = $projectItems
    governanceDocs = [ordered]@{
        compatibilityMatrix = "docs/compatibility-matrix.md"
        releaseProcess = "docs/release-process.md"
        ltsPolicy = "docs/lts-support-policy.md"
        sloSli = "docs/slo-sli.md"
        securityBaseline = "docs/security-baseline.md"
        opsRunbook = "docs/ops-runbook.md"
    }
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutput -Encoding UTF8
Write-Host "[release-manifest] generated: $resolvedOutput"
