param(
    [string]$BackendBaseUrl = $(if ($env:UNICORE_BACKEND_BASE_URL) { $env:UNICORE_BACKEND_BASE_URL } else { "http://localhost:5000" }),
    [string]$ModulesRoot = "..\..\modules",
    [string]$Username = $env:UNICORE_ADMIN_USERNAME,
    [securestring]$SecurePassword,
    [System.Management.Automation.PSCredential]$Credential,
    [string]$TenantId = $(if ($env:UNICORE_TENANT_ID) { $env:UNICORE_TENANT_ID } else { "default" }),
    [string]$AccessToken = "",
    [string]$ProtocolVersion = "1.0.0",
    [switch]$FailOnBreaking,
    [switch]$OutputJson,
    [string]$JsonOutputPath = ""
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

function ConvertTo-PlainText {
    param(
        [Parameter(Mandatory = $true)]
        [securestring]$Value
    )

    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Add-Error {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $errors.Add($Message)
}

function Add-ValidationError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleCode,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Add-Error ($ModuleCode + ": " + $Message)
}

$resolvedModulesRoot = Resolve-Path (Join-Path $PSScriptRoot $ModulesRoot)
$frontendModules = @()
$moduleCodePattern = '^[a-z][a-z0-9_-]*$'
$permissionPattern = '^[a-z][a-z0-9_-]*\.[a-z][a-z0-9_-]*$'

foreach ($moduleDir in (Get-ChildItem -LiteralPath $resolvedModulesRoot -Directory | Sort-Object Name)) {
    $packageJsonPath = Join-Path $moduleDir.FullName "package.json"
    if (-not (Test-Path -LiteralPath $packageJsonPath)) {
        continue
    }

    $manifestPath = Join-Path $moduleDir.FullName "manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw ("Cannot validate module '" + $moduleDir.Name + "': missing manifest.json.")
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.moduleCode) {
        throw ("Cannot validate module '" + $moduleDir.Name + "': manifest.json missing moduleCode.")
    }
    if ($manifest.moduleCode -notmatch $moduleCodePattern) {
        throw ("Cannot validate module '" + $moduleDir.Name + "': moduleCode '" + $manifest.moduleCode + "' does not match pattern " + $moduleCodePattern)
    }

    $routes = @()
    foreach ($route in $manifest.routes) {
        $routes += [pscustomobject]@{
            path = $route.path
            permission = $route.permission
        }
    }

    $frontendModules += [pscustomobject]@{
        sourceDir = $moduleDir.Name
        packageName = $packageJson.name
        version = $packageJson.version
        moduleCode = $manifest.moduleCode
        routes = $routes
        routePaths = @($routes | ForEach-Object { $_.path } | Sort-Object -Unique)
        routePermissions = @($routes | ForEach-Object { $_.permission } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    }
}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    $resolvedUsername = $Username
    $resolvedPassword = $null

    if ($null -ne $Credential) {
        $resolvedUsername = $Credential.UserName
        $resolvedPassword = ConvertTo-PlainText -Value $Credential.Password
    }
    elseif ($null -ne $SecurePassword) {
        $resolvedPassword = ConvertTo-PlainText -Value $SecurePassword
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:UNICORE_ADMIN_PASSWORD)) {
        $resolvedPassword = $env:UNICORE_ADMIN_PASSWORD
    }

    if ([string]::IsNullOrWhiteSpace($resolvedUsername) -or [string]::IsNullOrWhiteSpace($resolvedPassword)) {
        throw "AccessToken is empty. Pass -AccessToken, or pass -Credential, or set UNICORE_ADMIN_USERNAME and UNICORE_ADMIN_PASSWORD."
    }

    $loginBody = @{
        username = $resolvedUsername
        password = $resolvedPassword
        tenantId = $TenantId
    } | ConvertTo-Json

    $loginResponse = Invoke-RestMethod -Method Post -Uri ($BackendBaseUrl.TrimEnd("/") + "/api/auth/login") -ContentType "application/json" -Body $loginBody
    $AccessToken = Get-JsonValue -Object $loginResponse.data -Names @("accessToken", "AccessToken")
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw "Failed to obtain access token from backend."
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
$report = [ordered]@{
    checkedAt = [DateTimeOffset]::UtcNow.ToString("O")
    backendBaseUrl = $BackendBaseUrl
    modulesRoot = $resolvedModulesRoot.Path
    frontendModuleCount = $frontendModules.Count
    backendModuleCount = $backendModules.Count
    protocolVersion = $ProtocolVersion
    failOnBreaking = $FailOnBreaking.IsPresent
    passed = $false
    errors = @()
}

if (-not [string]::IsNullOrWhiteSpace($ProtocolVersion)) {
    $breakingQuery = "/api/modules/contracts/report?protocolVersion=$([uri]::EscapeDataString($ProtocolVersion))&failOnBreaking=$($FailOnBreaking.IsPresent.ToString().ToLowerInvariant())"
    $backendValidation = Invoke-RestMethod -Method Get -Uri ($BackendBaseUrl.TrimEnd("/") + $breakingQuery) -Headers $headers
    $backendReport = $backendValidation.data
    if ($null -ne $backendReport) {
        $backendValid = Get-JsonValue -Object $backendReport -Names @("isValid", "IsValid")
        if ($backendValid -eq $false) {
            Add-Error ("Backend module contract report validation failed (protocolVersion=" + $ProtocolVersion + ", failOnBreaking=" + $FailOnBreaking.IsPresent + ")")
        }
    }
}

foreach ($frontendModule in $frontendModules) {
    if ($frontendModule.moduleCode -notmatch $moduleCodePattern) {
        Add-ValidationError -ModuleCode $frontendModule.moduleCode -Message ("moduleCode 必须匹配 " + $moduleCodePattern)
    }

    foreach ($permission in $frontendModule.routePermissions) {
        if ($permission -notmatch $permissionPattern) {
            Add-ValidationError -ModuleCode $frontendModule.moduleCode -Message ("Invalid permission format '" + $permission + "'")
            continue
        }

        if (-not $permission.StartsWith($frontendModule.moduleCode + ".", [StringComparison]::Ordinal)) {
            Add-ValidationError -ModuleCode $frontendModule.moduleCode -Message ("Permission must start with moduleCode prefix: " + $permission)
        }
    }

    if (-not $backendByCode.ContainsKey($frontendModule.moduleCode)) {
        Add-Error ($frontendModule.sourceDir + ": frontend moduleCode '" + $frontendModule.moduleCode + "' not found in backend contract")
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

    $missingInBackend = @($frontendModule.routePermissions | Where-Object { $_ -notin $backendPermissions })
    if ($missingInBackend.Count -gt 0) {
        Add-ValidationError -ModuleCode $frontendModule.moduleCode -Message ("Backend missing route permissions -> " + ($missingInBackend -join ", "))
    }

    $frontendRoutesByPath = @{}
    foreach ($route in $frontendModule.routes) {
        if (-not [string]::IsNullOrWhiteSpace($route.path)) {
            $frontendRoutesByPath[$route.path] = $route
        }
    }

    foreach ($menu in (Get-JsonValue -Object $backendModule -Names @("menus", "Menus"))) {
        $menuKey = Get-JsonValue -Object $menu -Names @("menuCode", "MenuCode", "key", "Key", "routePath", "RoutePath")
        $menuPath = Get-JsonValue -Object $menu -Names @("routePath", "RoutePath", "path", "Path")
        if (-not $menuKey -or -not $menuPath) {
            continue
        }

        if (-not $frontendRoutesByPath.ContainsKey($menuPath)) {
            Add-ValidationError -ModuleCode $frontendModule.moduleCode -Message ("Frontend missing route for backend menu -> " + $menuKey + " (" + $menuPath + ")")
            continue
        }

        $frontendRoute = $frontendRoutesByPath[$menuPath]
        $backendPermission = Get-JsonValue -Object $menu -Names @("permissionCode", "PermissionCode", "permission", "Permission")
        $frontendPermission = $frontendRoute.permission
        $frontendPermissionText = if ([string]::IsNullOrWhiteSpace($frontendPermission)) { "" } else { $frontendPermission }
        $backendPermissionText = if ([string]::IsNullOrWhiteSpace($backendPermission)) { "" } else { $backendPermission }
        if ($frontendPermissionText -ne $backendPermissionText) {
            Add-ValidationError -ModuleCode $frontendModule.moduleCode -Message ("Route permission mismatch " + $menuKey + " (frontend=" + $frontendPermissionText + ", backend=" + $backendPermissionText + ")")
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
        Add-Error ("Frontend missing backend module: " + $moduleCode)
    }
}

$report.errors = @($errors)
$report.passed = ($errors.Count -eq 0)

if ($OutputJson) {
    $targetPath = if ([string]::IsNullOrWhiteSpace($JsonOutputPath)) {
        Join-Path $PSScriptRoot "module-contract-alignment-report.json"
    } elseif ([System.IO.Path]::IsPathRooted($JsonOutputPath)) {
        $JsonOutputPath
    } else {
        $trimmed = $JsonOutputPath.TrimStart()
        # In npm CI, a path like ./artifacts/... is relative to platform-admin root.
        # Joining directly to $PSScriptRoot would incorrectly nest it under scripts/.
        if ($trimmed.StartsWith("./") -or $trimmed.StartsWith(".\")) {
            $relativeFromAdmin = $trimmed.Substring(2).TrimStart([char[]]@('/', '\'))
            $adminRoot = Split-Path -Parent $PSScriptRoot
            Join-Path $adminRoot $relativeFromAdmin
        } else {
            Join-Path $PSScriptRoot $JsonOutputPath
        }
    }
    $targetDirectory = Split-Path -Parent $targetPath
    if (-not [string]::IsNullOrWhiteSpace($targetDirectory) -and -not (Test-Path -LiteralPath $targetDirectory)) {
        New-Item -Path $targetDirectory -ItemType Directory -Force | Out-Null
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $targetPath -Encoding UTF8
    Write-Host ("Alignment report written to " + $targetPath)
}

if ($errors.Count -gt 0) {
    throw ("Frontend/backend module contract alignment failed:`n - " + ($errors -join "`n - "))
}

Write-Host "Frontend/backend module contract alignment passed."
