#!/usr/bin/env bash
set -euo pipefail

dotnet restore KaguERP.slnx --locked-mode
dotnet build KaguERP.slnx --configuration Release --no-restore
dotnet run --project tests/Architecture/KaguERP.ArchitectureChecks.csproj --configuration Release --no-build
dotnet format KaguERP.slnx --no-restore --verify-no-changes --verbosity minimal

pnpm install --frozen-lockfile
pnpm verify

android_sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if command -v java >/dev/null 2>&1 && [[ -n "$android_sdk" ]]; then
  (
    cd apps/android
    ./gradlew lintDebug testDebugUnitTest --no-daemon
  )
else
  printf '%s\n' 'WARNING: Android lint/unit tests skipped: JDK 17 and Android SDK are required. This is an open MP-02 gate, not a passing Android result.' >&2
fi
