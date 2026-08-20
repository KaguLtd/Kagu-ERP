#!/usr/bin/env bash
set -euo pipefail

dotnet restore KaguERP.slnx --locked-mode
dotnet build KaguERP.slnx --configuration Release --no-restore
dotnet tests/Architecture/bin/Release/net10.0/KaguERP.ArchitectureChecks.dll
dotnet format KaguERP.slnx --no-restore --verify-no-changes --verbosity minimal

pnpm install --frozen-lockfile
pnpm verify

if command -v docker >/dev/null 2>&1; then
  running_services="$(docker compose ps --services --filter status=running)"
  if [[ "$running_services" == *"erp-db"* ]]; then
    ./scripts/test-db.sh
    ./scripts/test-restore.sh
  else
    printf '%s\n' 'WARNING: Database integration checks skipped: the Kagu ERP erp-db Compose service is not running.' >&2
  fi
else
  printf '%s\n' 'WARNING: Database integration checks skipped: Docker is unavailable.' >&2
fi

android_sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [[ -z "$android_sdk" ]] && [[ -d "${HOME}/Android/Sdk" ]]; then
  android_sdk="${HOME}/Android/Sdk"
elif [[ -z "$android_sdk" ]] && [[ -d "${HOME}/Library/Android/sdk" ]]; then
  android_sdk="${HOME}/Library/Android/sdk"
fi

if command -v java >/dev/null 2>&1 && [[ -n "$android_sdk" ]]; then
  java_version="$(java -version 2>&1 | head -n 1)"
  if [[ ! "$java_version" =~ version\ \"17([.]|\"|$) ]]; then
    printf 'Android verification requires JDK 17; active Java is: %s\n' "$java_version" >&2
    exit 1
  fi

  required_sdk_files=(
    "${android_sdk}/platforms/android-37.0/android.jar"
    "${android_sdk}/build-tools/36.0.0/aapt2"
  )
  for required_sdk_file in "${required_sdk_files[@]}"; do
    if [[ ! -f "$required_sdk_file" ]]; then
      printf 'Android SDK is incomplete; required file is missing: %s\n' "$required_sdk_file" >&2
      exit 1
    fi
  done

  export ANDROID_HOME="$android_sdk"
  export ANDROID_SDK_ROOT="$android_sdk"
  (
    cd apps/android
    ./gradlew lintDebug testDebugUnitTest assembleDebugAndroidTest --no-daemon
  )
else
  printf '%s\n' 'WARNING: Android lint/unit/instrumentation build skipped: JDK 17 and Android SDK are required. This is not a passing Android result.' >&2
fi
