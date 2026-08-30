#!/usr/bin/env bash
set -euo pipefail

run_id="${1:-}"
expected_commit="${2:-}"
stress_root="/opt/onlinejudge/stress"
postgres_container="onlinejudge-postgres"
production_redis_container="onlinejudge-redis"
api_port=15101
redis_port=16380

[[ "$run_id" =~ ^onlinejudge-stress-[a-zA-Z0-9_-]+$ ]] || { echo "invalid stress run id" >&2; exit 2; }
[[ "$expected_commit" =~ ^[0-9a-f]{40}$ ]] || { echo "invalid expected commit" >&2; exit 2; }
run_root="$stress_root/$run_id"
[[ "$run_root" == "$stress_root/onlinejudge-stress-"* ]] || { echo "unsafe stress root" >&2; exit 2; }
[[ -f "$run_root/onlinejudge-release.tar.gz" ]] || { echo "release archive not found" >&2; exit 3; }
sudo -n chown root:root "$run_root"
sudo -n chmod 0755 "$run_root"
sudo -n chmod 0640 "$run_root/onlinejudge-release.tar.gz"

slug=$(printf '%s' "${run_id#onlinejudge-stress-}" | tr '[:upper:]-' '[:lower:]_')
[[ "$slug" =~ ^[a-z0-9_]+$ ]] || { echo "invalid stress slug" >&2; exit 2; }
database="onlinejudge_stress_$slug"
database_role="$database"
redis_container="onlinejudge-stress-redis-$slug"
api_unit="onlinejudge-stress-api-$slug"
worker_unit="onlinejudge-stress-worker-$slug"
migrate_unit="onlinejudge-stress-migrate-$slug"
release_root="$run_root/release"
data_root="$run_root/data"
bin_root="$run_root/bin"
api_env="$run_root/stress-api.env"
worker_env="$run_root/stress-worker.env"
client_env="$run_root/stress-client.env"

if ss -ltnH | awk '{print $4}' | grep -Eq "(^|:)$api_port$|(^|:)$redis_port$"; then
  echo "stress port already in use" >&2
  exit 4
fi
if sudo -n docker inspect "$redis_container" >/dev/null 2>&1; then
  echo "stress Redis container already exists" >&2
  exit 4
fi
if sudo -n docker exec "$postgres_container" printenv POSTGRES_USER >/dev/null 2>&1; then :; else
  echo "production PostgreSQL container unavailable" >&2
  exit 4
fi

sudo -n install -d -m 0755 -o root -g root "$release_root" "$data_root" "$bin_root"
sudo -n tar -xzf "$run_root/onlinejudge-release.tar.gz" -C "$release_root"
manifest_commit=$(sudo -n awk -F= '$1=="Commit"{gsub(/\r/, "", $2); print $2}' "$release_root/release-manifest.txt")
[[ "$manifest_commit" == "$expected_commit" ]] || { echo "release manifest commit mismatch" >&2; exit 5; }
[[ -f "$release_root/api/OnlineJudge.Api" && -f "$release_root/worker/OnlineJudge.JudgeWorker" && -f "$release_root/efbundle" ]] || {
  echo "release executables are missing" >&2; exit 5;
}
[[ ! -e "$release_root/api/appsettings.Development.json" && ! -e "$release_root/worker/appsettings.Development.json" ]] || {
  echo "development configuration found in stress release" >&2; exit 5;
}

sudo -n install -m 0755 -o root -g root "$(dirname "$0")/docker-stress-wrapper.sh" "$bin_root/docker"
sudo -n chmod 0755 "$release_root/api/OnlineJudge.Api" "$release_root/worker/OnlineJudge.JudgeWorker" "$release_root/efbundle"
sudo -n install -d -m 0750 -o onlinejudge-api -g onlinejudge-api "$data_root/uploads" "$data_root/challenge-files" "$data_root/team-repositories" "$data_root/api-home"
sudo -n install -d -m 0750 -o onlinejudge-worker -g onlinejudge-worker "$data_root/worker-home"
sudo -n install -d -m 2750 -o onlinejudge-api -g onlinejudge-worker "$data_root/judge-assets"
sudo -n chown -R root:root "$release_root"
sudo -n chmod -R a+rX "$release_root"

postgres_user=$(sudo -n docker exec "$postgres_container" printenv POSTGRES_USER)
postgres_database=$(sudo -n docker exec "$postgres_container" printenv POSTGRES_DB)
database_password=$(openssl rand -hex 24)
jwt_secret=$(openssl rand -hex 48)
root_password=$(openssl rand -hex 24)
root_username="stress_root_$slug"
root_email="stress_root_$slug@example.invalid"

sudo -n docker exec -i "$postgres_container" psql -v ON_ERROR_STOP=1 -U "$postgres_user" -d "$postgres_database" \
  --set=role_password="$database_password" <<SQL
CREATE ROLE "$database_role" LOGIN PASSWORD :'role_password';
CREATE DATABASE "$database" OWNER "$database_role";
SQL

redis_image=$(sudo -n docker inspect -f '{{.Config.Image}}' "$production_redis_container")
sudo -n docker run -d \
  --name "$redis_container" \
  --label onlinejudge.stress=true \
  --label "onlinejudge.stress.run=$run_id" \
  --restart=no \
  -p "127.0.0.1:$redis_port:6379" \
  "$redis_image" >/dev/null

umask 077
api_tmp=$(mktemp)
worker_tmp=$(mktemp)
client_tmp=$(mktemp)
trap 'rm -f "$api_tmp" "$worker_tmp" "$client_tmp"' EXIT
cat >"$api_tmp" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:$api_port
ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5432;Database=$database;Username=$database_role;Password=$database_password
Redis__ConnectionString=127.0.0.1:$redis_port,abortConnect=false
Jwt__Secret=$jwt_secret
Jwt__Issuer=OnlineJudge.Stress
Jwt__Audience=OnlineJudge.Stress.Client
RootAccount__UserName=$root_username
RootAccount__Email=$root_email
RootAccount__Password=$root_password
Email__Provider=Dev
LeaderboardSeasonLifecycle__Enabled=false
Storage__UploadImagesRoot=$data_root/uploads
Storage__ChallengeFilesRoot=$data_root/challenge-files
TeamProjects__RepositoryStorageRoot=$data_root/team-repositories
JudgeAssets__StorageRoot=$data_root/judge-assets
HOME=$data_root/api-home
EOF
cat >"$worker_tmp" <<EOF
DOTNET_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5432;Database=$database;Username=$database_role;Password=$database_password
Redis__ConnectionString=127.0.0.1:$redis_port,abortConnect=false
JudgeAssets__StorageRoot=$data_root/judge-assets
HOME=$data_root/worker-home
PATH=$bin_root:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
ONLINEJUDGE_STRESS_DOCKER_PREFIX=$run_id
ONLINEJUDGE_STRESS_RUN_LABEL=$run_id
EOF
cat >"$client_tmp" <<EOF
STRESS_ROOT_ACCOUNT=$root_username
STRESS_ROOT_PASSWORD=$root_password
EOF
sudo -n install -m 0600 -o root -g root "$api_tmp" "$api_env"
sudo -n install -m 0600 -o root -g root "$worker_tmp" "$worker_env"
sudo -n install -m 0600 -o admin -g admin "$client_tmp" "$client_env"

sudo -n systemd-run --quiet --wait --collect \
  --unit="$migrate_unit" \
  --property=Type=oneshot \
  --property=User=onlinejudge-api \
  --property=Group=onlinejudge-api \
  --property="WorkingDirectory=$release_root" \
  --property="EnvironmentFile=$api_env" \
  "$release_root/efbundle"

sudo -n systemd-run --quiet \
  --unit="$api_unit" \
  --property=User=onlinejudge-api \
  --property=Group=onlinejudge-api \
  --property=Restart=no \
  --property="WorkingDirectory=$release_root/api" \
  --property="EnvironmentFile=$api_env" \
  "$release_root/api/OnlineJudge.Api"
sudo -n systemd-run --quiet \
  --unit="$worker_unit" \
  --property=User=onlinejudge-worker \
  --property=Group=onlinejudge-worker \
  --property=Restart=no \
  --property="WorkingDirectory=$release_root/worker" \
  --property="EnvironmentFile=$worker_env" \
  "$release_root/worker/OnlineJudge.JudgeWorker"

for _ in $(seq 1 30); do
  if curl -fsS --max-time 2 "http://127.0.0.1:$api_port/api/site-settings/appearance" >/dev/null; then break; fi
  sleep 1
done
curl -fsS --max-time 3 "http://127.0.0.1:$api_port/api/site-settings/appearance" >/dev/null
[[ "$(systemctl is-active "$api_unit")" == active ]] || { echo "stress API did not remain active" >&2; exit 6; }
[[ "$(systemctl is-active "$worker_unit")" == active ]] || { echo "stress Worker did not remain active" >&2; exit 6; }
[[ "$(sudo -n docker inspect -f '{{.State.Running}}' "$redis_container")" == true ]] || { echo "stress Redis is not running" >&2; exit 6; }

cat <<EOF
RUN_ID=$run_id
DATABASE=$database
REDIS_CONTAINER=$redis_container
API_UNIT=$api_unit
WORKER_UNIT=$worker_unit
API_URL=http://127.0.0.1:$api_port
MANIFEST_COMMIT=$manifest_commit
EOF
