# OnlineJudge production deployment assets

These files implement the approved single-host topology for Ubuntu 24.04 on a 2 vCPU / 4 GB server at canonical origin `https://unrealstudiooj.top`; `www.unrealstudiooj.top` is a certificate-covered redirect alias. Nginx is the only application-facing process. The API listens on `127.0.0.1:5101`; PostgreSQL and Redis are published only on loopback; the Docker daemon is never exposed over TCP. The JudgeWorker remains a host systemd service so it can reuse the existing Docker CLI sandbox contract.

## Host paths and ownership

- Releases: `/opt/onlinejudge/releases/<release-id>` with `/opt/onlinejudge/current` as the active symlink.
- Secrets: `/etc/onlinejudge/{infrastructure,api,worker}.env`, root-owned mode `0600`.
- Data: `/var/lib/onlinejudge/{uploads,challenge-files,theme-assets,team-repositories,judge-assets}`.
- Service homes: `/var/lib/onlinejudge/{api-home,worker-home,worker-tmp}`.
- Backups: `/var/backups/onlinejudge`.

Create separate `onlinejudge-api` and `onlinejudge-worker` system users. Only the Worker joins the `docker` group. Docker-group membership is root-equivalent and is therefore confined to the Worker; the API must not receive it. `worker.env` sets `TMPDIR` to a host-visible path because sandbox containers cannot mount a systemd-private `/tmp`.

## First installation

1. Build `artifacts/onlinejudge-release.tar.gz` with `scripts/Publish-Production.ps1`, transfer the archive and its SHA-256 file, and verify the checksum on the server.
2. Extract the archive into a new `/opt/onlinejudge/releases/<release-id>` directory. Do not overwrite an existing release. Point `/opt/onlinejudge/current` to it only after the migration succeeds.
3. Copy each `.env.example` to `/etc/onlinejudge` without the `.example` suffix, replace every `CHANGE_ME` value, set owner `root:root`, and mode `0600`. Production registration and password recovery require working SMTP. No SMS setting is required; retained compatibility endpoints fail closed outside Development.
4. Create an `onlinejudge-assets` shared group and the data directories above. Give API ownership of uploads, challenge files, theme assets, team repositories and `api-home`; give Worker ownership of `worker-home` and `worker-tmp`; make `judge-assets` group-owned by `onlinejudge-assets` with the setgid bit so it remains writable by the API and readable by the Worker.
5. Start infrastructure using `docker compose --project-name onlinejudge --env-file /etc/onlinejudge/infrastructure.env -f /opt/onlinejudge/current/deploy/production/compose.infrastructure.yml up -d --wait`.
6. Run the release `efbundle` as `onlinejudge-api` with `/etc/onlinejudge/api.env`. Take a backup first for every later release.
7. Install the three unit files into `/etc/systemd/system`, reload systemd, then enable and start infrastructure, API and Worker in that order.
8. Install `onlinejudge-bootstrap.conf` while obtaining one certificate for both hostnames with `certbot certonly --webroot -w /var/www/letsencrypt --cert-name unrealstudiooj.top -d unrealstudiooj.top -d www.unrealstudiooj.top`. The explicit certificate name keeps the live certificate path aligned with `onlinejudge.conf`. The bootstrap exposes only the ACME challenge and returns 503 for the application. After certificate issuance, install `onlinejudge.conf`, run `nginx -t`, and reload Nginx. Configure renewal to reload Nginx, then require `certbot renew --dry-run` to pass.

## Release, rollback and recovery

Releases are immutable. Extract a new archive beside the old one, verify its manifest and executables, back up, apply its migration bundle, atomically repoint `current`, then restart API and Worker. Run `scripts/verify-host.sh` after restart; it also verifies that the production brand logo exists and is served as `image/png`, and that the HTTPS `www` alias preserves the request path and query while returning a single 301 to the canonical origin. Never automatically delete old releases.

Application rollback is allowed only when the previous binary is compatible with the already-applied database schema. Otherwise use a forward fix. Database restoration is a separate outage procedure: stop API and Worker, preserve the failed database and file roots, restore a matched `database.dump` and `files.tar.gz`, then start services and verify. Never restore only the database when file-backed records may refer to the matching file archive.

Run `scripts/backup.sh` from a root-controlled timer. It creates a PostgreSQL custom-format dump and a matched archive of all authoritative file roots. Redis is intentionally excluded: judge-job truth is in PostgreSQL and Redis contains transient queue wakeups, verification codes and counters. Validate every backup with `scripts/verify-backup.sh` on an isolated, unpublished PostgreSQL container. Keep at least seven daily backups and add an off-host encrypted copy before treating this as a production recovery system.

## Verification boundaries

Local validation can prove Compose rendering, loopback-only bindings, container health, script syntax and release packaging. The following remain target-host gates: real TLS issuance/renewal, systemd security/permissions, Nginx reload, SMTP delivery, backup restore with real data, persistent-file restart, C11/C++17/C# sandbox smoke, and bounded resource observation. Do not call deployment complete until those gates pass on the approved host.
