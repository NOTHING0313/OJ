# Task Review: PLATFORM-BASELINE-04A

## Status

`COMPLETED`

Stage 4A seals the verified Stage 0-3 judge reliability baseline, upgrades every .NET and C# sandbox target to .NET 10, and establishes reusable backend/frontend/EF quality gates for local execution, GitHub Actions, and production packaging.

## Risk Classification

- Initial risk: `R4` because the task changes repository-wide runtime/toolchain versions, sandbox compilation, CI, and release packaging.
- Final risk: `R4`; no scope expansion occurred, but sandbox/runtime/release impact remains cross-cutting.
- Governance mode: continuous staged governance with final flush to decision, debt, context, and task ledgers.

## Contract

- Preserve immutable `ProblemJudgeRevision` selection and PostgreSQL `JudgeJob` authority introduced by Stages 1-3.
- Keep Redis a best-effort wake-up hint and keep all submitted-code execution in `OnlineJudge.JudgeWorker`.
- Target `net10.0` in all repository projects and generated C# submission projects; pin SDK `10.0.400`.
- Align ASP.NET Core/EF packages and EF tool to `10.0.11`, Npgsql EF provider to `10.0.3`, and C# sandbox SDK image to .NET 10.
- Add fast hosted CI for backend build/test/EF drift and frontend lint/test/build by reusing the local verification script.
- Extend production publishing to enforce EF drift and frontend lint/test before creating Linux x64 artifacts.
- Do not add or change HTTP DTOs, persisted entities, database migrations, Redis payloads, or judge result semantics.
- Do not begin sandbox resource-governance implementation assigned to Stage 4B.

## Stage Result Ledger

| Step | Goal | Gate evidence | State |
|---|---|---|---|
| 4A.1 | Verify and seal Stage 0-3 baseline | 962 backend tests; frontend build; EF drift; three-language Docker smoke; security probe | Completed |
| 4A.2 | Create recoverable local baseline | Commit `35576d2`; `output/` excluded; no remote push | Completed |
| 4A.3 | Upgrade host/tooling/sandbox to .NET 10 | Release build 0 warnings/errors; EF 10 tool; C# 10 image built | Completed |
| 4A.4 | Establish frontend lint and Vitest foundation | ESLint gate passed; 3/3 utility tests passed; production build passed | Completed |
| 4A.5 | Establish reusable hosted/local fast CI | `run-all-checks.ps1` 4/4 passed; GitHub workflow added for push/PR | Completed; hosted run NotRun locally |
| 4A.6 | Validate Docker and production packaging | Three-language smoke, 50-run security smoke, Linux artifacts, EF bundle, archive and SHA-256 passed | Completed |

## Verification Matrix

| Check | Result | Evidence |
|---|---|---|
| Stage 0-3 baseline backend suite | Passed | 962 / 962 on .NET 9 before baseline commit |
| Stage 0-3 frontend build | Passed | TypeScript and Vite completed before baseline commit |
| Stage 0-3 EF drift | Passed | No model changes before baseline commit |
| Baseline Docker judge smoke | Passed | C11/C++17/C# Accepted; Wrong Answer and Compile Error paths passed |
| .NET 10 solution build | Passed | 0 warnings / 0 errors |
| .NET 10 backend suite | Passed | 962 / 962 |
| EF 10 model drift | Passed | No changes since latest migration |
| Frontend lint | Passed | ESLint 10 baseline, zero warnings/errors |
| Frontend unit tests | Passed | 3 / 3 Vitest tests |
| Frontend production build | Passed with advisory | Build succeeded; existing 3.45 MB main-chunk advisory deferred to 4E |
| .NET 10 judge Docker smoke | Passed | C11/C++17/C# Accepted; Wrong Answer and Compile Error paths passed; report `artifacts/smoke/20260903-104533/result.json` |
| Sandbox security smoke | Passed with known risk | 50 leak runs; network/process/memory/timeout/isolation checks passed; disk quota remains explicit 4B risk |
| Canonical fast gate | Passed | Build, 962 tests, EF drift, frontend lint/test/build: 4 / 4 steps |
| Production release | Passed | Commit `6511486`; API, Worker, frontend, EF bundle, sandbox definitions, archive validated |
| Release archive hash | Passed | `931026902296dfcc5829884fa753d07648b85dd97fa77a30c1441c37ade46ae4` |
| Hosted GitHub Actions execution | NotRun | Workflow created locally; no push was authorized |

## Diff Intent Table

| Change group | Intended effect | Scope check |
|---|---|---|
| `global.json`, project files, EF tool manifest | One pinned .NET 10 host/tool baseline | No domain or API contract change |
| C# runner, sandbox Dockerfile, security smoke | Compile and validate submitted C# on .NET 10 | C11/C++17 behavior unchanged |
| Email/SMS verification services | Resolve .NET 10 JSON overload ambiguity by explicitly using Redis string content | Serialized record shape unchanged |
| Frontend package/config/test files | Add executable ESLint and Vitest baseline | No production UI behavior changed |
| Canonical verification and publisher scripts | Enforce backend, EF, frontend, and packaging gates consistently | Existing entry points reused |
| GitHub Actions workflow | Run scoped fast gates on push and pull request | No assumed Docker runner added |
| Governance documents | Record runtime/CI decisions, debt, stage evidence, and next boundary | Documentation only |

## Deferred Governance Queue

| Item | Disposition |
|---|---|
| Repository-wide .NET 10 baseline | Flushed to `DecisionLog.md` and context capsule |
| Reusable local/hosted verification entry point | Flushed to `DecisionLog.md` |
| React Hooks compiler/Fast Refresh lint findings | Recorded as `PLATFORM-04A-D001`, assigned to 4E |
| Hosted Docker CI runner ownership | Recorded as `PLATFORM-04A-D002` |
| Sandbox disk quota | Recorded as `SANDBOX-04B-D001`, assigned to 4B |
| Public API or persistence changes | None; no Public API Ledger entry required |

## Documentation Update Table

| Document | Update |
|---|---|
| `docs/ai/context/DecisionLog.md` | Added .NET 10 baseline and reusable verification decisions |
| `docs/ai/context/TechnicalDebtRegister.md` | Added frontend lint-depth, hosted Docker CI, and sandbox disk-quota debt |
| `docs/ai/context/OnlineJudge-Context-Capsule.md` | Replaced stale initialization facts with verified Stage 3/4A architecture and gates |
| `docs/ai/tasks/PLATFORM-BASELINE-04A.md` | Recorded contract, stage ledger, verification, diff intent, and boundaries |

## Known Boundaries

- The GitHub Actions workflow is committed but has not run remotely because no push was authorized.
- Docker judge/security checks are proven locally; hosted Docker execution awaits a confirmed runner.
- Full React Hooks 7 compiler recommendations are not enabled yet; the observed issues require planned 4E refactoring rather than baseline suppression disguised as remediation.
- The sandbox still lacks an enforced per-submission disk/output quota and reports this explicitly; 4B owns the fix.
- The release archive is a local artifact for validation only and was not published or deployed.

## Stop Rule Confirmation

Stage 4A is complete. Stage 4B was not started. No external publication, push, merge, service exposure, public API expansion, or persistence-format change was performed.
