#!/usr/bin/env bash
set -euo pipefail

backup_root="${1:-/var/backups/onlinejudge}"
postgres_container="${POSTGRES_CONTAINER_NAME:-onlinejudge-postgres}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
pending_dir="$backup_root/.pending-$timestamp-$$"
final_dir="$backup_root/$timestamp"

umask 077
install -d -m 0700 "$backup_root"
[[ ! -e "$pending_dir" && ! -e "$final_dir" ]] || { echo "backup target already exists" >&2; exit 2; }
install -d -m 0700 "$pending_dir"

docker exec "$postgres_container" sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner --no-privileges' > "$pending_dir/database.dump"
tar -C /var/lib/onlinejudge -czf "$pending_dir/files.tar.gz" uploads challenge-files theme-assets team-repositories judge-assets
(
  cd "$pending_dir"
  sha256sum database.dump files.tar.gz > SHA256SUMS
)

mv "$pending_dir" "$final_dir"
printf 'Backup completed: %s\n' "$final_dir"
