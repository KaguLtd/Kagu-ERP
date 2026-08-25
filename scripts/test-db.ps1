$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$environmentPath = Join-Path $repositoryRoot '.env'
$localSettings = if (Test-Path -LiteralPath $environmentPath) {
    ConvertFrom-StringData (Get-Content -LiteralPath $environmentPath -Raw)
} else {
    @{}
}

$required = @(
    'KAGU_ERP_POSTGRES_DB',
    'KAGU_ERP_MIGRATOR_USER',
    'KAGU_ERP_MIGRATOR_PASSWORD',
    'KAGU_ERP_APP_USER',
    'KAGU_ERP_APP_PASSWORD'
)
$resolvedSettings = @{}
foreach ($requiredSetting in $required) {
    $processValue = [Environment]::GetEnvironmentVariable($requiredSetting)
    $resolvedValue = if ([string]::IsNullOrWhiteSpace($processValue)) {
        $localSettings[$requiredSetting]
    } else {
        $processValue
    }

    if ([string]::IsNullOrWhiteSpace($resolvedValue)) {
        throw "Required local setting $requiredSetting is missing."
    }

    $resolvedSettings[$requiredSetting] = $resolvedValue
}

$database = $resolvedSettings['KAGU_ERP_POSTGRES_DB']
$databaseHost = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_HOST')
if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = $localSettings['KAGU_ERP_POSTGRES_HOST'] }
if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = '127.0.0.1' }
$databasePort = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_PORT')
if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = $localSettings['KAGU_ERP_POSTGRES_PORT'] }
if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = '55432' }
$migratorUser = $resolvedSettings['KAGU_ERP_MIGRATOR_USER']
$migratorPassword = $resolvedSettings['KAGU_ERP_MIGRATOR_PASSWORD']
$appUser = $resolvedSettings['KAGU_ERP_APP_USER']
$appPassword = $resolvedSettings['KAGU_ERP_APP_PASSWORD']

$common = "Host=$databaseHost;Port=$databasePort;Database=$database;Pooling=true;Include Error Detail=false"
$env:KAGU_ERP_MIGRATOR_CONNECTION_STRING = "$common;Username=$migratorUser;Password=$migratorPassword;Application Name=KaguERP.Migrator"
$env:KAGU_ERP_APP_CONNECTION_STRING = "$common;Username=$appUser;Password=$appPassword;Application Name=KaguERP.IntegrationChecks"

try {
    dotnet restore KaguERP.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "locked restore failed with exit code $LASTEXITCODE." }
    dotnet restore tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "standalone integration locked restore failed with exit code $LASTEXITCODE." }
    dotnet build src/Erp.Migrator/KaguERP.Migrator.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database migrator build failed with exit code $LASTEXITCODE." }
    dotnet build tests/Architecture/KaguERP.ArchitectureChecks.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "database quality harness build failed with exit code $LASTEXITCODE." }
    dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "standalone database integration harness build failed with exit code $LASTEXITCODE." }
    # Keep the standalone migrator build as the deployable artifact, but execute the
    # same linked migration code through the established quality harness. Some managed
    # Windows hosts block newly generated standalone assemblies with 0x800711C7.
    dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll migrate
    if ($LASTEXITCODE -ne 0) { throw "database migration failed with exit code $LASTEXITCODE." }
    dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll migrate
    if ($LASTEXITCODE -ne 0) { throw "database migration idempotency check failed with exit code $LASTEXITCODE." }
    dotnet tests/Integration/bin/Release/net10.0/KaguERP.DatabaseIntegrationChecks.dll
    if ($LASTEXITCODE -ne 0) { throw "database integration checks failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:KAGU_ERP_MIGRATOR_CONNECTION_STRING -ErrorAction SilentlyContinue
    Remove-Item Env:KAGU_ERP_APP_CONNECTION_STRING -ErrorAction SilentlyContinue
}
