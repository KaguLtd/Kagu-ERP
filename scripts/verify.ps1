$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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

function Find-AndroidSdk {
    $candidates = @(
        [Environment]::GetEnvironmentVariable('ANDROID_HOME'),
        [Environment]::GetEnvironmentVariable('ANDROID_SDK_ROOT'),
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk')
    )

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate -PathType Container)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

dotnet restore KaguERP.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
dotnet build KaguERP.slnx --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll
if ($LASTEXITCODE -ne 0) { throw "architecture checks failed with exit code $LASTEXITCODE." }
dotnet format KaguERP.slnx --no-restore --verify-no-changes --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet format failed with exit code $LASTEXITCODE." }

pnpm install --frozen-lockfile
if ($LASTEXITCODE -ne 0) { throw "pnpm install failed with exit code $LASTEXITCODE." }
pnpm verify
if ($LASTEXITCODE -ne 0) { throw "pnpm verify failed with exit code $LASTEXITCODE." }

if (Enable-DockerCommand) {
    $runningServices = docker compose ps --services --filter status=running
    if ($LASTEXITCODE -ne 0) { throw "docker compose ps failed with exit code $LASTEXITCODE." }

    if ($runningServices -contains 'erp-db') {
        & ./scripts/test-db.ps1
    } else {
        Write-Warning 'Database integration checks skipped: the Kagu ERP erp-db Compose service is not running.'
    }

    if ($runningServices -contains 'keycloak') {
        & ./scripts/test-auth.ps1
    } else {
        Write-Warning 'Authentication smoke checks skipped: the Kagu ERP keycloak Compose service is not running.'
    }

    if (($runningServices -contains 'erp-db') -and ($runningServices -contains 'keycloak')) {
        & ./scripts/test-restore.ps1
    } else {
        Write-Warning 'Restore smoke checks skipped: ERP DB and Keycloak Compose services must both be running.'
    }
} else {
    Write-Warning 'Database integration checks skipped: Docker is unavailable.'
    Write-Warning 'Authentication smoke checks skipped: Docker is unavailable.'
    Write-Warning 'Restore smoke checks skipped: Docker is unavailable.'
}

$javaCommand = Get-Command java -ErrorAction SilentlyContinue
$androidSdk = Find-AndroidSdk

if ($null -eq $javaCommand -or [string]::IsNullOrWhiteSpace($androidSdk)) {
    Write-Warning 'Android lint/unit/instrumentation build skipped: JDK 17 and Android SDK are required. This is not a passing Android result.'
} else {
    $javaVersion = (& $javaCommand.Source -version 2>&1 | Select-Object -First 1).ToString()
    if ($javaVersion -notmatch 'version "17(?:[.]|"|$)') {
        throw "Android verification requires JDK 17; active Java is: $javaVersion"
    }

    $requiredSdkFiles = @(
        (Join-Path $androidSdk 'platforms\android-37.0\android.jar'),
        (Join-Path $androidSdk 'build-tools\36.0.0\aapt2.exe')
    )
    $missingSdkFiles = @($requiredSdkFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($missingSdkFiles.Count -gt 0) {
        throw "Android SDK is incomplete; required files are missing: $($missingSdkFiles -join ', ')"
    }

    $env:ANDROID_HOME = $androidSdk
    $env:ANDROID_SDK_ROOT = $androidSdk
    Push-Location apps/android
    try {
        & .\gradlew.bat lintDebug testDebugUnitTest assembleDebugAndroidTest --no-daemon
        if ($LASTEXITCODE -ne 0) { throw "Android verification failed with exit code $LASTEXITCODE." }
    } finally {
        Pop-Location
    }
}
