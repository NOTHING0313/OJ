#!/usr/bin/env bash
set -euo pipefail

domain="${1:-unrealstudiooj.top}"
alias_domain="www.$domain"
logo_path="/opt/onlinejudge/current/frontend/brand/unrealstudio-logo.png"
infrastructure_env="/etc/onlinejudge/infrastructure.env"
compose_file="/opt/onlinejudge/current/deploy/production/compose.infrastructure.yml"
compose=(docker compose --project-name onlinejudge --env-file "$infrastructure_env" -f "$compose_file")

read_volume_name() {
  local key="$1"
  local value
  value="$(awk -v key="$key" '
    index($0, key "=") == 1 {
      sub(/^[^=]*=/, "")
      value = $0
    }
    END { print value }
  ' "$infrastructure_env")"
  value="${value%$'\r'}"
  value="${value#\"}"
  value="${value%\"}"
  value="${value#\'}"
  value="${value%\'}"
  if [[ ! "$value" =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]*$ ]]; then
    echo "Missing or invalid $key in $infrastructure_env" >&2
    exit 9
  fi
  printf '%s\n' "$value"
}

verify_volume_mount() {
  local service="$1"
  local destination="$2"
  local expected_volume="$3"
  local container_id
  local actual_volume

  docker volume inspect "$expected_volume" >/dev/null
  container_id="$("${compose[@]}" ps -q "$service")"
  [[ -n "$container_id" ]] || { echo "No running container found for $service" >&2; exit 9; }
  actual_volume="$(docker inspect --format "{{range .Mounts}}{{if and (eq .Type \"volume\") (eq .Destination \"$destination\")}}{{.Name}}{{end}}{{end}}" "$container_id")"
  if [[ "$actual_volume" != "$expected_volume" ]]; then
    echo "Unexpected $service volume at $destination: expected=$expected_volume actual=${actual_volume:-none}" >&2
    exit 9
  fi
}

postgres_volume="$(read_volume_name POSTGRES_VOLUME_NAME)"
redis_volume="$(read_volume_name REDIS_VOLUME_NAME)"

"${compose[@]}" ps
verify_volume_mount postgres /var/lib/postgresql/data "$postgres_volume"
verify_volume_mount redis /data "$redis_volume"
systemctl --quiet is-active onlinejudge-api.service
systemctl --quiet is-active onlinejudge-worker.service
nginx -t

if [[ ! -s "$logo_path" ]]; then
  echo "Frontend brand asset is missing or empty: $logo_path" >&2
  exit 6
fi

curl --fail --silent --show-error --max-time 5 http://127.0.0.1:5101/api/site-settings/appearance >/dev/null
curl --fail --silent --show-error --max-time 10 "https://$domain/api/site-settings/appearance" >/dev/null

if ! alpn_protocol="$(openssl s_client -connect "$domain:443" -servername "$domain" -alpn h2 \
  </dev/null 2>/dev/null | sed -n 's/^ALPN protocol: //p')"; then
  echo "TLS handshake failed while verifying HTTP/2 for $domain" >&2
  exit 8
fi

if [[ "$alpn_protocol" != "h2" ]]; then
  echo "HTTP/2 was not negotiated for $domain: ALPN=${alpn_protocol:-none}" >&2
  exit 8
fi

logo_content_type="$(curl --fail --silent --show-error --max-time 10 \
  --output /dev/null --write-out '%{content_type}' \
  "https://$domain/brand/unrealstudio-logo.png")"

if [[ "$logo_content_type" != "image/png" ]]; then
  echo "Unexpected logo content type: $logo_content_type" >&2
  exit 6
fi

alias_path="/login?redirect=%2Fproblems"
expected_alias_redirect="https://$domain$alias_path"
alias_result="$(curl --silent --show-error --max-time 10 \
  --output /dev/null --write-out '%{http_code}|%{redirect_url}' \
  "https://$alias_domain$alias_path")"
alias_status="${alias_result%%|*}"
alias_redirect="${alias_result#*|}"

if [[ "$alias_status" != "301" || "$alias_redirect" != "$expected_alias_redirect" ]]; then
  echo "Unexpected canonical redirect from $alias_domain: status=$alias_status location=$alias_redirect" >&2
  exit 7
fi

if ss -ltnH | awk '{print $4}' | grep -Eq '(^|0\.0\.0\.0:|\[::\]:)(5432|6379)$'; then
  echo "PostgreSQL or Redis is listening on a public wildcard address" >&2
  exit 5
fi

printf 'Host verification passed for %s\n' "$domain"
