param(
    [string]$Root = ".",
    [string[]]$IncludePaths = @()
)

$ErrorActionPreference = "Stop"

function Resolve-RootPath([string]$pathValue) {
    return (Resolve-Path -LiteralPath $pathValue).Path
}

function Resolve-IncludeScripts([string]$baseRoot, [string[]]$paths) {
    $resolved = @()
    foreach ($item in $paths) {
        if ([string]::IsNullOrWhiteSpace($item)) {
            continue
        }

        $segments = $item.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)
        foreach ($segment in $segments) {
            $candidate = $segment.Trim()
            if ([string]::IsNullOrWhiteSpace($candidate)) {
                continue
            }

            $full = if ([System.IO.Path]::IsPathRooted($candidate)) {
                $candidate
            }
            else {
                Join-Path $baseRoot $candidate
            }

            if (-not (Test-Path -LiteralPath $full)) {
                throw "Include path not found: $candidate"
            }

            $resolved += (Resolve-Path -LiteralPath $full).Path
        }
    }

    return @($resolved | Sort-Object -Unique)
}

function Get-RepoScripts([string]$baseRoot) {
    $all = Get-ChildItem -Path $baseRoot -Recurse -File -Filter "*.ps1"
    $normalized = $baseRoot.TrimEnd('\', '/')
    $skipPrefixes = @(
        (Join-Path $normalized ".git"),
        (Join-Path $normalized ".run"),
        (Join-Path $normalized "artifacts")
    )

    $result = @()
    foreach ($file in $all) {
        $path = $file.FullName
        $skip = $false
        foreach ($prefix in $skipPrefixes) {
            if ($path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $skip = $true
                break
            }
        }

        if (-not $skip -and $path -match "[\\/]+node_modules[\\/]") {
            $skip = $true
        }

        if (-not $skip) {
            $result += $path
        }
    }

    return @($result | Sort-Object -Unique)
}

$rootPath = Resolve-RootPath -pathValue $Root
$scripts = @()
if ($IncludePaths.Count -gt 0) {
    $scripts = Resolve-IncludeScripts -baseRoot $rootPath -paths $IncludePaths
}
else {
    $scripts = Get-RepoScripts -baseRoot $rootPath
}

if ($scripts.Count -eq 0) {
    throw "No PowerShell scripts found to validate."
}

$parseErrors = New-Object System.Collections.Generic.List[string]
foreach ($scriptPath in $scripts) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)

    if ($errors.Count -gt 0) {
        foreach ($parseError in $errors) {
            $parseErrors.Add(("{0}:{1}:{2} {3}" -f $scriptPath, $parseError.Extent.StartLineNumber, $parseError.Extent.StartColumnNumber, $parseError.Message))
        }
    }
    else {
        Write-Host "[ps-validate] syntax ok: $scriptPath"
    }
}

if ($parseErrors.Count -gt 0) {
    throw ("PowerShell syntax validation failed:`n - " + ($parseErrors -join "`n - "))
}

Write-Host "[ps-validate] validated $($scripts.Count) script(s)."
