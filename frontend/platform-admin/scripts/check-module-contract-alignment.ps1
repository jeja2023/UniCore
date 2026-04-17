param(
    [string]$BackendBaseUrl = "http://localhost:5000",
    [string]$ModulesRoot = "..\..\modules",
    [string]$Username = "admin",
    [string]$Password = "UniCore@123",
    [string]$TenantId = "default",
    [string]$AccessToken = ""
)

$ErrorActionPreference = "Stop"

function Find-ModuleFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleDir,
        [Parameter(Mandatory = $true)]
        [string]$BaseName
    )

    foreach ($extension in @(".tsx", ".ts", ".jsx", ".js")) {
        $candidate = Join-Path $ModuleDir ($BaseName + $extension)
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-JsonValue {
    param(
        [Parameter(Mandatory = $true)]
        $Object,
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        if ($null -ne $Object.PSObject.Properties[$name]) {
            return $Object.$name
        }
    }

    return $null
}

$resolvedModulesRoot = Resolve-Path (Join-Path $PSScriptRoot $ModulesRoot)
$frontendModules = @()

foreach ($moduleDir in (Get-ChildItem -LiteralPath $resolvedModulesRoot -Directory | Sort-Object Name)) {
    $packageJsonPath = Join-Path $moduleDir.FullName "package.json"
    if (-not (Test-Path -LiteralPath $packageJsonPath)) {
        continue
    }

    $permissionsPath = Find-ModuleFile -ModuleDir $moduleDir.FullName -BaseName "permissions"
    $menuPath = Find-ModuleFile -ModuleDir $moduleDir.FullName -BaseName "menu"

    if ($null -eq $permissionsPath -or $null -eq $menuPath) {
        throw ("Cannot check module '" + $moduleDir.Name + "' because permissions/menu exports are missing.")
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $permissionsContent = Get-Content -LiteralPath $permissionsPath -Raw
    $permissionMatches = [System.Text.RegularExpressions.Regex]::Matches($permissionsContent, '[\"\x27]([a-z][a-z0-9_-]*\.[a-z][a-z0-9_-]*)[\"\x27]')
    $permissionValues = @()
    $permissionMap = @{}
    foreach ($match in $permissionMatches) {
        $permissionValues += $match.Groups[1].Value
    }

    $propertyMatches = [System.Text.RegularExpressions.Regex]::Matches($permissionsContent, '([A-Za-z0-9_]+)\s*:\s*[\"\x27]([a-z][a-z0-9_-]*\.[a-z][a-z0-9_-]*)[\"\x27]')
    foreach ($match in $propertyMatches) {
        $permissionMap[$match.Groups[1].Value] = $match.Groups[2].Value
    }

    $permissionValues = @($permissionValues | Sort-Object -Unique)
    $moduleCode = $null
    if ($permissionValues.Count -gt 0) {
        $prefixes = @($permissionValues | ForEach-Object { ($_ -split '\.')[0] } | Sort-Object -Unique)
        if ($prefixes.Count -eq 1) {
            $moduleCode = $prefixes[0]
        }
    }

    $menuContent = Get-Content -LiteralPath $menuPath -Raw
    $menuMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $menuContent,
        '\{\s*key:\s*[\"\x27](?<key>[^\"\x27]+)[\"\x27]\s*,\s*title:\s*[\"\x27](?<title>[^\"\x27]+)[\"\x27]\s*,\s*path:\s*[\"\x27](?<path>[^\"\x27]+)[\"\x27]\s*,\s*permission:\s*(?<permission>permissions\.[A-Za-z0-9_]+|[\"\x27][^\"\x27]+[\"\x27])',
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    $menus = @()
    foreach ($match in $menuMatches) {
        $rawPermission = $match.Groups["permission"].Value
        $resolvedPermission = $rawPermission
        if ($rawPermission.StartsWith("permissions.")) {
            $permissionKey = $rawPermission.Substring("permissions.".Length)
            $resolvedPermission = $permissionMap[$permissionKey]
        }
        else {
            $resolvedPermission = $rawPermission.Trim('"', "'")
        }

        $menus += [pscustomobject]@{
            key = $match.Groups["key"].Value
            title = $match.Groups["title"].Value
            path = $match.Groups["path"].Value
            permission = $resolvedPermission
        }
    }

    $frontendModules += [pscustomobject]@{
        sourceDir = $moduleDir.Name
        packageName = $packageJson.name
        version = $packageJson.version
        moduleCode = $moduleCode
        permissions = $permissionValues
        menus = $menus
    }
}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    $loginBody = @{
        username = $Username
        password = $Password
        tenantId = $TenantId
    } | ConvertTo-Json

    $loginResponse = Invoke-RestMethod -Method Post -Uri ($BackendBaseUrl.TrimEnd("/") + "/api/auth/login") -ContentType "application/json" -Body $loginBody
    $AccessToken = Get-JsonValue -Object $loginResponse.data -Names @("accessToken", "AccessToken")
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw "Failed to acquire access token from backend."
    }
}

$headers = @{
    Authorization = "Bearer $AccessToken"
}

$contractsResponse = Invoke-RestMethod -Method Get -Uri ($BackendBaseUrl.TrimEnd("/") + "/api/modules/contracts") -Headers $headers
$backendModules = @($contractsResponse.data)
$backendByCode = @{}
foreach ($backendModule in $backendModules) {
    $moduleCode = Get-JsonValue -Object $backendModule -Names @("moduleCode", "ModuleCode")
    if (-not [string]::IsNullOrWhiteSpace($moduleCode)) {
        $backendByCode[$moduleCode] = $backendModule
    }
}

$errors = New-Object System.Collections.Generic.List[string]

foreach ($frontendModule in $frontendModules) {
    if ([string]::IsNullOrWhiteSpace($frontendModule.moduleCode)) {
        $errors.Add($frontendModule.sourceDir + ": cannot infer moduleCode from frontend permissions")
        continue
    }

    if (-not $backendByCode.ContainsKey($frontendModule.moduleCode)) {
        $errors.Add($frontendModule.sourceDir + ": frontend moduleCode '" + $frontendModule.moduleCode + "' not found in backend contracts")
        continue
    }

    $backendModule = $backendByCode[$frontendModule.moduleCode]
    $backendPermissions = @()
    foreach ($permission in (Get-JsonValue -Object $backendModule -Names @("permissions", "Permissions"))) {
        $permissionCode = Get-JsonValue -Object $permission -Names @("permissionCode", "PermissionCode", "code", "Code")
        if ($permissionCode) {
            $backendPermissions += $permissionCode
        }
    }
    $backendPermissions = $backendPermissions | Sort-Object -Unique

    $missingInBackend = $frontendModule.permissions | Where-Object { $_ -notin $backendPermissions }
    $missingInFrontend = $backendPermissions | Where-Object { $_ -notin $frontendModule.permissions }
    if ($missingInBackend.Count -gt 0) {
        $errors.Add($frontendModule.moduleCode + ": backend is missing permissions -> " + ($missingInBackend -join ", "))
    }
    if ($missingInFrontend.Count -gt 0) {
        $errors.Add($frontendModule.moduleCode + ": frontend is missing permissions -> " + ($missingInFrontend -join ", "))
    }

    $backendMenus = @{}
    foreach ($menu in (Get-JsonValue -Object $backendModule -Names @("menus", "Menus"))) {
        $menuKey = Get-JsonValue -Object $menu -Names @("menuCode", "MenuCode", "key", "Key")
        if (-not $menuKey) {
            continue
        }

        $backendMenus[$menuKey] = [pscustomobject]@{
            path = Get-JsonValue -Object $menu -Names @("routePath", "RoutePath", "path", "Path")
            permission = Get-JsonValue -Object $menu -Names @("permissionCode", "PermissionCode", "permission", "Permission")
        }
    }

    foreach ($menu in $frontendModule.menus) {
        if (-not $backendMenus.ContainsKey($menu.key)) {
            $errors.Add($frontendModule.moduleCode + ": backend is missing menu -> " + $menu.key)
            continue
        }

        $backendMenu = $backendMenus[$menu.key]
        if ($menu.path -ne $backendMenu.path) {
            $errors.Add($frontendModule.moduleCode + ": menu path mismatch for " + $menu.key + " (frontend=" + $menu.path + ", backend=" + $backendMenu.path + ")")
        }
        if ($menu.permission -ne $backendMenu.permission) {
            $errors.Add($frontendModule.moduleCode + ": menu permission mismatch for " + $menu.key + " (frontend=" + $menu.permission + ", backend=" + $backendMenu.permission + ")")
        }
    }
}

foreach ($backendModule in $backendModules) {
    $moduleCode = Get-JsonValue -Object $backendModule -Names @("moduleCode", "ModuleCode")
    if ([string]::IsNullOrWhiteSpace($moduleCode)) {
        continue
    }

    $existsInFrontend = $frontendModules | Where-Object { $_.moduleCode -eq $moduleCode } | Select-Object -First 1
    if ($null -eq $existsInFrontend) {
        $errors.Add("backend module missing in frontend: " + $moduleCode)
    }
}

if ($errors.Count -gt 0) {
    throw ("Frontend/backend module contract alignment failed:`n - " + ($errors -join "`n - "))
}

Write-Host "Frontend/backend module contract alignment passed."
