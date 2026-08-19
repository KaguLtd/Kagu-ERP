$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (Test-Path -LiteralPath '.env') {
    foreach ($line in Get-Content -LiteralPath '.env') {
        if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') {
            $name = $Matches[1]
            if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name))) {
                [Environment]::SetEnvironmentVariable($name, $Matches[2])
            }
        }
    }
}

$required = @(
    'KAGU_ERP_POSTGRES_DB',
    'KAGU_ERP_MIGRATOR_USER',
    'KAGU_ERP_MIGRATOR_PASSWORD',
    'KAGU_ERP_APP_USER',
    'KAGU_ERP_APP_PASSWORD'
)
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "Required local setting $name is missing."
    }
}

$database = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_DB')
$databaseHost = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_HOST')
if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = '127.0.0.1' }
$databasePort = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_PORT')
if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = '55432' }
$migratorUser = [Environment]::GetEnvironmentVariable('KAGU_ERP_MIGRATOR_USER')
$migratorPassword = [Environment]::GetEnvironmentVariable('KAGU_ERP_MIGRATOR_PASSWORD')
$appUser = [Environment]::GetEnvironmentVariable('KAGU_ERP_APP_USER')
$appPassword = [Environment]::GetEnvironmentVariable('KAGU_ERP_APP_PASSWORD')

$common = "Host=$databaseHost;Port=$databasePort;Database=$database;Pooling=true;Include Error Detail=false"
$env:KAGU_ERP_MIGRATOR_CONNECTION_STRING = "$common;Username=$migratorUser;Password=$migratorPassword;Application Name=KaguERP.Migrator"
$env:KAGU_ERP_APP_CONNECTION_STRING = "$common;Username=$appUser;Password=$appPassword;Application Name=KaguERP.IntegrationChecks"

try {
    dotnet restore KaguERP.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "locked restore failed with exit code $LASTEXITCODE." }
    dotnet run --project src/Erp.Migrator/KaguERP.Migrator.csproj --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database migration failed with exit code $LASTEXITCODE." }
    dotnet run --project src/Erp.Migrator/KaguERP.Migrator.csproj --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database migration idempotency check failed with exit code $LASTEXITCODE." }
    dotnet run --project tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database integration checks failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:KAGU_ERP_MIGRATOR_CONNECTION_STRING -ErrorAction SilentlyContinue
    Remove-Item Env:KAGU_ERP_APP_CONNECTION_STRING -ErrorAction SilentlyContinue
}
