#!/usr/bin/env bash
# Verifies every expected package is still listed on NuGet, then restores and
# builds all of them together in a fresh app on each target framework.
# Catches unlisting, broken publishes, and dependency conflicts between packages.
set -uo pipefail

REPO=$(pwd)
DATE=$(date -u +%Y-%m-%d)
STATS="${REPO}/stats/${DATE}.json"
EXPECTED="${REPO}/.github/scripts/expected-packages.txt"
FAILED=0

# 1. Listing check
while read -r id; do
  [ -z "$id" ] && continue
  case "$id" in \#*) continue ;; esac
  if ! jq -e --arg id "$id" '.packages[] | select(.id==$id)' "$STATS" > /dev/null; then
    echo "::error::${id} is missing from NuGet search. Unlisted, delisted, or not yet indexed."
    FAILED=1
  fi
done < "$EXPECTED"

# 2. Restore and build check, once per framework
for TFM in net8.0 net10.0; do
  WORK=$(mktemp -d)
  cd "$WORK"
  # The web template uses Microsoft.NET.Sdk.Web, which handles the static web
  # assets shipped by BlazorMemory.Components. A console template would not.
  dotnet new web -n HealthCheck -o . --framework "$TFM" > /dev/null

  while read -r id; do
    [ -z "$id" ] && continue
    case "$id" in \#*) continue ;; esac
    if ! dotnet add package "$id" > /dev/null 2>&1; then
      echo "::error::Restore failed for ${id} on ${TFM}"
      FAILED=1
    fi
  done < "$EXPECTED"

  if ! dotnet build -c Release > build.log 2>&1; then
    echo "::error::Combined build failed on ${TFM}"
    tail -40 build.log
    FAILED=1
  else
    echo "Combined build passed on ${TFM}"
  fi

  cd "$REPO"
  rm -rf "$WORK"
done

exit $FAILED
