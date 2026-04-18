param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$ForwardArgs
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$targetScript = Join-Path $repoRoot "new-project.ps1"

if (-not (Test-Path -LiteralPath $targetScript)) {
    throw "未找到目标脚本：$targetScript"
}

& $targetScript @ForwardArgs
