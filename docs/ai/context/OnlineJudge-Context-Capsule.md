# OnlineJudge Context Capsule

## 1. Scope

- Project: OnlineJudge
- Module / Stage: Platform improvement Stage 4C-R through 4E (local implementation complete)
- Updated: 2026-09-03
- Current task: target-host production verification after `RECRUITMENT-PRODUCTION-FRONTEND-04C-04E`

## 2. Current Goal

Preserve the verified application and judge baseline while carrying the versioned 4D deployment assets through the still-pending target-host TLS, service, restore, SMTP, sandbox and resource gates.

## 3. Current Git Baseline

- Root: `H:/GitHub/OJ`
- Branch: `main`
- Stage 0-3 baseline commit: `35576d2` (`feat: harden judge revisions and durable job processing`)
- Stage 4A implementation commit: `6511486` (`build: establish net10 and continuous verification baseline`)
- Stage 4A governance commit: `031ab81` (`docs: record platform baseline stage results`)
- Stage 4B implementation/governance baseline: `eede043` (`feat: harden judge sandbox data plane`).
- Stage 4C implementation commit: `3d5c56f` (`feat: harden identity sessions and exports`).
- Stage 4C governance record: `docs/ai/tasks/IDENTITY-SESSION-EXPORT-04C.md` and the context ledgers.
- Stage 4C recruitment-policy correction: `f16bb96` (`fix: align recruitment account safeguards`).
- Stage 4D local deployment assets: `64dfe9a` (`feat: add single-host production deployment assets`).
- Stage 4E frontend route/budget work: `52731b7` (`perf: split frontend routes and enforce bundle budget`).
- Continuous package review: `docs/ai/tasks/RECRUITMENT-PRODUCTION-FRONTEND-04C-04E.md`.
- Remote writes: none; no push was performed.
- `output/` contains a local PDF artifact and remains untracked and untouched.

## 4. Architecture Invariants

- Domain models stay in `OnlineJudge.Domain`; application contracts and DTOs stay in `OnlineJudge.Application`.
- EF Core, PostgreSQL, Redis, Docker, and external integrations stay in `OnlineJudge.Infrastructure`.
- HTTP controllers remain thin and never execute submitted code.
- `OnlineJudge.JudgeWorker` is the only submitted-code execution process.
- PostgreSQL `JudgeJob` rows are durable work authority; Redis is a best-effort wake-up hint.
- Every new submission binds to an immutable `ProblemJudgeRevision`; Workers must never reconstruct judge input from mutable authoring rows.
- Judge languages remain C11, C++17, and C#.

## 5. Current Architecture Snapshot

- Backend runtime: .NET 10 (`net10.0`) with SDK `10.0.400` pinned by `global.json`.
- Data access: EF Core `10.0.11`, Npgsql EF provider `10.0.3`, PostgreSQL.
- Queue/recovery: PostgreSQL lease/heartbeat/retry/dead-letter state machine with Redis wake hints and database polling fallback.
- Frontend: React 19, TypeScript 5.7, Vite 6, Monaco Editor; ESLint and Vitest fast gates are available.
- Sandbox: host Docker execution with no network/IPC, fixed non-root user, bounded memory/CPU/PIDs/files/output, read-only container root, read-only runtime workspace, size-bounded `/tmp`, submission-scoped labels and cleanup.
- C# submissions compile against the .NET 10 SDK sandbox image and generated `net10.0` project.
- Email/SMS verification transitions are atomic in Redis and covered by real-Redis CI integration tests.
- Password creation uses the shared 8-128 Unicode policy; new hashes use versioned PBKDF2-SHA256 `v2` at 600,000 iterations and legacy `v1` hashes upgrade on successful login.
- Registration and registration-email-code endpoints intentionally have no per-IP rate limiter; other risk-based limiters remain.
- Phone verification is absent from the first-party UI; backend routes and persisted fields remain as a compatibility surface and fail closed without a Production sender.
- First-party browsers use a Secure HttpOnly JWT cookie plus antiforgery tokens; legacy Bearer login remains available.
- Challenge CSV exports distinguish trusted cells from untrusted text and neutralize spreadsheet formula prefixes.
- Production assets define Nginx TLS, loopback API/PostgreSQL/Redis, host systemd API/Worker, external secrets, persistent roots and backup/restore checks under `deploy/production`.
- All page routes are lazy. The production build enforces the initial-JavaScript budget and prevents Monaco from entering the initial graph.

## 6. Verification Baseline

- Release solution build: passed with 0 warnings and 0 errors.
- Backend tests: latest package publisher passed 1003 tests with 8 Redis integration tests discover-time skipped; those 8 / 8 then passed separately against real local Redis.
- EF model drift: no pending model changes.
- Frontend lint: passed with Hooks exhaustive-dependency and Fast Refresh rules enabled.
- Frontend tests: 7 / 7 Vitest tests passed; typecheck and lint passed.
- Frontend production build: passed; initial static JavaScript is 281.5 KiB raw / 89.4 KiB gzip and excludes Monaco.
- Production publisher: passed for commit `52731b7`; archive includes API, Worker, frontend, EF bundle, sandbox and `deploy/production` assets; SHA-256 `5f8aaaf398f29d6c9a0c71f7c9275298bca4c186198a80c0a874220d91f76424`.
- Local 4D runtime: isolated PostgreSQL/Redis health and loopback bindings passed; Bash and Nginx syntax passed.
- Judge Docker smoke: 7 / 7 passed, including C11/C++17/C# Accepted, C++17 Wrong Answer/Compile Error, combined-output termination, and read-only runtime workspace.
- Function-mode Docker regression: 10 / 10 main E2E scenarios and 3 / 3 custom-struct language scenarios passed.
- Sandbox security smoke: passed with 50 leak-cleanup runs; runtime host writes and oversized `/tmp` file writes are blocking assertions.
- GitHub Actions workflow exists for push/pull-request backend and frontend fast gates; hosted execution is not locally verifiable.
- Live local session verification passed for Secure/HttpOnly/SameSite cookie flags, cookie `/me`, CSRF rejection/acceptance, logout, and unchanged Bearer flows.

## 7. Known Technical Debt And Risks

- Runtime workspace, temporary disk, individual file size, and combined stdout/stderr are bounded. A portable aggregate quota for the writable compiler bind mount remains tracked as `SANDBOX-04B-D002`.
- Safe garbage collection for soft-deleted judge assets is still open as `JUDGE-REV-FND-D001`.
- React dependency and Fast Refresh lint debt `PLATFORM-04A-D001` is resolved.
- Hosted Docker judge CI needs a confirmed Docker-capable runner; tracked as `PLATFORM-04A-D002`.
- The optional problem-detail Monaco chunk remains approximately 2.69 MiB and is tracked as `FRONTEND-04E-D001`.
- Production target-host evidence remains open as `PROD-04D-D001`; local assets are not proof of public readiness.
- Production browser sessions still require real HTTPS/forwarded-scheme verification (`AUTH-04C-D001`).
- Dormant phone compatibility is tracked as `AUTH-04C-D002`.
- Real Excel/LibreOffice import verification is blocked by the current host and tracked as `EXPORT-04C-D001`.

## 8. Current Immediate Task

Local 4C-R/4D/4E implementation is complete. The next safe task is a separately authorized target-host deployment rehearsal using `deploy/production/README.md`. Do not claim production readiness until every target-host gate passes.

Read first:

1. `AGENTS.md`
2. `.agents/skills/onlinejudge-project-context/SKILL.md`
3. `docs/ai/tasks/RECRUITMENT-PRODUCTION-FRONTEND-04C-04E.md`
4. `deploy/production/README.md`
5. Current source/configuration files explicitly named by the next task card

## 9. Next Approved Stage Boundary

Local Stage 4D assets are complete. Remote deployment, DNS, certificate issuance, production migration/data writes, SMTP delivery and public release require explicit authorization and target-host evidence.

## 10. Stop Rules

- Do not push, publish externally, merge, or expose services without explicit authorization.
- Do not change public HTTP DTOs, persisted formats, or core judge authority boundaries outside an approved stage contract.
- Do not claim hosted CI passed until GitHub Actions actually executes.
- Mandatory gate failures block stage completion; skipped checks must be reported as NotRun.
