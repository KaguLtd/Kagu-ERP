param(
    [switch] $SkipServices
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require-Command {
    param([Parameter(Mandatory = $true)][string] $Name)

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function New-LocalSecret {
    $bytes = New-Object byte[] 24
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    } finally {
        $generator.Dispose()
    }

    return ([Convert]::ToBase64String($bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Enable-DockerCommand {
    $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
    if ($null -ne $dockerCommand) {
        return $true
    }

    $perUserDockerBin = Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\resources\bin'
    $perUserDocker = Join-Path $perUserDockerBin 'docker.exe'
    if (Test-Path -LiteralPath $perUserDocker) {
        $env:PATH = "$perUserDockerBin;$env:PATH"
        return $true
    }

    return $false
}

Require-Command git
Require-Command dotnet
Require-Command node
Require-Command pnpm

if (-not (Test-Path -LiteralPath '.env')) {
    $erpPassword = New-LocalSecret
    $erpMigratorPassword = New-LocalSecret
    $erpAppPassword = New-LocalSecret
    $keycloakDbPassword = New-LocalSecret
    $keycloakAdminPassword = New-LocalSecret
    $keycloakSmokePassword = New-LocalSecret

    $environmentFile = @"
# Generated for local development by scripts/bootstrap.ps1. Never commit this file.
KAGU_ERP_POSTGRES_DB=kagu_erp
KAGU_ERP_POSTGRES_USER=kagu_erp_local_owner
KAGU_ERP_POSTGRES_PASSWORD=$erpPassword
KAGU_ERP_MIGRATOR_USER=kagu_erp_migrator
KAGU_ERP_MIGRATOR_PASSWORD=$erpMigratorPassword
KAGU_ERP_APP_USER=kagu_erp_app
KAGU_ERP_APP_PASSWORD=$erpAppPassword

KEYCLOAK_POSTGRES_DB=keycloak
KEYCLOAK_POSTGRES_USER=keycloak_local_owner
KEYCLOAK_POSTGRES_PASSWORD=$keycloakDbPassword

KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME=kagu-local-admin
KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD=$keycloakAdminPassword
KAGU_KEYCLOAK_SMOKE_PASSWORD=$keycloakSmokePassword
"@

    Set-Content -LiteralPath '.env' -Value $environmentFile -Encoding utf8NoBOM
    Write-Output 'Generated ignored .env with random local-only credentials.'
} else {
    $existingEnvironment = Get-Content -LiteralPath '.env' -Raw
    $missingValues = [System.Collections.Generic.List[string]]::new()

    if ($existingEnvironment -notmatch '(?m)^KAGU_ERP_MIGRATOR_USER=') {
        $missingValues.Add('KAGU_ERP_MIGRATOR_USER=kagu_erp_migrator')
    }

    if ($existingEnvironment -notmatch '(?m)^KAGU_ERP_MIGRATOR_PASSWORD=') {
        $missingValues.Add("KAGU_ERP_MIGRATOR_PASSWORD=$(New-LocalSecret)")
    }

    if ($existingEnvironment -notmatch '(?m)^KAGU_ERP_APP_USER=') {
        $missingValues.Add('KAGU_ERP_APP_USER=kagu_erp_app')
    }

    if ($existingEnvironment -notmatch '(?m)^KAGU_ERP_APP_PASSWORD=') {
        $missingValues.Add("KAGU_ERP_APP_PASSWORD=$(New-LocalSecret)")
    }

    if ($existingEnvironment -notmatch '(?m)^KAGU_KEYCLOAK_SMOKE_PASSWORD=') {
        $missingValues.Add("KAGU_KEYCLOAK_SMOKE_PASSWORD=$(New-LocalSecret)")
    }

    if ($missingValues.Count -gt 0) {
        Add-Content -LiteralPath '.env' -Value ("`n" + ($missingValues -join "`n")) -Encoding utf8NoBOM
        Write-Output 'Existing .env values preserved; missing local development settings were added.'
    } else {
        Write-Output 'Existing .env preserved.'
    }
}

dotnet restore KaguERP.slnx --locked-mode
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

pnpm install --frozen-lockfile
if ($LASTEXITCODE -ne 0) {
    throw "pnpm install failed with exit code $LASTEXITCODE."
}

if ($SkipServices) {
    Write-Output 'Service bootstrap skipped by request; dependency installation completed.'
} elseif (-not (Enable-DockerCommand)) {
    Write-Warning 'Docker is unavailable; local PostgreSQL and Keycloak were not started.'
} else {
    docker compose config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config failed with exit code $LASTEXITCODE."
    }

    docker compose up --detach --wait
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed with exit code $LASTEXITCODE."
    }

    & ./scripts/test-db.ps1
}

Write-Output 'Bootstrap completed. Run scripts/verify.ps1 for all available quality gates.'
