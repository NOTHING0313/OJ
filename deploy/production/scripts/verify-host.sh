#!/usr/bin/env bash
set -euo pipefail

domain="${1:-unrealstudioonlinejudge.de5.net}"

docker compose --project-name onlinejudge --env-file /etc/onlinejudge/infrastructure.env \
  -f /opt/onlinejudge/current/deploy/production/compose.infrastructure.yml ps
systemctl --quiet is-active onlinejudge-api.service
systemctl --quiet is-active onlinejudge-worker.service
nginx -t
curl --fail --silent --show-error --max-time 5 http://127.0.0.1:5101/api/site-settings/appearance >/dev/null
curl --fail --silent --show-error --max-time 10 "https://$domain/api/site-settings/appearance" >/dev/null

if ss -ltnH | awk '{print $4}' | grep -Eq '(^|0\.0\.0\.0:|\[::\]:)(5432|6379)$'; then
  echo "PostgreSQL or Redis is listening on a public wildcard address" >&2
  exit 5
fi

printf 'Host verification passed for %s\n' "$domain"
