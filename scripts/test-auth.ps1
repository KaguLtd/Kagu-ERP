$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$environmentPath = Join-Path $repositoryRoot '.env'

function Invoke-ProtectedEndpoint {
    param(
        [Parameter(Mandatory = $true)][string] $ApiBaseUrl,
        [Parameter(Mandatory = $true)][string] $AccessToken
    )

    try {
        $response = Invoke-WebRequest -TimeoutSec 15 -UseBasicParsing `
            -Headers @{ Authorization = "Bearer $AccessToken" } `
            "$ApiBaseUrl/api/v1/me/scopes" -ErrorAction Stop
        return [pscustomobject]@{ Status = 200; Code = ''; Body = ($response.Content | ConvertFrom-Json) }
    } catch {
        $status = [int]$_.Exception.Response.StatusCode
        $problem = $_.ErrorDetails.Message | ConvertFrom-Json
        return [pscustomobject]@{ Status = $status; Code = $problem.code; Body = $null }
    }
}

function Read-JwtPayload {
    param([Parameter(Mandatory = $true)][string] $AccessToken)

    $parts = $AccessToken.Split('.')
    if ($parts.Length -ne 3) {
        throw 'Keycloak returned a malformed access token.'
    }

    $encoded = $parts[1].Replace('-', '+').Replace('_', '/')
    switch ($encoded.Length % 4) {
        2 { $encoded += '==' }
        3 { $encoded += '=' }
    }

    return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded)) | ConvertFrom-Json
}

$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentPath) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') {
        $name = $Matches[1]
        $value = $Matches[2]
        $settings[$name] = $value
    }
}

function Get-LocalSetting {
    param([Parameter(Mandatory = $true)][string] $Name)

    $processValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue
    }

    return $settings[$Name]
}

$smokePassword = Get-LocalSetting 'KAGU_KEYCLOAK_SMOKE_PASSWORD'
if ([string]::IsNullOrWhiteSpace($smokePassword)) {
    throw 'KAGU_KEYCLOAK_SMOKE_PASSWORD is missing. Run scripts/bootstrap.ps1.'
}

$authority = 'http://localhost:58080/realms/kagu-local-test'
$tokenEndpoint = 'http://127.0.0.1:58080/realms/kagu-local-test/protocol/openid-connect/token'
$apiBaseUrl = 'http://127.0.0.1:55099'
$apiProcess = $null
$fixtureSeeded = $false
$previousEnvironment = @{
    ASPNETCORE_URLS = [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS')
    Authentication__Authority = [Environment]::GetEnvironmentVariable('Authentication__Authority')
    Authentication__Audience = [Environment]::GetEnvironmentVariable('Authentication__Audience')
    Authentication__RequireHttpsMetadata = [Environment]::GetEnvironmentVariable('Authentication__RequireHttpsMetadata')
    KAGU_ERP_MIGRATOR_CONNECTION_STRING = [Environment]::GetEnvironmentVariable('KAGU_ERP_MIGRATOR_CONNECTION_STRING')
    KAGU_ERP_APP_CONNECTION_STRING = [Environment]::GetEnvironmentVariable('KAGU_ERP_APP_CONNECTION_STRING')
    KAGU_ERP_AUTH_SMOKE_ISSUER = [Environment]::GetEnvironmentVariable('KAGU_ERP_AUTH_SMOKE_ISSUER')
    KAGU_ERP_AUTH_SMOKE_SUBJECT = [Environment]::GetEnvironmentVariable('KAGU_ERP_AUTH_SMOKE_SUBJECT')
}

try {
    $discovery = Invoke-RestMethod -TimeoutSec 10 -Uri "$authority/.well-known/openid-configuration"
    if ($discovery.issuer -ne $authority) {
        throw 'Local Keycloak discovery issuer does not match the configured authority.'
    }

    $correctToken = Invoke-RestMethod -TimeoutSec 10 -Method Post -Uri $tokenEndpoint `
        -ContentType 'application/x-www-form-urlencoded' -Body @{
            client_id = 'kagu-erp-local-smoke'
            grant_type = 'password'
            username = 'kagu-local-smoke'
            password = $smokePassword
        }
    $wrongAudienceToken = Invoke-RestMethod -TimeoutSec 10 -Method Post -Uri $tokenEndpoint `
        -ContentType 'application/x-www-form-urlencoded' -Body @{
            client_id = 'admin-cli'
            grant_type = 'password'
            username = 'kagu-local-smoke'
            password = $smokePassword
        }

    $tokenPayload = Read-JwtPayload $correctToken.access_token
    if ($tokenPayload.iss -ne $authority -or [string]::IsNullOrWhiteSpace($tokenPayload.sub)) {
        throw 'Keycloak token does not contain the expected issuer and subject.'
    }

    $databaseHost = Get-LocalSetting 'KAGU_ERP_POSTGRES_HOST'
    if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = '127.0.0.1' }
    $databasePort = Get-LocalSetting 'KAGU_ERP_POSTGRES_PORT'
    if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = '55432' }
    $common = "Host=$databaseHost;Port=$databasePort;Database=$(Get-LocalSetting 'KAGU_ERP_POSTGRES_DB');Pooling=true;Include Error Detail=false"
    $env:KAGU_ERP_MIGRATOR_CONNECTION_STRING = "$common;Username=$(Get-LocalSetting 'KAGU_ERP_MIGRATOR_USER');Password=$(Get-LocalSetting 'KAGU_ERP_MIGRATOR_PASSWORD');Application Name=KaguERP.AuthSmokeFixture"
    $env:KAGU_ERP_APP_CONNECTION_STRING = "$common;Username=$(Get-LocalSetting 'KAGU_ERP_APP_USER');Password=$(Get-LocalSetting 'KAGU_ERP_APP_PASSWORD');Application Name=KaguERP.AuthSmokeApi"
    $env:KAGU_ERP_AUTH_SMOKE_ISSUER = $tokenPayload.iss
    $env:KAGU_ERP_AUTH_SMOKE_SUBJECT = $tokenPayload.sub

    dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll seed-auth-smoke
    if ($LASTEXITCODE -ne 0) {
        throw "Authentication smoke fixture seed failed with exit code $LASTEXITCODE."
    }
    $fixtureSeeded = $true

    $occupiedPort = Get-NetTCPConnection -LocalPort 55099 -State Listen -ErrorAction SilentlyContinue
    if ($null -ne $occupiedPort) {
        throw 'Local auth smoke port 55099 is already in use.'
    }

    $env:ASPNETCORE_URLS = $apiBaseUrl
    $env:Authentication__Authority = $authority
    $env:Authentication__Audience = 'kagu-erp-api'
    $env:Authentication__RequireHttpsMetadata = 'false'
    $apiProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('src/Erp.Api/bin/Release/net10.0/KaguERP.Api.dll') `
        -PassThru -WindowStyle Hidden

    $healthy = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($apiProcess.HasExited) {
            throw "Local API exited during auth smoke startup with code $($apiProcess.ExitCode)."
        }

        try {
            $health = Invoke-WebRequest -TimeoutSec 2 -UseBasicParsing "$apiBaseUrl/health/live"
            if ($health.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        } catch {
            # Startup polling intentionally ignores connection failures until the bounded deadline.
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $healthy) {
        throw 'Local API did not become healthy for the auth smoke test.'
    }

    $readiness = Invoke-WebRequest -TimeoutSec 5 -UseBasicParsing "$apiBaseUrl/health/ready"
    if ($readiness.StatusCode -ne 200 -or ($readiness.Content | ConvertFrom-Json).status -ne 'ready') {
        throw 'API readiness did not report the configured PostgreSQL dependency as ready.'
    }

    $validResult = Invoke-ProtectedEndpoint $apiBaseUrl $correctToken.access_token
    $wrongAudienceResult = Invoke-ProtectedEndpoint $apiBaseUrl $wrongAudienceToken.access_token

    if ($validResult.Status -ne 200 -or $validResult.Body.companyIds.Count -ne 1) {
        throw 'A valid Keycloak identity did not resolve to its ERP company permission scope.'
    }

    if ($wrongAudienceResult.Status -ne 401 -or $wrongAudienceResult.Code -ne 'AUTHENTICATION_REQUIRED') {
        throw 'A token without the ERP API audience was not rejected at authentication.'
    }

    Write-Output 'Keycloak token-to-ERP tenant/company/permission smoke checks passed.'
} finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
    }

    if ($fixtureSeeded) {
        dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll cleanup-auth-smoke
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Authentication smoke fixture cleanup failed with exit code $LASTEXITCODE."
        }
    }

    foreach ($entry in $previousEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }
}
