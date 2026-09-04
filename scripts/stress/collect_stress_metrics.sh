#!/usr/bin/env bash
set -euo pipefail

duration_seconds=60
interval_seconds=5
output="-"
postgres_container="onlinejudge-postgres"
production_redis_container="onlinejudge-redis"
stress_redis_container=""
stress_database=""
production_health_url="http://127.0.0.1:5101/api/site-settings/appearance"
stress_api_unit=""
stress_worker_unit=""
min_disk_available_percent=20
min_memory_available_percent=15

while [[ $# -gt 0 ]]; do
  case "$1" in
    --duration-seconds) duration_seconds="$2"; shift 2 ;;
    --interval-seconds) interval_seconds="$2"; shift 2 ;;
    --output) output="$2"; shift 2 ;;
    --postgres-container) postgres_container="$2"; shift 2 ;;
    --production-redis-container) production_redis_container="$2"; shift 2 ;;
    --stress-redis-container) stress_redis_container="$2"; shift 2 ;;
    --stress-database) stress_database="$2"; shift 2 ;;
    --production-health-url) production_health_url="$2"; shift 2 ;;
    --stress-api-unit) stress_api_unit="$2"; shift 2 ;;
    --stress-worker-unit) stress_worker_unit="$2"; shift 2 ;;
    --min-disk-available-percent) min_disk_available_percent="$2"; shift 2 ;;
    --min-memory-available-percent) min_memory_available_percent="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

[[ "$duration_seconds" =~ ^[0-9]+$ && "$duration_seconds" -gt 0 ]] || { echo "invalid duration" >&2; exit 2; }
[[ "$interval_seconds" =~ ^[0-9]+$ && "$interval_seconds" -gt 0 ]] || { echo "invalid interval" >&2; exit 2; }
[[ "$min_disk_available_percent" =~ ^[0-9]+$ && "$min_disk_available_percent" -ge 20 ]] || { echo "disk threshold cannot be below 20%" >&2; exit 2; }
[[ "$min_memory_available_percent" =~ ^[0-9]+$ && "$min_memory_available_percent" -ge 15 ]] || { echo "memory threshold cannot be below 15%" >&2; exit 2; }
[[ -n "$stress_redis_container" ]] || { echo "--stress-redis-container is required" >&2; exit 2; }
[[ "$stress_database" =~ ^onlinejudge_stress_[a-z0-9_]+$ ]] || { echo "--stress-database must name an isolated stress database" >&2; exit 2; }
[[ -n "$stress_api_unit" ]] || { echo "--stress-api-unit is required" >&2; exit 2; }
[[ -n "$stress_worker_unit" ]] || { echo "--stress-worker-unit is required" >&2; exit 2; }

if [[ "$output" == "-" ]]; then
  exec 3>&1
else
  mkdir -p "$(dirname "$output")"
  exec 3>"$output"
fi

pg_user=$(sudo -n docker exec "$postgres_container" printenv POSTGRES_USER)
pg_db=$(sudo -n docker exec "$postgres_container" printenv POSTGRES_DB)
mem_total_kb=$(awk '/^MemTotal:/{print $2}' /proc/meminfo)
disk_total=$(df -B1 --output=size / | tail -1 | tr -d ' ')
initial_swap_used_kb=$(awk '/^SwapTotal:/{t=$2}/^SwapFree:/{f=$2}END{print t-f}' /proc/meminfo)

echo 'timestamp,cpu_pct,load1,load5,mem_available_bytes,swap_used_bytes,disk_used_bytes,disk_available_bytes,disk_write_sectors,net_rx_bytes,net_tx_bytes,production_api_rss_kb,production_worker_rss_kb,stress_api_rss_kb,stress_worker_rss_kb,postgres_rss_bytes,production_redis_rss_bytes,stress_redis_rss_bytes,postgres_connections,production_redis_used_memory_bytes,stress_redis_used_memory_bytes,stress_queue_depth,stress_oldest_pending_age_seconds,production_queue_depth,production_oldest_pending_age_seconds,docker_container_count,production_http' >&3

start_epoch=$(date +%s)
while (( $(date +%s) - start_epoch <= duration_seconds )); do
  sample_started=$(date +%s)
  read -r _ u1 n1 s1 id1 iw1 irq1 sirq1 st1 _ < /proc/stat
  total1=$((u1+n1+s1+id1+iw1+irq1+sirq1+st1)); idle1=$((id1+iw1))
  sleep 1
  read -r _ u2 n2 s2 id2 iw2 irq2 sirq2 st2 _ < /proc/stat
  total2=$((u2+n2+s2+id2+iw2+irq2+sirq2+st2)); idle2=$((id2+iw2))
  cpu=$(awk -v dt="$((total2-total1))" -v di="$((idle2-idle1))" 'BEGIN{if(dt>0)printf "%.2f",100*(dt-di)/dt;else print "0.00"}')
  read -r load1 load5 _ < /proc/loadavg
  mem_available_kb=$(awk '/^MemAvailable:/{print $2}' /proc/meminfo)
  swap_used_kb=$(awk '/^SwapTotal:/{t=$2}/^SwapFree:/{f=$2}END{print t-f}' /proc/meminfo)
  read -r disk_used disk_available < <(df -B1 --output=used,avail / | tail -1)
  disk_write_sectors=$(awk '$3 !~ /^(loop|ram|fd)/ {sum+=$10} END{print sum+0}' /proc/diskstats)
  read -r net_rx net_tx < <(awk -F'[: ]+' '$1!="lo" && $1!="Inter-" && NF>10 {rx+=$3;tx+=$11}END{print rx+0,tx+0}' /proc/net/dev)

  api_pid=$(systemctl show onlinejudge-api -p MainPID --value)
  worker_pid=$(systemctl show onlinejudge-worker -p MainPID --value)
  stress_api_pid=$(systemctl show "$stress_api_unit" -p MainPID --value)
  stress_worker_pid=$(systemctl show "$stress_worker_unit" -p MainPID --value)
  api_rss=$(awk '/^VmRSS:/{print $2}' "/proc/$api_pid/status" 2>/dev/null || echo 0)
  worker_rss=$(awk '/^VmRSS:/{print $2}' "/proc/$worker_pid/status" 2>/dev/null || echo 0)
  stress_api_rss=$(awk '/^VmRSS:/{print $2}' "/proc/$stress_api_pid/status" 2>/dev/null || echo 0)
  stress_worker_rss=$(awk '/^VmRSS:/{print $2}' "/proc/$stress_worker_pid/status" 2>/dev/null || echo 0)
  postgres_mem_text=$(sudo -n docker stats --no-stream --format '{{.Name}}|{{.MemUsage}}' | awk -F'|' -v name="$postgres_container" '$1==name{split($2,a," ");print a[1]}')
  production_redis_mem_text=$(sudo -n docker stats --no-stream --format '{{.Name}}|{{.MemUsage}}' | awk -F'|' -v name="$production_redis_container" '$1==name{split($2,a," ");print a[1]}')
  stress_redis_mem_text=$(sudo -n docker stats --no-stream --format '{{.Name}}|{{.MemUsage}}' | awk -F'|' -v name="$stress_redis_container" '$1==name{split($2,a," ");print a[1]}')
  to_bytes() {
    case "$1" in
      *KiB) awk -v value="${1%KiB}" 'BEGIN{printf "%.0f",value*1024}' ;;
      *MiB) awk -v value="${1%MiB}" 'BEGIN{printf "%.0f",value*1048576}' ;;
      *GiB) awk -v value="${1%GiB}" 'BEGIN{printf "%.0f",value*1073741824}' ;;
      *B) printf '%s' "${1%B}" ;;
      *) printf '0' ;;
    esac
  }
  postgres_rss=$(to_bytes "$postgres_mem_text")
  production_redis_rss=$(to_bytes "$production_redis_mem_text")
  stress_redis_rss=$(to_bytes "$stress_redis_mem_text")
  pg_connections=$(sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$pg_db" -Atc 'select count(*) from pg_stat_activity;' 2>/dev/null)
  production_redis_memory=$(sudo -n docker exec "$production_redis_container" redis-cli --raw INFO memory 2>/dev/null | awk -F: '/^used_memory:/{gsub(/\r/,"",$2);print $2}')
  stress_redis_memory=$(sudo -n docker exec "$stress_redis_container" redis-cli --raw INFO memory 2>/dev/null | awk -F: '/^used_memory:/{gsub(/\r/,"",$2);print $2}')
  read -r stress_depth stress_oldest_age < <(sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$stress_database" -AtF' ' -c 'SELECT COUNT(*) FILTER (WHERE "Status" = 1), COALESCE(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - MIN("CreatedAt") FILTER (WHERE "Status" = 1)))::bigint, 0) FROM "JudgeJobs";')
  read -r production_depth production_oldest_age < <(sudo -n docker exec "$postgres_container" psql -U "$pg_user" -d "$pg_db" -AtF' ' -c 'SELECT COUNT(*) FILTER (WHERE "Status" = 1), COALESCE(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - MIN("CreatedAt") FILTER (WHERE "Status" = 1)))::bigint, 0) FROM "JudgeJobs";')
  docker_count=$(sudo -n docker ps -q | wc -l)
  production_http=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 3 "$production_health_url" || echo 000)

  printf '%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%.0f,%.0f,%.0f,%s,%s,%s,%s,%s,%s,%s,%s,%s\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$cpu" "$load1" "$load5" "$((mem_available_kb*1024))" "$((swap_used_kb*1024))" \
    "$disk_used" "$disk_available" "$disk_write_sectors" "$net_rx" "$net_tx" "$api_rss" "$worker_rss" "$stress_api_rss" "$stress_worker_rss" \
    "$postgres_rss" "$production_redis_rss" "$stress_redis_rss" "$pg_connections" "$production_redis_memory" "$stress_redis_memory" \
    "$stress_depth" "$stress_oldest_age" "$production_depth" "$production_oldest_age" "$docker_count" "$production_http" >&3

  disk_available_percent=$((100*disk_available/disk_total))
  mem_available_percent=$((100*mem_available_kb/mem_total_kb))
  if (( disk_available_percent < min_disk_available_percent )); then echo "STOP_STRESS: disk available below ${min_disk_available_percent}%" >&2; exit 20; fi
  if (( mem_available_percent < min_memory_available_percent )); then echo "STOP_STRESS: memory available below ${min_memory_available_percent}%" >&2; exit 21; fi
  if (( swap_used_kb > initial_swap_used_kb + 131072 )); then echo "STOP_STRESS: swap increased by more than 128 MiB" >&2; exit 22; fi
  for service in onlinejudge-api onlinejudge-worker nginx; do
    if [[ "$(systemctl is-active "$service" 2>/dev/null || true)" != "active" ]]; then echo "STOP_STRESS: $service is not active" >&2; exit 23; fi
  done
  for service in "$stress_api_unit" "$stress_worker_unit"; do
    if [[ "$(systemctl is-active "$service" 2>/dev/null || true)" != "active" ]]; then echo "STOP_STRESS: $service is not active" >&2; exit 25; fi
  done
  if [[ "$production_http" != "200" ]]; then echo "STOP_STRESS: production health returned $production_http" >&2; exit 24; fi

  sample_elapsed=$(( $(date +%s) - sample_started ))
  sleep_for=$((interval_seconds-sample_elapsed))
  (( sleep_for > 0 )) && sleep "$sleep_for"
done
