$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$environmentPath = Join-Path $repositoryRoot '.env'
$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentPath) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') {
        $key = $Matches[1]
        $value = $Matches[2]
        $settings[$key] = $value
    }
}

$sourceDatabase = $settings['KAGU_ERP_POSTGRES_DB']
$ownerUser = $settings['KAGU_ERP_POSTGRES_USER']
if ([string]::IsNullOrWhiteSpace($sourceDatabase) -or [string]::IsNullOrWhiteSpace($ownerUser)) {
    throw 'Local ERP database name and owner are required for restore smoke.'
}

if ($sourceDatabase -match '^kagu_erp_restore_') {
    throw 'A restore smoke cannot use another restore database as its source.'
}

$restoreDatabase = "kagu_erp_restore_$([Guid]::NewGuid().ToString('N'))"
$dumpPath = "/tmp/$restoreDatabase.dump"
if ($restoreDatabase -notmatch '^kagu_erp_restore_[0-9a-f]{32}$') {
    throw 'Generated restore database name failed the safety check.'
}

$previousDatabase = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_DB')
$restoreCreated = $false
try {
    docker compose exec -T erp-db pg_dump --username $ownerUser --dbname $sourceDatabase `
        --format custom --file $dumpPath
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit code $LASTEXITCODE." }

    docker compose exec -T erp-db createdb --username $ownerUser `
        --owner kagu_erp_schema_owner $restoreDatabase
    if ($LASTEXITCODE -ne 0) { throw "restore database creation failed with exit code $LASTEXITCODE." }
    $restoreCreated = $true

    docker compose exec -T erp-db pg_restore --username $ownerUser --dbname $restoreDatabase `
        --exit-on-error $dumpPath
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed with exit code $LASTEXITCODE." }

    $env:KAGU_ERP_POSTGRES_DB = $restoreDatabase
    & ./scripts/test-db.ps1
    & ./scripts/test-auth.ps1
    Write-Output 'Isolated local PostgreSQL restore, migration, scope, outbox and auth smoke checks passed.'
} finally {
    if ($restoreCreated -and $restoreDatabase -match '^kagu_erp_restore_[0-9a-f]{32}$') {
        docker compose exec -T erp-db dropdb --username $ownerUser --if-exists --force $restoreDatabase
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Temporary restore database cleanup failed with exit code $LASTEXITCODE."
        }
    }

    if ($dumpPath -match '^/tmp/kagu_erp_restore_[0-9a-f]{32}[.]dump$') {
        docker compose exec -T erp-db rm -f $dumpPath
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Temporary restore dump cleanup failed with exit code $LASTEXITCODE."
        }
    }

    [Environment]::SetEnvironmentVariable('KAGU_ERP_POSTGRES_DB', $previousDatabase)
}
