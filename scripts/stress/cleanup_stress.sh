#!/usr/bin/env bash
set -euo pipefail

execute=false
if [[ "${1:-}" == "--execute" ]]; then execute=true; shift; fi
[[ $# -eq 0 ]] || { echo "Usage: $0 [--execute]" >&2; exit 2; }

prefix="onlinejudge-stress"
label="onlinejudge.stress=true"
stress_root="/opt/onlinejudge/stress"
postgres_container="onlinejudge-postgres"

containers=$(sudo -n docker ps -aq --filter "label=$label")
directories=$(sudo -n find "$stress_root" -mindepth 1 -maxdepth 1 -type d -name "$prefix*" -print 2>/dev/null || true)
units=$(systemctl list-units --all --plain --no-legend 'onlinejudge-stress-*' 2>/dev/null | awk '{print $1}' | grep -E '^onlinejudge-stress-[a-zA-Z0-9_.@-]+\.service$' || true)
pg_user=$(sudo -n docker exec "$postgres_container" printenv POSTGRES_USER)
pg_db=$(sudo -n docker exec "$postgres_container" printenv POSTGRES_DB)
databases=$(sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$pg_db" -Atc "select datname from pg_database where datname like 'onlinejudge_stress_%' and datname !~ '[^a-zA-Z0-9_]';" 2>/dev/null || true)
roles=$(sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$pg_db" -Atc "select rolname from pg_roles where rolname like 'onlinejudge_stress_%' and rolname !~ '[^a-zA-Z0-9_]';" 2>/dev/null || true)

echo "MODE=$([[ "$execute" == true ]] && echo EXECUTE || echo DRY_RUN)"
echo "LABEL_SELECTOR=$label"
printf '%s\n' 'CONTAINERS:' "${containers:-NONE}"
printf '%s\n' 'UNITS:' "${units:-NONE}"
printf '%s\n' 'DIRECTORIES:' "${directories:-NONE}"
printf '%s\n' 'DATABASES:' "${databases:-NONE}"
printf '%s\n' 'ROLES:' "${roles:-NONE}"

if [[ "$execute" != true ]]; then exit 0; fi
if [[ "${OJ_STRESS_CLEANUP_CONFIRM:-}" != "DELETE_ONLINEJUDGE_STRESS_ONLY" ]]; then
  echo "Execution requires OJ_STRESS_CLEANUP_CONFIRM=DELETE_ONLINEJUDGE_STRESS_ONLY" >&2
  exit 3
fi

while IFS= read -r unit; do
  [[ -z "$unit" ]] && continue
  [[ "$unit" =~ ^onlinejudge-stress-[a-zA-Z0-9_.@-]+\.service$ ]] || { echo "unsafe unit: $unit" >&2; exit 4; }
  sudo -n systemctl stop "$unit"
  sudo -n systemctl reset-failed "$unit" 2>/dev/null || true
done <<< "$units"
if [[ -n "$containers" ]]; then sudo -n docker rm -f $containers; fi
while IFS= read -r database; do
  [[ -z "$database" ]] && continue
  [[ "$database" =~ ^onlinejudge_stress_[a-zA-Z0-9_]+$ ]] || { echo "unsafe database: $database" >&2; exit 5; }
  sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$pg_db" -c "drop database \"$database\" with (force);"
done <<< "$databases"
while IFS= read -r role; do
  [[ -z "$role" ]] && continue
  [[ "$role" =~ ^onlinejudge_stress_[a-zA-Z0-9_]+$ ]] || { echo "unsafe role: $role" >&2; exit 6; }
  sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$pg_db" -c "drop role \"$role\";"
done <<< "$roles"
while IFS= read -r directory; do
  [[ -z "$directory" ]] && continue
  [[ "$directory" == "$stress_root/$prefix"* ]] || { echo "unsafe directory: $directory" >&2; exit 4; }
  sudo -n rm -rf -- "$directory"
done <<< "$directories"
