#!/usr/bin/env bash
# Collects download stats for every package owned by OWNER and appends them
# to stats/history.csv. Safe to re-run on the same day: that day's rows are replaced.
set -euo pipefail

OWNER="aftabkh4n"
DATE=$(date -u +%Y-%m-%d)
mkdir -p stats

SEARCH=$(curl -fsS https://api.nuget.org/v3/index.json \
  | jq -r '[.resources[] | select(."@type"=="SearchQueryService")][0]."@id"')

RAW=$(mktemp)
curl -fsS "${SEARCH}?q=owner:${OWNER}&take=100&prerelease=false&semVerLevel=2.0.0" -o "$RAW"

COUNT=$(jq '.data | length' "$RAW")
if [ "$COUNT" -eq 0 ]; then
  echo "::error::NuGet search returned no packages for owner ${OWNER}"
  exit 1
fi

# Trimmed snapshot: the raw response carries full descriptions and grows fast.
jq --arg d "$DATE" '{date: $d, packages: [.data[] | {id, version, totalDownloads, verified}] | sort_by(.id)}' \
  "$RAW" > "stats/${DATE}.json"

HISTORY=stats/history.csv
[ -f "$HISTORY" ] || echo "date,package,version,downloads" > "$HISTORY"

# Drop any rows already written today, then append fresh ones.
grep -v "^\"${DATE}\"," "$HISTORY" > "${HISTORY}.tmp" || true
mv "${HISTORY}.tmp" "$HISTORY"
jq -r '.date as $d | .packages[] | [$d, .id, .version, .totalDownloads] | @csv' \
  "stats/${DATE}.json" >> "$HISTORY"

TOTAL=$(jq '[.packages[].totalDownloads] | add' "stats/${DATE}.json")

# Previous day's total, if any, for a delta in the run summary.
PREV_DATE=$(tail -n +2 "$HISTORY" | cut -d, -f1 | tr -d '"' | sort -u | grep -v "^${DATE}$" | tail -1 || true)
if [ -n "${PREV_DATE}" ]; then
  PREV_TOTAL=$(awk -F, -v d="\"${PREV_DATE}\"" '$1==d {s+=$4} END {print s+0}' "$HISTORY")
  DELTA=$((TOTAL - PREV_TOTAL))
else
  PREV_DATE="none"
  DELTA="n/a"
fi

{
  echo "## NuGet stats ${DATE}"
  echo ""
  echo "Total downloads: **${TOTAL}** (change since ${PREV_DATE}: ${DELTA})"
  echo ""
  echo "| Package | Version | Downloads | Verified |"
  echo "|---|---|---|---|"
  jq -r '.packages[] | "| \(.id) | \(.version) | \(.totalDownloads) | \(.verified) |"' "stats/${DATE}.json"
} >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"
