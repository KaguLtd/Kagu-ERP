$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$environmentPath = Join-Path $repositoryRoot '.env'
$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentPath) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') {
        $settings[$Matches[1]] = $Matches[2]
    }
}

$ownerUser = $settings['KAGU_ERP_POSTGRES_USER']
if ([string]::IsNullOrWhiteSpace($ownerUser)) {
    throw 'Local PostgreSQL owner is required for the empty-database migration check.'
}

$emptyDatabase = "kagu_erp_empty_$([Guid]::NewGuid().ToString('N'))"
if ($emptyDatabase -notmatch '^kagu_erp_empty_[0-9a-f]{32}$') {
    throw 'Generated empty database name failed the safety check.'
}

$previousDatabase = [Environment]::GetEnvironmentVariable('KAGU_ERP_POSTGRES_DB')
$databaseCreated = $false
try {
    docker compose exec -T erp-db createdb --username $ownerUser `
        --owner kagu_erp_schema_owner $emptyDatabase
    if ($LASTEXITCODE -ne 0) { throw "empty database creation failed with exit code $LASTEXITCODE." }
    $databaseCreated = $true

    $env:KAGU_ERP_POSTGRES_DB = $emptyDatabase
    & ./scripts/test-db.ps1
    Write-Output 'Empty PostgreSQL migration, idempotency, RLS and integration checks passed.'
} finally {
    if ($databaseCreated -and $emptyDatabase -match '^kagu_erp_empty_[0-9a-f]{32}$') {
        docker compose exec -T erp-db dropdb --username $ownerUser --if-exists --force $emptyDatabase
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Temporary empty database cleanup failed with exit code $LASTEXITCODE."
        }
    }

    [Environment]::SetEnvironmentVariable('KAGU_ERP_POSTGRES_DB', $previousDatabase)
}
