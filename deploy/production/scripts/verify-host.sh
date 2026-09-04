#!/usr/bin/env bash
set -euo pipefail

domain="${1:-unrealstudiooj.top}"
logo_path="/opt/onlinejudge/current/frontend/brand/unrealstudio-logo.png"

docker compose --project-name onlinejudge --env-file /etc/onlinejudge/infrastructure.env \
  -f /opt/onlinejudge/current/deploy/production/compose.infrastructure.yml ps
systemctl --quiet is-active onlinejudge-api.service
systemctl --quiet is-active onlinejudge-worker.service
nginx -t

if [[ ! -s "$logo_path" ]]; then
  echo "Frontend brand asset is missing or empty: $logo_path" >&2
  exit 6
fi

curl --fail --silent --show-error --max-time 5 http://127.0.0.1:5101/api/site-settings/appearance >/dev/null
curl --fail --silent --show-error --max-time 10 "https://$domain/api/site-settings/appearance" >/dev/null

logo_content_type="$(curl --fail --silent --show-error --max-time 10 \
  --output /dev/null --write-out '%{content_type}' \
  "https://$domain/brand/unrealstudio-logo.png")"

if [[ "$logo_content_type" != "image/png" ]]; then
  echo "Unexpected logo content type: $logo_content_type" >&2
  exit 6
fi

if ss -ltnH | awk '{print $4}' | grep -Eq '(^|0\.0\.0\.0:|\[::\]:)(5432|6379)$'; then
  echo "PostgreSQL or Redis is listening on a public wildcard address" >&2
  exit 5
fi

printf 'Host verification passed for %s\n' "$domain"
