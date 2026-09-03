# OnlineJudge Context Capsule

## 1. Scope

- Project: OnlineJudge
- Module / Stage: Platform improvement Stage 4B
- Updated: 2026-09-03
- Current task: `SANDBOX-DATAPLANE-04B`

## 2. Current Goal

Preserve the verified Stage 0-3 judge reliability contracts and the .NET 10/CI baseline while hardening the Docker judge data plane and function-mode regression gates.

## 3. Current Git Baseline

- Root: `H:/GitHub/OJ`
- Branch: `main`
- Stage 0-3 baseline commit: `35576d2` (`feat: harden judge revisions and durable job processing`)
- Stage 4A implementation commit: `6511486` (`build: establish net10 and continuous verification baseline`)
- Stage 4A governance commit: `031ab81` (`docs: record platform baseline stage results`)
- Stage 4B changes are local and uncommitted pending user review.
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

## 6. Verification Baseline

- Release solution build: passed with 0 warnings and 0 errors.
- Backend tests: Stage 4B targeted function/sandbox suite passed 105 / 105 and the final full suite passed 968 / 968 on `net10.0`.
- EF model drift: no pending model changes.
- Frontend lint: passed under the 4A stable baseline rule set.
- Frontend tests: 3 / 3 Vitest tests passed.
- Frontend production build: passed; the existing 3.45 MB main-chunk advisory remains.
- Judge Docker smoke: 7 / 7 passed, including C11/C++17/C# Accepted, C++17 Wrong Answer/Compile Error, combined-output termination, and read-only runtime workspace.
- Function-mode Docker regression: 10 / 10 main E2E scenarios and 3 / 3 custom-struct language scenarios passed.
- Sandbox security smoke: passed with 50 leak-cleanup runs; runtime host writes and oversized `/tmp` file writes are blocking assertions.
- GitHub Actions workflow exists for push/pull-request backend and frontend fast gates; hosted execution is not locally verifiable.

## 7. Known Technical Debt And Risks

- Runtime workspace, temporary disk, individual file size, and combined stdout/stderr are bounded. A portable aggregate quota for the writable compiler bind mount remains tracked as `SANDBOX-04B-D002`.
- Safe garbage collection for soft-deleted judge assets is still open as `JUDGE-REV-FND-D001`.
- React Hooks compiler-level lint findings and Fast Refresh boundaries are deferred to 4E as `PLATFORM-04A-D001`.
- Hosted Docker judge CI needs a confirmed Docker-capable runner; tracked as `PLATFORM-04A-D002`.
- The frontend main bundle remains about 3.45 MB before gzip and is assigned to 4E.
- Production exposure, backup/restore, monitoring, and Docker daemon permissions remain later-stage work.

## 8. Current Immediate Task

`SANDBOX-DATAPLANE-04B` is complete after its final full repository gate is recorded. Await the next explicit instruction before starting Stage 4C.

Read first:

1. `AGENTS.md`
2. `.agents/skills/onlinejudge-project-context/SKILL.md`
3. `docs/ai/tasks/SANDBOX-DATAPLANE-04B.md`
4. Current source/configuration files explicitly named by the next task card

## 9. Next Approved Stage Boundary

Stage 4B stops after sandbox/data-plane and function-mode reliability. Stage 4C scope must be explicitly approved before implementation.

## 10. Stop Rules

- Do not push, publish externally, merge, or expose services without explicit authorization.
- Do not change public HTTP DTOs, persisted formats, or core judge authority boundaries outside an approved stage contract.
- Do not claim hosted CI passed until GitHub Actions actually executes.
- Mandatory gate failures block stage completion; skipped checks must be reported as NotRun.
