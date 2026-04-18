param(
    [ValidatePattern("^[A-Za-z][A-Za-z0-9_.-]*$")]
    [string]$ProjectName,

    [string]$DestinationRoot = "",

    [string]$ModuleName = "",

    [string]$ModuleCode = "",

    [string]$SecondModule = "",

    [string[]]$AdditionalModules = @(),

    [string[]]$ModuleCodes = @(),

    [string]$ConfigFile = "",

    [string]$Profile = "",

    [switch]$ListProfiles,

    [switch]$SkipTemplateInstall
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[UniCore Bootstrap] $message" -ForegroundColor Cyan
}

function Test-CommandExists([string]$commandName) {
    return $null -ne (Get-Command $commandName -ErrorAction SilentlyContinue)
}

function Resolve-AbsolutePath([string]$basePath, [string]$pathValue) {
    if ([string]::IsNullOrWhiteSpace($pathValue)) {
        return ""
    }
    if ([System.IO.Path]::IsPathRooted($pathValue)) {
        return $pathValue
    }
    return Join-Path $basePath $pathValue
}

function Resolve-SingleFile([string]$searchRoot, [string]$filter) {
    $matches = Get-ChildItem -Path $searchRoot -Filter $filter -Recurse -File
    if ($matches.Count -eq 0) {
        throw "未找到文件：$filter（搜索目录：$searchRoot）"
    }
    if ($matches.Count -gt 1) {
        $paths = $matches | ForEach-Object { $_.FullName }
        throw "找到多个文件，请手动处理：$filter`n$($paths -join "`n")"
    }
    return $matches[0].FullName
}

function Get-ProfileFiles([string]$profilesDir) {
    if (-not (Test-Path -LiteralPath $profilesDir)) {
        return @()
    }
    return @(Get-ChildItem -Path $profilesDir -Filter "*.json" -File | Sort-Object Name)
}

function Load-JsonConfig([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "配置文件不存在：$path"
    }
    $raw = Get-Content -LiteralPath $path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "配置文件为空：$path"
    }
    return $raw | ConvertFrom-Json
}

function Add-ModuleToProject(
    [string]$projectDir,
    [string]$solutionFile,
    [string]$webApiProject,
    [string]$moduleName,
    [string]$moduleCode
) {
    Write-Step "创建业务模块: $moduleName（code=$moduleCode）"
    dotnet new unicore-module -n $moduleName --ModuleCode $moduleCode | Out-Host

    $moduleProjectCandidates = @(
        (Join-Path $projectDir "src\$moduleName\$moduleName.csproj"),
        (Join-Path $projectDir "$moduleName\$moduleName.csproj")
    )

    $moduleProject = $null
    foreach ($candidate in $moduleProjectCandidates) {
        if (Test-Path -LiteralPath $candidate) {
            $moduleProject = $candidate
            break
        }
    }
    if ($null -eq $moduleProject) {
        $moduleProject = Resolve-SingleFile -searchRoot $projectDir -filter "$moduleName.csproj"
    }

    Write-Step "将模块 $moduleName 加入解决方案..."
    dotnet sln "$solutionFile" add "$moduleProject" | Out-Host

    Write-Step "为 Platform.WebApi 添加模块 $moduleName 引用..."
    dotnet add "$webApiProject" reference "$moduleProject" | Out-Host
}

function Parse-AdditionalModule([string]$item) {
    if ([string]::IsNullOrWhiteSpace($item)) {
        throw "AdditionalModules 包含空项。正确格式：ModuleName:moduleCode（例如 CrmModule:crm）"
    }

    $parts = $item.Split(":", 2)
    if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1])) {
        throw "AdditionalModules 项 '$item' 格式无效。正确格式：ModuleName:moduleCode（例如 CrmModule:crm）"
    }

    return @{
        Name = $parts[0].Trim()
        Code = $parts[1].Trim()
    }
}

function Convert-CodeToModuleName([string]$moduleCode) {
    if ([string]::IsNullOrWhiteSpace($moduleCode)) {
        throw "模块代码不能为空。"
    }

    $segments = $moduleCode.Trim().Split("-", [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments.Count -eq 0) {
        throw "模块代码格式无效：$moduleCode"
    }

    $name = ($segments | ForEach-Object {
            $value = $_.Trim()
            if ($value.Length -eq 1) {
                $value.ToUpper()
            }
            else {
                $value.Substring(0, 1).ToUpper() + $value.Substring(1).ToLower()
            }
        }) -join ""

    return "$name" + "Module"
}

function Expand-CommaSeparatedValues([string[]]$values) {
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
    return $result
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$platformTemplatePath = Join-Path $repoRoot "templates\unicore-platform-template"
$moduleTemplatePath = Join-Path $repoRoot "templates\unicore-business-module-template"
$profilesDir = Join-Path $repoRoot "bootstrap-profiles"

if ($ListProfiles) {
    $profileFiles = Get-ProfileFiles -profilesDir $profilesDir
    if ($profileFiles.Count -eq 0) {
        Write-Host "未找到预置场景配置。目录：$profilesDir" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "可用预置场景：" -ForegroundColor Green
    foreach ($file in $profileFiles) {
        Write-Host "- $($file.BaseName)"
    }
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
    $configPath = Resolve-AbsolutePath -basePath $repoRoot -pathValue $ConfigFile
    $config = Load-JsonConfig -path $configPath

    if (-not $PSBoundParameters.ContainsKey("Profile") -and -not [string]::IsNullOrWhiteSpace($config.Profile)) {
        $Profile = [string]$config.Profile
    }
    if (-not $PSBoundParameters.ContainsKey("ProjectName") -and -not [string]::IsNullOrWhiteSpace($config.ProjectName)) {
        $ProjectName = [string]$config.ProjectName
    }
    if (-not $PSBoundParameters.ContainsKey("DestinationRoot") -and -not [string]::IsNullOrWhiteSpace($config.DestinationRoot)) {
        $DestinationRoot = [string]$config.DestinationRoot
    }
    if (-not $PSBoundParameters.ContainsKey("ModuleName") -and -not [string]::IsNullOrWhiteSpace($config.ModuleName)) {
        $ModuleName = [string]$config.ModuleName
    }
    if (-not $PSBoundParameters.ContainsKey("ModuleCode") -and -not [string]::IsNullOrWhiteSpace($config.ModuleCode)) {
        $ModuleCode = [string]$config.ModuleCode
    }
    if (-not $PSBoundParameters.ContainsKey("SecondModule") -and -not [string]::IsNullOrWhiteSpace($config.SecondModule)) {
        $SecondModule = [string]$config.SecondModule
    }
    if (-not $PSBoundParameters.ContainsKey("AdditionalModules") -and $null -ne $config.AdditionalModules) {
        $AdditionalModules = @($config.AdditionalModules | ForEach-Object { [string]$_ })
    }
    if (-not $PSBoundParameters.ContainsKey("ModuleCodes") -and $null -ne $config.ModuleCodes) {
        $ModuleCodes = @($config.ModuleCodes | ForEach-Object { [string]$_ })
    }
    if (-not $PSBoundParameters.ContainsKey("SkipTemplateInstall") -and $null -ne $config.SkipTemplateInstall) {
        $SkipTemplateInstall = [bool]$config.SkipTemplateInstall
    }
}

if (-not [string]::IsNullOrWhiteSpace($Profile)) {
    $profilePath = Join-Path $profilesDir "$Profile.json"
    $profileConfig = Load-JsonConfig -path $profilePath

    if (-not $PSBoundParameters.ContainsKey("DestinationRoot") -and [string]::IsNullOrWhiteSpace($DestinationRoot) -and -not [string]::IsNullOrWhiteSpace($profileConfig.DestinationRoot)) {
        $DestinationRoot = [string]$profileConfig.DestinationRoot
    }
    if (-not $PSBoundParameters.ContainsKey("ModuleName") -and [string]::IsNullOrWhiteSpace($ModuleName) -and -not [string]::IsNullOrWhiteSpace($profileConfig.ModuleName)) {
        $ModuleName = [string]$profileConfig.ModuleName
    }
    if (-not $PSBoundParameters.ContainsKey("ModuleCode") -and [string]::IsNullOrWhiteSpace($ModuleCode) -and -not [string]::IsNullOrWhiteSpace($profileConfig.ModuleCode)) {
        $ModuleCode = [string]$profileConfig.ModuleCode
    }
    if (-not $PSBoundParameters.ContainsKey("SecondModule") -and [string]::IsNullOrWhiteSpace($SecondModule) -and -not [string]::IsNullOrWhiteSpace($profileConfig.SecondModule)) {
        $SecondModule = [string]$profileConfig.SecondModule
    }
    if (-not $PSBoundParameters.ContainsKey("AdditionalModules") -and $AdditionalModules.Count -eq 0 -and $null -ne $profileConfig.AdditionalModules) {
        $AdditionalModules = @($profileConfig.AdditionalModules | ForEach-Object { [string]$_ })
    }
    if (-not $PSBoundParameters.ContainsKey("ModuleCodes") -and $ModuleCodes.Count -eq 0 -and $null -ne $profileConfig.ModuleCodes) {
        $ModuleCodes = @($profileConfig.ModuleCodes | ForEach-Object { [string]$_ })
    }
    if (-not $PSBoundParameters.ContainsKey("SkipTemplateInstall") -and $null -ne $profileConfig.SkipTemplateInstall) {
        $SkipTemplateInstall = [bool]$profileConfig.SkipTemplateInstall
    }
}

$AdditionalModules = @(Expand-CommaSeparatedValues -values $AdditionalModules)
$ModuleCodes = @(Expand-CommaSeparatedValues -values $ModuleCodes)

if ([string]::IsNullOrWhiteSpace($ProjectName)) {
    throw "必须提供 ProjectName。可通过参数 -ProjectName 或配置文件中的 ProjectName 指定。"
}

if (-not (Test-Path -LiteralPath $platformTemplatePath)) {
    throw "未找到平台模板目录: $platformTemplatePath"
}
if (-not (Test-Path -LiteralPath $moduleTemplatePath)) {
    throw "未找到业务模块模板目录: $moduleTemplatePath"
}
if (-not (Test-CommandExists "dotnet")) {
    throw "未检测到 dotnet，请先安装 .NET SDK（建议 .NET 10）。"
}

if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = Split-Path -Parent $repoRoot
}

$DestinationRoot = (Resolve-Path -LiteralPath $DestinationRoot).Path
$projectDir = Join-Path $DestinationRoot $ProjectName

if (Test-Path -LiteralPath $projectDir) {
    throw "目标目录已存在: $projectDir"
}

if (-not $SkipTemplateInstall) {
    Write-Step "安装本地模板（platform + module）..."
    dotnet new install "$platformTemplatePath" | Out-Host
    dotnet new install "$moduleTemplatePath" | Out-Host
}
else {
    Write-Step "已跳过模板安装（-SkipTemplateInstall）"
}

Write-Step "创建平台项目: $ProjectName"
Push-Location $DestinationRoot
try {
    dotnet new unicore-platform -n $ProjectName | Out-Host
}
finally {
    Pop-Location
}

if (-not [string]::IsNullOrWhiteSpace($ModuleName)) {
    if ([string]::IsNullOrWhiteSpace($ModuleCode)) {
        throw "传入了 -ModuleName，但未传入 -ModuleCode。请同时指定模块代码（例如 order、crm）。"
    }
}

if ($AdditionalModules.Count -gt 0 -and ([string]::IsNullOrWhiteSpace($ModuleName) -or [string]::IsNullOrWhiteSpace($ModuleCode))) {
    Write-Step "检测到 AdditionalModules，将在主模块之外继续批量创建并挂载。"
}

$modulesToCreate = @()
if (-not [string]::IsNullOrWhiteSpace($ModuleName)) {
    $modulesToCreate += @{
        Name = $ModuleName.Trim()
        Code = $ModuleCode.Trim()
    }
}

foreach ($item in $AdditionalModules) {
    $modulesToCreate += Parse-AdditionalModule -item $item
}

if (-not [string]::IsNullOrWhiteSpace($SecondModule)) {
    $modulesToCreate += Parse-AdditionalModule -item $SecondModule
}

foreach ($code in $ModuleCodes) {
    if ([string]::IsNullOrWhiteSpace($code)) {
        throw "ModuleCodes 中包含空项，请检查。"
    }

    $trimmedCode = $code.Trim()
    $modulesToCreate += @{
        Name = Convert-CodeToModuleName -moduleCode $trimmedCode
        Code = $trimmedCode
    }
}

if ($modulesToCreate.Count -gt 0) {
    $solutionFile = Resolve-SingleFile -searchRoot $projectDir -filter "*.sln"
    $webApiProject = Join-Path $projectDir "src\Platform.WebApi\Platform.WebApi.csproj"
    if (-not (Test-Path -LiteralPath $webApiProject)) {
        throw "未找到 WebApi 项目文件：$webApiProject"
    }

    $moduleNameSet = @{}
    $moduleCodeSet = @{}
    foreach ($module in $modulesToCreate) {
        if ($moduleNameSet.ContainsKey($module.Name)) {
            throw "模块名称重复：$($module.Name)"
        }
        if ($moduleCodeSet.ContainsKey($module.Code)) {
            throw "模块代码重复：$($module.Code)"
        }
        $moduleNameSet[$module.Name] = $true
        $moduleCodeSet[$module.Code] = $true
    }

    Push-Location $projectDir
    try {
        foreach ($module in $modulesToCreate) {
            Add-ModuleToProject -projectDir $projectDir -solutionFile $solutionFile -webApiProject $webApiProject -moduleName $module.Name -moduleCode $module.Code
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "创建完成。" -ForegroundColor Green
Write-Host "项目目录: $projectDir" -ForegroundColor Green
Write-Host ""
Write-Host "建议下一步：" -ForegroundColor Yellow
Write-Host "1) cd `"$projectDir`""
Write-Host "2) dotnet run --project .\src\Platform.WebApi\Platform.WebApi.csproj"
Write-Host ""
Write-Host "示例：" -ForegroundColor DarkGray
Write-Host ".\new-project.ps1 -ProjectName AcmeOpsPlatform -DestinationRoot e:\Projects -ModuleName OrderModule -ModuleCode order"
Write-Host ".\new-project.ps1 -ProjectName AcmeOpsPlatform -DestinationRoot e:\Projects -ModuleCodes order,crm,inventory"
Write-Host ".\new-project.ps1 -ProjectName AcmeOpsPlatform -DestinationRoot e:\Projects -ModuleName OrderModule -ModuleCode order -AdditionalModules CrmModule:crm,InventoryModule:inventory"
Write-Host ".\new-project.ps1 -ProjectName AcmeOpsPlatform -Profile erp -DestinationRoot e:\Projects"
Write-Host ".\new-project.ps1 -ListProfiles"
Write-Host ".\new-project.ps1 -ConfigFile .\new-project.config.sample.json"
