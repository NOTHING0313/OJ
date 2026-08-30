#!/usr/bin/env bash
set -euo pipefail

real_docker="/usr/bin/docker"
prefix="${ONLINEJUDGE_STRESS_DOCKER_PREFIX:?ONLINEJUDGE_STRESS_DOCKER_PREFIX is required}"
run_label="${ONLINEJUDGE_STRESS_RUN_LABEL:?ONLINEJUDGE_STRESS_RUN_LABEL is required}"
[[ "$prefix" =~ ^onlinejudge-stress-[a-zA-Z0-9_-]+$ ]] || { echo "invalid stress Docker prefix" >&2; exit 64; }
[[ "$run_label" =~ ^onlinejudge-stress-[a-zA-Z0-9_-]+$ ]] || { echo "invalid stress run label" >&2; exit 64; }

command_name="${1:-}"
shift || true
args=("$@")
for index in "${!args[@]}"; do
  if [[ "${args[$index]}" == oj-judge-* ]]; then
    args[$index]="$prefix-${args[$index]}"
  fi
done

case "$command_name" in
  create)
    exec "$real_docker" create \
      --label onlinejudge.stress=true \
      --label "onlinejudge.stress.run=$run_label" \
      "${args[@]}"
    ;;
  ps)
    exec "$real_docker" ps \
      --filter "label=onlinejudge.stress.run=$run_label" \
      "${args[@]}"
    ;;
  start|inspect|kill|rm)
    exec "$real_docker" "$command_name" "${args[@]}"
    ;;
  *)
    echo "stress Docker wrapper rejected command: $command_name" >&2
    exit 65
    ;;
esac
