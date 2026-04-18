param(
    [Parameter(Mandatory = $true)]
    [string]$Host,
    [int]$Port = 5432,
    [Parameter(Mandatory = $true)]
    [string]$AdminUser,
    [Parameter(Mandatory = $true)]
    [SecureString]$AdminPassword,
    [Parameter(Mandatory = $true)]
    [string]$AppUser,
    [Parameter(Mandatory = $true)]
    [SecureString]$AppPassword,
    [Parameter(Mandatory = $true)]
    [string]$DatabaseName
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host "[init-postgres] $message" -ForegroundColor Green
}

function Invoke-Psql([string]$sql, [string]$db = "postgres") {
    $psqlArgs = @(
        "-h", $Host,
        "-p", $Port,
        "-U", $AdminUser,
        "-d", $db,
        "-v", "ON_ERROR_STOP=1",
        "-c", $sql
    )
    & psql @psqlArgs
    if ($LASTEXITCODE -ne 0) {
        throw "psql 执行失败，数据库: $db"
    }
}

$escapedAppUser = $AppUser.Replace("'", "''")
$escapedDbName = $DatabaseName.Replace("'", "''")

$adminPasswordPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
$appPasswordPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($AppPassword)
$adminPasswordPlain = $null
$appPasswordPlain = $null

try {
    $adminPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($adminPasswordPtr)
    $appPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($appPasswordPtr)
}
finally {
    if ($adminPasswordPtr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($adminPasswordPtr)
    }
    if ($appPasswordPtr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($appPasswordPtr)
    }
}

$escapedAppPassword = $appPasswordPlain.Replace("'", "''")
$env:PGPASSWORD = $adminPasswordPlain
try {
    Write-Step "Ensure role exists: $AppUser"
    Invoke-Psql @"
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$escapedAppUser') THEN
        EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', '$escapedAppUser', '$escapedAppPassword');
    END IF;
END
\$\$;
"@

    Write-Step "Ensure database exists: $DatabaseName"
    Invoke-Psql @"
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = '$escapedDbName') THEN
        EXECUTE format('CREATE DATABASE %I OWNER %I', '$escapedDbName', '$escapedAppUser');
    END IF;
END
\$\$;
"@

    Write-Step "Grant privileges on database"
    Invoke-Psql "GRANT ALL PRIVILEGES ON DATABASE `"$DatabaseName`" TO `"$AppUser`";"

    Write-Step "Set schema permissions and default privileges"
    Invoke-Psql "GRANT USAGE, CREATE ON SCHEMA public TO `"$AppUser`";" $DatabaseName
    Invoke-Psql "ALTER SCHEMA public OWNER TO `"$AppUser`";" $DatabaseName
    Invoke-Psql "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO `"$AppUser`";" $DatabaseName
    Invoke-Psql "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO `"$AppUser`";" $DatabaseName
    Invoke-Psql "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO `"$AppUser`";" $DatabaseName

    Write-Step "Done. Next: run EF migrations."
    Write-Host "dotnet ef database update --project src/Platform.Infrastructure/Platform.Infrastructure.csproj --startup-project src/Platform.WebApi/Platform.WebApi.csproj" -ForegroundColor DarkGray
}
finally {
    $adminPasswordPlain = $null
    $appPasswordPlain = $null
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
