#!/usr/bin/env bash
set -euo pipefail

backup_dir="${1:-}"
[[ -n "$backup_dir" ]] || { echo "usage: verify-backup.sh <backup-directory>" >&2; exit 2; }
backup_dir="$(realpath "$backup_dir")"
[[ -f "$backup_dir/database.dump" && -f "$backup_dir/files.tar.gz" && -f "$backup_dir/SHA256SUMS" ]] || {
  echo "backup set is incomplete" >&2; exit 3;
}

(
  cd "$backup_dir"
  sha256sum --check SHA256SUMS
  tar -tzf files.tar.gz >/dev/null
)

container="onlinejudge-restore-check-$(date -u +%Y%m%d%H%M%S)-$$"
cleanup() {
  docker rm -f "$container" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run -d --name "$container" --network none \
  -e POSTGRES_DB=restore_check \
  -e POSTGRES_USER=restore_check \
  -e POSTGRES_PASSWORD=restore_check_only \
  postgres:16 >/dev/null

for _ in $(seq 1 60); do
  if docker exec "$container" pg_isready -U restore_check -d restore_check >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$container" pg_isready -U restore_check -d restore_check >/dev/null
docker cp "$backup_dir/database.dump" "$container:/tmp/database.dump"
docker exec "$container" pg_restore --exit-on-error --no-owner --no-privileges -U restore_check -d restore_check /tmp/database.dump

migration_count="$(docker exec "$container" psql -X -qAt -U restore_check -d restore_check -c 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')"
[[ "$migration_count" =~ ^[1-9][0-9]*$ ]] || { echo "restored database has no EF migration history" >&2; exit 4; }
printf 'Backup restore verification passed; migrations=%s\n' "$migration_count"
