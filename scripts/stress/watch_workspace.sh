#!/usr/bin/env bash
set -euo pipefail

marker="${1:?marker path is required}"
output="${2:?output path is required}"
duration_seconds="${3:-120}"
[[ "$marker" == /opt/onlinejudge/stress/onlinejudge-stress-* ]] || { echo "unsafe marker path" >&2; exit 2; }
[[ "$output" == /opt/onlinejudge/stress/onlinejudge-stress-* ]] || { echo "unsafe output path" >&2; exit 2; }
[[ "$duration_seconds" =~ ^[0-9]+$ && "$duration_seconds" -le 300 ]] || { echo "invalid duration" >&2; exit 2; }

start=$(date +%s)
maximum=0
samples=0
while [[ ! -e "$marker" && $(( $(date +%s)-start )) -lt "$duration_seconds" ]]; do
  current=$(sudo -n du -sb /tmp/onlinejudge 2>/dev/null | awk '{print $1}' || echo 0)
  [[ "$current" =~ ^[0-9]+$ ]] || current=0
  (( current > maximum )) && maximum=$current
  samples=$((samples+1))
  sleep 0.2
done
printf 'workspace_peak_bytes=%s\nsamples=%s\n' "$maximum" "$samples" > "$output"
