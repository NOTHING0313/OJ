# Task Card: OJ-PRODUCTION-DEPLOY-01

## Status

`TARGET_HOST_PARTIALLY_VERIFIED_REMAINING_GATES_OPEN`

Local implementation evidence is recorded in `RECRUITMENT-PRODUCTION-FRONTEND-04C-04E.md`. An operator report dated 2026-09-04 records successful source publication, database migration with preserved business counts, isolated backup restoration, public TLS/HTTP2 and canonical redirect checks, and healthy Nginx/systemd/Docker services. The report is currently a local untracked artifact at `output/pdf/OnlineJudge-Production-Upgrade-and-TLS-Report-20260904.pdf`; it does not replace the acceptance matrix below.

The available report does not establish SMTP delivery, a controlled persistent-file restart/redeploy, target-host C11/C++17/C# sandbox smoke, bounded 2C4G resource observation, or certificate-renewal execution. Those gates remain open, so this card does not claim complete production readiness.

This card remains the acceptance authority for the partially completed production deployment. Its open gates require separately authorized target-host execution and retained evidence.

## Objective

Prepare the current OnlineJudge for a production-ready deployment on a small public Linux server while preserving existing application, judge, data, and UI behavior.

## Target Environment

- Ubuntu Server 24.04 LTS, x86-64
- Alibaba Cloud
- 2 vCPU
- Approximately 4 GB RAM
- 2 GB swap
- Approximately 50 GB SSD
- Docker Engine and Docker Compose Plugin installed
- UFW enabled
- Canonical domain: `unrealstudiooj.top`
- Certificate-covered redirect alias: `www.unrealstudiooj.top`

User-attested infrastructure state:

- SSH key login: PASS
- UFW: PASS
- Docker Engine: PASS
- Docker Compose: PASS
- Docker image pull: PASS
- Alibaba ACR mirror: configured

## Network Contract

Publicly allowed ports:

- 22/tcp
- 80/tcp
- 443/tcp

Must not be publicly exposed:

- PostgreSQL
- Redis
- Docker daemon

## Required Discovery Before Changes

1. Reconfirm Git baseline, dirty-worktree ownership, effective Agents, and task scope.
2. Inventory Compose services, port publishing, volumes, networks, health checks, and restart behavior.
3. Inventory API, Worker, frontend, PostgreSQL, Redis, uploads, challenge files, Docker socket/CLI, and sandbox images.
4. Identify authoritative production configuration inputs without copying secret values.
5. Measure or conservatively bound memory, CPU, disk, container, and worker requirements for 2C4G.
6. Select the smallest credible build, test, container, and deployment verification plan before implementation.

## Required Workstreams

### 1. Network And Service Topology

- Remove public PostgreSQL and Redis exposure.
- Define internal service networking and production API reachability.
- Determine API production binding and container/service ownership.
- Preserve no-public-Docker-daemon boundary.

### 2. Secrets And Configuration

- Externalize development database credentials.
- Externalize the development JWT secret.
- Define production SMTP or explicitly retain a non-production provider with documented behavior.
- Ensure governance, logs, Compose output, and source control never contain actual production values.

### 3. Frontend And HTTP Boundary

- Replace the fixed localhost API base URL with a production-safe configuration strategy.
- Replace development-only CORS with the approved production origin policy.
- Define Nginx reverse proxy routing for frontend, API, static uploads, and HTTPS.

### 4. Persistence

- Define PostgreSQL volume ownership, backup, restore, and rollback.
- Persist API upload data.
- Persist challenge file submissions and keep database file paths valid across restart/redeploy.
- Set file permissions, retention, capacity limits, and backup scope.

### 5. JudgeWorker And Sandbox Security

- Verify how JudgeWorker reaches the host Docker daemon.
- Review Docker socket/CLI privileges, bind mounts, image trust, network isolation, PID/CPU/memory limits, process cleanup, and workspace cleanup.
- Verify C11, C++17, and C# image availability and architecture compatibility.
- Preserve current sequential worker behavior unless a separate task authorizes concurrency changes.

### 6. Resource Budget

- Fit API, Worker, frontend serving, PostgreSQL, Redis, Docker daemon, and judge containers within 2 vCPU and approximately 4 GB RAM.
- Define conservative container and service limits, swap expectations, disk growth, and low-resource failure behavior.
- Avoid assumptions that require multi-worker or per-test-case lifecycle redesign.

### 7. Operations

- Define Nginx configuration and reload validation.
- Define systemd or an explicitly chosen equivalent service lifecycle.
- Configure HTTPS issuance and renewal for both hostnames, with `www.unrealstudiooj.top` redirecting to the canonical origin.
- Define structured logging, rotation, health checks, startup order, and restart policy.
- Define database/file backup, restore test, release rollback, and forward-fix procedures.

## Current Issues To Verify

1. PostgreSQL Compose network exposure
2. Redis Compose network exposure
3. Development database credentials
4. Development JWT secret
5. Frontend localhost API base URL
6. Development CORS
7. API production binding
8. Persistent upload data
9. Challenge-file persistence
10. JudgeWorker to host Docker access
11. Docker Sandbox permissions
12. 2C4G resource budget
13. Nginx
14. systemd
15. HTTPS
16. Logging
17. Backup and rollback

## Explicit Out Of Scope

- Judge core algorithm refactoring
- Per-test-case container lifecycle optimization
- Compile/runtime memory-limit separation
- Multiple concurrent submissions in one worker
- Multiple workers
- Further .NET runtime/toolchain upgrades
- Database schema redesign
- UI feature changes

## Stop Conditions

Stop for confirmation before:

- exposing a new public port or Docker daemon endpoint;
- writing production credentials or private keys;
- installing a new production dependency;
- changing public API, persisted schema, queue semantics, worker concurrency, or sandbox trust boundary;
- deleting, moving, or migrating existing database/upload/challenge data;
- performing a remote write, deployment, DNS change, certificate issuance, push, or public release not explicitly authorized.

## Acceptance Evidence

- Reviewed Task Delta limited to the approved deployment scope.
- Production secrets remain external and redacted from evidence.
- PostgreSQL, Redis, and Docker daemon are not publicly reachable.
- API/frontend/domain routing, HTTPS, HTTP/2, and the canonical `www` redirect are verified on the target host.
- Persistent database, uploads, and challenge files survive a controlled restart/redeploy.
- Targeted application tests and frontend build pass.
- Sandbox smoke tests cover C11, C++17, and C# on the target architecture.
- Resource use is observed under a bounded workload suitable for 2C4G.
- Logging, backup/restore, and rollback procedures have executable evidence.

## Remaining Execution Instruction

Resume from the open acceptance gates only. First inventory the evidence already retained, then execute the smallest missing target-host checks. Request confirmation before any security-boundary, persistent-data, dependency, DNS/certificate or other external-system write.
