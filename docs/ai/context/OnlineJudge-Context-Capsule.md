# OnlineJudge Context Capsule

## 1. Scope

- Project: OnlineJudge
- Module / Stage: Platform improvement Stage 4A
- Updated: 2026-09-03
- Current task: `PLATFORM-BASELINE-04A`

## 2. Current Goal

Complete the approved 4A platform baseline: preserve the verified Stage 0-3 judge reliability work, standardize the repository on .NET 10, and establish reusable local/hosted fast quality gates before sandbox and operations changes.

## 3. Current Git Baseline

- Root: `H:/GitHub/OJ`
- Branch: `main`
- Stage 0-3 baseline commit: `35576d2` (`feat: harden judge revisions and durable job processing`)
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
- Sandbox: host Docker execution, no container network, bounded memory/CPU/PIDs, read-only container root, submission-scoped labels and cleanup.
- C# submissions compile against the .NET 10 SDK sandbox image and generated `net10.0` project.

## 6. Verification Baseline

- Release solution build: passed with 0 warnings and 0 errors.
- Backend tests: 962 / 962 passed on `net10.0`.
- EF model drift: no pending model changes.
- Frontend lint: passed under the 4A stable baseline rule set.
- Frontend tests: 3 / 3 Vitest tests passed.
- Frontend production build: passed; the existing 3.45 MB main-chunk advisory remains.
- Judge Docker smoke: C11, C++17, and C# Accepted; C++17 Wrong Answer and Compile Error paths passed.
- Sandbox security smoke: passed with 50 leak-cleanup runs; disk quota remains an explicit 4B risk.
- GitHub Actions workflow exists for push/pull-request backend and frontend fast gates; hosted execution is not locally verifiable.

## 7. Known Technical Debt And Risks

- Per-submission sandbox disk/output quota is not enforced; tracked as `SANDBOX-04B-D001`.
- Safe garbage collection for soft-deleted judge assets is still open as `JUDGE-REV-FND-D001`.
- React Hooks compiler-level lint findings and Fast Refresh boundaries are deferred to 4E as `PLATFORM-04A-D001`.
- Hosted Docker judge CI needs a confirmed Docker-capable runner; tracked as `PLATFORM-04A-D002`.
- The frontend main bundle remains about 3.45 MB before gzip and is assigned to 4E.
- Production exposure, backup/restore, monitoring, and Docker daemon permissions remain later-stage work.

## 8. Current Immediate Task

Finish `PLATFORM-BASELINE-04A` by validating the clean-commit production release artifact and recording the final stage ledger. Stop after 4A; do not begin 4B without the next explicit stage instruction.

Read first:

1. `AGENTS.md`
2. `.agents/skills/onlinejudge-project-context/SKILL.md`
3. `docs/ai/tasks/PLATFORM-BASELINE-04A.md` once created
4. Current source/configuration files explicitly named by the next task card

## 9. Next Approved Stage Boundary

Stage 4B is sandbox/data-plane hardening and function-judge reliability. It may implement disk/output budgets, lifecycle tightening, data-plane isolation, and function-mode regression coverage, but it must preserve the Stage 1-3 revision and durable-job contracts.

## 10. Stop Rules

- Do not push, publish externally, merge, or expose services without explicit authorization.
- Do not change public HTTP DTOs, persisted formats, or core judge authority boundaries outside an approved stage contract.
- Do not claim hosted CI passed until GitHub Actions actually executes.
- Mandatory gate failures block stage completion; skipped checks must be reported as NotRun.
