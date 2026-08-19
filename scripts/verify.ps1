$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

dotnet restore KaguERP.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
dotnet build KaguERP.slnx --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
dotnet run --project tests/Architecture/KaguERP.ArchitectureChecks.csproj --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw "architecture checks failed with exit code $LASTEXITCODE." }
dotnet format KaguERP.slnx --no-restore --verify-no-changes --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet format failed with exit code $LASTEXITCODE." }

pnpm install --frozen-lockfile
if ($LASTEXITCODE -ne 0) { throw "pnpm install failed with exit code $LASTEXITCODE." }
pnpm verify
if ($LASTEXITCODE -ne 0) { throw "pnpm verify failed with exit code $LASTEXITCODE." }

$javaCommand = Get-Command java -ErrorAction SilentlyContinue
$androidSdk = [Environment]::GetEnvironmentVariable('ANDROID_HOME')
if ([string]::IsNullOrWhiteSpace($androidSdk)) {
    $androidSdk = [Environment]::GetEnvironmentVariable('ANDROID_SDK_ROOT')
}

if ($null -eq $javaCommand -or [string]::IsNullOrWhiteSpace($androidSdk)) {
    Write-Warning 'Android lint/unit tests skipped: JDK 17 and Android SDK are required. This is an open MP-02 gate, not a passing Android result.'
} else {
    Push-Location apps/android
    try {
        & .\gradlew.bat lintDebug testDebugUnitTest --no-daemon
        if ($LASTEXITCODE -ne 0) { throw "Android verification failed with exit code $LASTEXITCODE." }
    } finally {
        Pop-Location
    }
}
