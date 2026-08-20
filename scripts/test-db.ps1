$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$environmentPath = Join-Path $repositoryRoot '.env'
$localSettings = @{}
if (Test-Path -LiteralPath $environmentPath) {
    foreach ($line in Get-Content -LiteralPath $environmentPath) {
        if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') {
            $name = $Matches[1]
            $value = $Matches[2]
            $localSettings[$name] = $value
        }
    }

}

function Get-LocalSetting {
    param([Parameter(Mandatory = $true)][string] $Name)

    $processValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue
    }

    return $localSettings[$Name]
}

$required = @(
    'KAGU_ERP_POSTGRES_DB',
    'KAGU_ERP_MIGRATOR_USER',
    'KAGU_ERP_MIGRATOR_PASSWORD',
    'KAGU_ERP_APP_USER',
    'KAGU_ERP_APP_PASSWORD'
)
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace((Get-LocalSetting $name))) {
        throw "Required local setting $name is missing."
    }
}

$database = Get-LocalSetting 'KAGU_ERP_POSTGRES_DB'
$databaseHost = Get-LocalSetting 'KAGU_ERP_POSTGRES_HOST'
if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = '127.0.0.1' }
$databasePort = Get-LocalSetting 'KAGU_ERP_POSTGRES_PORT'
if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = '55432' }
$migratorUser = Get-LocalSetting 'KAGU_ERP_MIGRATOR_USER'
$migratorPassword = Get-LocalSetting 'KAGU_ERP_MIGRATOR_PASSWORD'
$appUser = Get-LocalSetting 'KAGU_ERP_APP_USER'
$appPassword = Get-LocalSetting 'KAGU_ERP_APP_PASSWORD'

$common = "Host=$databaseHost;Port=$databasePort;Database=$database;Pooling=true;Include Error Detail=false"
$env:KAGU_ERP_MIGRATOR_CONNECTION_STRING = "$common;Username=$migratorUser;Password=$migratorPassword;Application Name=KaguERP.Migrator"
$env:KAGU_ERP_APP_CONNECTION_STRING = "$common;Username=$appUser;Password=$appPassword;Application Name=KaguERP.IntegrationChecks"

try {
    dotnet restore KaguERP.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "locked restore failed with exit code $LASTEXITCODE." }
    dotnet build src/Erp.Migrator/KaguERP.Migrator.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database migrator build failed with exit code $LASTEXITCODE." }
    dotnet build tests/Architecture/KaguERP.ArchitectureChecks.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database quality harness build failed with exit code $LASTEXITCODE." }
    dotnet src/Erp.Migrator/bin/Release/net10.0/KaguERP.Migrator.dll
    if ($LASTEXITCODE -ne 0) { throw "database migration failed with exit code $LASTEXITCODE." }
    dotnet src/Erp.Migrator/bin/Release/net10.0/KaguERP.Migrator.dll
    if ($LASTEXITCODE -ne 0) { throw "database migration idempotency check failed with exit code $LASTEXITCODE." }
    dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll database
    if ($LASTEXITCODE -ne 0) { throw "database integration checks failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:KAGU_ERP_MIGRATOR_CONNECTION_STRING -ErrorAction SilentlyContinue
    Remove-Item Env:KAGU_ERP_APP_CONNECTION_STRING -ErrorAction SilentlyContinue
}
