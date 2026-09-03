# OnlineJudge Context Capsule

## 1. Scope

- Project: OnlineJudge
- Module / Stage: Platform improvement Stage 4C
- Updated: 2026-09-03
- Current task: `IDENTITY-SESSION-EXPORT-04C`

## 2. Current Goal

Preserve the verified judge/sandbox baseline while hardening verification codes, passwords, browser sessions, and spreadsheet exports without changing judge authority or database entities.

## 3. Current Git Baseline

- Root: `H:/GitHub/OJ`
- Branch: `main`
- Stage 0-3 baseline commit: `35576d2` (`feat: harden judge revisions and durable job processing`)
- Stage 4A implementation commit: `6511486` (`build: establish net10 and continuous verification baseline`)
- Stage 4A governance commit: `031ab81` (`docs: record platform baseline stage results`)
- Stage 4B implementation/governance baseline: `eede043` (`feat: harden judge sandbox data plane`).
- Stage 4C implementation commit: `3d5c56f` (`feat: harden identity sessions and exports`).
- Stage 4C governance record: `docs/ai/tasks/IDENTITY-SESSION-EXPORT-04C.md` and the context ledgers.
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
- New password hashes use versioned PBKDF2-SHA256 `v2` at 600,000 iterations; legacy `v1` hashes verify and upgrade on successful login.
- First-party browsers use a Secure HttpOnly JWT cookie plus antiforgery tokens; legacy Bearer login remains available.
- Challenge CSV exports distinguish trusted cells from untrusted text and neutralize spreadsheet formula prefixes.

## 6. Verification Baseline

- Release solution build: passed with 0 warnings and 0 errors.
- Backend tests: Stage 4C full suite passed 1002 / 1002 before final review fixes; post-review focused auth/account/session/real-Redis suite passed 64 / 64; CSV security suite passed 8 / 8.
- EF model drift: no pending model changes.
- Frontend lint: passed under the 4A stable baseline rule set.
- Frontend tests: 5 / 5 Vitest tests passed; typecheck and lint passed.
- Frontend production build: passed; the existing 3.45 MB main-chunk advisory remains.
- Judge Docker smoke: 7 / 7 passed, including C11/C++17/C# Accepted, C++17 Wrong Answer/Compile Error, combined-output termination, and read-only runtime workspace.
- Function-mode Docker regression: 10 / 10 main E2E scenarios and 3 / 3 custom-struct language scenarios passed.
- Sandbox security smoke: passed with 50 leak-cleanup runs; runtime host writes and oversized `/tmp` file writes are blocking assertions.
- GitHub Actions workflow exists for push/pull-request backend and frontend fast gates; hosted execution is not locally verifiable.
- Live local session verification passed for Secure/HttpOnly/SameSite cookie flags, cookie `/me`, CSRF rejection/acceptance, logout, and unchanged Bearer flows.

## 7. Known Technical Debt And Risks

- Runtime workspace, temporary disk, individual file size, and combined stdout/stderr are bounded. A portable aggregate quota for the writable compiler bind mount remains tracked as `SANDBOX-04B-D002`.
- Safe garbage collection for soft-deleted judge assets is still open as `JUDGE-REV-FND-D001`.
- React Hooks compiler-level lint findings and Fast Refresh boundaries are deferred to 4E as `PLATFORM-04A-D001`.
- Hosted Docker judge CI needs a confirmed Docker-capable runner; tracked as `PLATFORM-04A-D002`.
- The frontend main bundle remains about 3.45 MB before gzip and is assigned to 4E.
- Production exposure, backup/restore, monitoring, and Docker daemon permissions remain later-stage work.
- Production browser sessions require verified HTTPS termination/forwarded-scheme configuration (`AUTH-04C-D001`).
- Real Excel/LibreOffice import verification is blocked by the current host and tracked as `EXPORT-04C-D001`.

## 8. Current Immediate Task

`IDENTITY-SESSION-EXPORT-04C` implementation and executable gates are complete. The external Office import check is explicitly NotRun. Await the next explicit instruction before starting Stage 4D or 4E.

Read first:

1. `AGENTS.md`
2. `.agents/skills/onlinejudge-project-context/SKILL.md`
3. `docs/ai/tasks/IDENTITY-SESSION-EXPORT-04C.md`
4. Current source/configuration files explicitly named by the next task card

## 9. Next Approved Stage Boundary

Stage 4C stops after identity/session/export hardening. Stage 4D/4E scope must be explicitly approved before implementation.

## 10. Stop Rules

- Do not push, publish externally, merge, or expose services without explicit authorization.
- Do not change public HTTP DTOs, persisted formats, or core judge authority boundaries outside an approved stage contract.
- Do not claim hosted CI passed until GitHub Actions actually executes.
- Mandatory gate failures block stage completion; skipped checks must be reported as NotRun.
