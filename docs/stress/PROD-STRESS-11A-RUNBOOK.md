# PROD-STRESS-11A Controlled Stress Runbook

## Status and authority

- Prepared from development commit `ec053bf321d45fca39abdaab8946090823e611f2`.
- Production remained on `/opt/onlinejudge/releases/20260829-b584ed5-rc3` during preflight.
- This runbook does not authorize deployment, migration, production data writes, or execution of the controlled load stages.
- Every stress resource must use the `onlinejudge-stress` prefix and Docker label `onlinejudge.stress=true`.

## Measured production baseline

Captured on 2026-08-30 without active load generation:

- Host: Ubuntu 24.04, kernel 6.8.0-63-generic, x86_64, 2 logical CPUs on Intel Xeon Platinum.
- RAM: 3,669,319,680 bytes total; approximately 2.70 GB available at preflight.
- Swap: 2,147,479,552 bytes total; approximately 1.4 MB used.
- Root filesystem: 52,448,063,488 bytes total, 15,952,846,848 bytes used, 34,129,993,728 bytes available.
- Docker 29.7.2: two running containers, PostgreSQL 16 and Redis 7, both healthy.
- API, Worker, and Nginx: active. API listens on `127.0.0.1:5101`.
- HTTP frontend, local API, and public API: 200. HTTPS 443 is not configured and is not changed by stress work.
- Production judge queue: zero throughout the idle sample.
- Full 60-second samples are retained as ignored runtime evidence at `artifacts/stress/<run-id>/idle-baseline.csv`; raw metrics are not committed.

Production size snapshot:

| Entity | Count |
| --- | ---: |
| Users | 6 |
| Problems | 7 |
| TestCases | 162 |
| Submissions | 38 |
| Teams | 1 |
| TeamMembers | 1 |
| Challenges | 1 |
| SecurityAuditLogs | Not present in the currently deployed RC3 schema |

Only counts were read. No row content or personally identifying information was exported.

## Isolation architecture

The controlled stage must provision an independent stack before any write scenario:

| Resource | Required isolated value |
| --- | --- |
| Release | `/opt/onlinejudge/stress/onlinejudge-stress-<commit-or-timestamp>` |
| API bind | `127.0.0.1:15101` |
| PostgreSQL database | `onlinejudge_stress_<timestamp>` |
| Redis | Dedicated `onlinejudge-stress-redis-<timestamp>` container, bound to localhost only if a host port is required |
| Judge queue | `judge:submissions:pending` inside the dedicated stress Redis container |
| Worker | Dedicated stress Worker configured only with the stress DB and stress Redis |
| Upload root | Below the stress release/data root, never production uploads |
| Challenge files | Below the stress release/data root, never production challenge files |
| Team repositories | Dedicated synthetic repository root |
| Docker resources | Prefix `onlinejudge-stress`; label `onlinejudge.stress=true` |

The queue key is fixed by the current application code, so isolation is provided by a dedicated Redis container, not a configurable key prefix. Do not reuse a production Redis instance or Redis DB index. Before starting the stress Worker, demonstrate that its Redis endpoint resolves to the dedicated stress container while production remains on its existing Redis container. Do not change `/opt/onlinejudge/current` or production systemd units.

## Synthetic dataset

Create the schema in the isolated database using the stress release's normal migration artifact. Seed only generated values:

| Entity | Initial synthetic target |
| --- | ---: |
| Users | 100 |
| Problems | 50 |
| TestCases | 500 |
| Historical submissions | 1,000 |
| Teams | 20 |
| Team members | 100 |
| Challenges | 10 |
| Leaderboard score rows | 1,000 |

Generate dedicated Answerer, ProblemSetter, and Root stress accounts. Credentials are supplied through ephemeral stress-only environment configuration and are never stored in this repository or metrics output. Do not copy production email addresses, phone numbers, password hashes, source code, team text, uploads, repositories, or audit records.

## Load generator

`scripts/stress/api_read.py` uses only Python's standard library. It requires an explicit target kind:

- `production-read-sanity`: GET-only, at most 2 VUs, at most 15 seconds, and at least 250 ms between requests per VU.
- `isolated-stress`: requires a local marker file whose exact content is `onlinejudge-stress`.

An optional bearer token is read from the environment variable named by `--token-env`; it is never printed. Example controlled-stage command:

```bash
printf '%s\n' onlinejudge-stress > /tmp/onlinejudge-stress.marker
python3 scripts/stress/api_read.py \
  --base-url http://127.0.0.1:15101 \
  --target-kind isolated-stress \
  --safety-marker-file /tmp/onlinejudge-stress.marker \
  --vus 5 \
  --duration-seconds 60
```

## Metrics

Run `scripts/stress/collect_stress_metrics.sh` on the production host in a separate terminal before starting load. It records host CPU/load, available memory, swap, disk and network counters, API/Worker RSS, PostgreSQL/Redis memory, PostgreSQL connections, production/stress queue depth, Docker container count, and production HTTP health.

For this 52 GB host, use a more conservative 25% disk threshold:

```bash
scripts/stress/collect_stress_metrics.sh \
  --duration-seconds 600 \
  --interval-seconds 5 \
  --min-disk-available-percent 25 \
  --output /opt/onlinejudge/stress/onlinejudge-stress-<run>/stress-metrics.csv
```

The load generator reports requests, success/failure, RPS, P50/P90/P95/P99/max latency, 429, total 4xx, total 5xx, and transport failures.

## Scenario matrix

| Scenario | Target | Controlled-stage behavior |
| --- | --- | --- |
| Read API | Isolated API; production only for bounded sanity | Problems, first problem detail, global/challenge leaderboard, appearance; authenticated help/profile when a stress token exists |
| Session authority | Isolated API | Authenticated GETs; correlate RPS and latency with PostgreSQL connections/query load |
| Login | Isolated API | Normal login, wrong password, many accounts, repeated same account; record 200/401/429/5xx |
| Submission API | Isolated API/DB/Redis | Submit synthetic source and verify only the stress queue changes |
| Full judge | Isolated API/Worker/Redis | C++17, C11, and C#; Accepted, Compile Error, Wrong Answer, TLE, and MLE |
| Team chat | Isolated data | Short valid messages and bounded bursts; record 200/429/P95 |
| Team Git | Dedicated public stress repository | Concurrency 1, 2, then 4 only; never use production Team repositories |
| Upload | Isolated upload root | Valid 100 KB, 1 MB, and 4 MB PNG/WebP fixtures; record validation, disk write, and rate limiting |
| File spam | Isolated Worker | Five runs maximum, five seconds each; record workspace disk before/peak/after cleanup |

Do not use sandbox escape payloads; SECURITY-10D already covers isolation. Capacity tests use benign resource-bound programs.

## Concurrency ladder

Read/session stages: 1, 5, 10, 20, 40, 80 VUs. Each stage lasts 60 seconds with a 30-second zero-load cooldown. Do not advance automatically. The operator reviews the collector and production health after every stage. Lower the maximum if earlier stages approach a stop threshold.

Team Git uses only 1, 2, and 4 concurrent operations. Judge capacity is measured separately by controlled arrival rate, queue depth, throughput, average/P95 judge duration, and queue drain time. One Worker remains the initial concurrency baseline.

## Stop conditions

Stop load immediately and do not advance when any of these occurs:

- Root filesystem available space falls below 25% for this host (absolute minimum is never below 20%).
- Available RAM falls below 15%, swap increases persistently, or swap use grows by more than 128 MiB during a run.
- API, Worker, PostgreSQL, Redis, or Nginx is OOM-killed, restarts, or becomes unhealthy.
- CPU remains saturated for at least 60 seconds together with serious P99 degradation, 5xx, or watchdog failure.
- Unexpected 5xx exceeds 1% for two sampling periods. Expected 429 responses are recorded separately.
- Read API P95 exceeds 1,000 ms or P99 exceeds 3,000 ms: stop raising concurrency and record the capacity knee.
- Production frontend/API health fails, a production service restarts, or the production judge queue behaves abnormally.
- Any write appears in production data, production uploads, production repositories, or the production judge queue.

The stop operator terminates the load generator first, then the stress Worker. Production services are never stopped as part of stress control.

## Capacity and recovery

`SUSTAINED_CAPACITY` is the highest completed 60-second stage satisfying all of:

- 5xx below 1%.
- Read API P95 at or below 1,000 ms.
- No OOM or service restart.
- Production health remains normal.
- Stress judge queue drains within the agreed recovery window.

Record burst capacity separately. After each high stage, stop arrivals and record `RECOVERY_TIME_SECONDS` until CPU/RAM and PostgreSQL connections return near idle and the stress judge queue drains to zero.

## 2026-08-30 measured capacity baseline

The controlled run on the server described above produced this hardware- and release-specific baseline:

- Read API stable stage: 40 VUs.
- Read API peak observed throughput: 122.88 requests/second.
- Read API degradation point: 80 VUs with P95 latency 1,311.91 ms.
- Authenticated reads: 40 VUs at 216.71 requests/second.
- Judge safe arrival rate: 0.1 submission/second.
- Single-Worker judge throughput: approximately 8.93 jobs/minute.
- Worst measured judge queue recovery: 66 seconds.
- Primary bottlenecks: two-vCPU saturation and single-Worker compile/container startup.
- Server recommendation for the measured small production dataset and traffic: `KEEP`.

These values are a point-in-time baseline, not a permanent performance guarantee. Repeat the controlled test after material changes to hardware, application code, production data volume, rate limits, or Worker count.

`combined_load.py` requires the active stress metrics unit through `--metrics-unit`. The Team Git scenario requires an explicitly selected public stress repository through `--git-repository-url`; do not use a production Team repository.

## Cleanup and rollback

Always dry-run the exact selector first:

```bash
scripts/stress/cleanup_stress.sh
```

The script can select only Docker resources labeled `onlinejudge.stress=true`, transient units and directories beginning `onlinejudge-stress`, and databases/roles matching `onlinejudge_stress_<safe-name>`. The dedicated Redis container is removed as a labeled resource; no production Redis keys are selected. It never uses `docker system prune` or broad temporary-directory deletion.

Cleanup execution is intentionally double-gated and belongs to the controlled-stage closeout, not preflight:

```bash
OJ_STRESS_CLEANUP_CONFIRM=DELETE_ONLINEJUDGE_STRESS_ONLY \
  scripts/stress/cleanup_stress.sh --execute
```

Rollback means stop and remove only labeled stress services/containers, remove only the isolated database/Redis namespace and stress paths, and leave `/opt/onlinejudge/current`, production services, production DB, uploads, repositories, and release directories unchanged. If isolation cannot be proven, do not start the test.
