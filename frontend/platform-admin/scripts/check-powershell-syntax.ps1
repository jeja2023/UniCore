param(
    [string]$ScriptsRoot = "."
)

$ErrorActionPreference = "Stop"

$resolvedScriptsRoot = Resolve-Path (Join-Path $PSScriptRoot $ScriptsRoot)
$scriptFiles = Get-ChildItem -LiteralPath $resolvedScriptsRoot -Filter *.ps1 -File | Sort-Object Name

if ($scriptFiles.Count -eq 0) {
    Write-Host "No PowerShell scripts found under $resolvedScriptsRoot"
    exit 0
}

$errors = New-Object System.Collections.Generic.List[string]

foreach ($scriptFile in $scriptFiles) {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $scriptFile.FullName,
        [ref]$tokens,
        [ref]$parseErrors
    ) | Out-Null

    if ($parseErrors.Count -gt 0) {
        foreach ($parseError in $parseErrors) {
            $errors.Add(
                $scriptFile.Name + " (line " + $parseError.Extent.StartLineNumber + ", col " + $parseError.Extent.StartColumnNumber + "): " + $parseError.Message
            )
        }
    }
}

if ($errors.Count -gt 0) {
    throw ("PowerShell syntax validation failed:`n - " + ($errors -join "`n - "))
}

Write-Host ("PowerShell syntax validation passed for " + $scriptFiles.Count + " script(s).")
