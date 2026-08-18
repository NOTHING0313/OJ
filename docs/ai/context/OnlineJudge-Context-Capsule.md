# OnlineJudge Initial Context Capsule

## 1. Scope

- Project: OnlineJudge
- Module / Stage: Managed Project Agents initialization
- Updated: 2026-08-17
- Prepared for: `OJ-PRODUCTION-DEPLOY-01`

## 2. Current Goal

Establish a production-ready deployment plan and implementation for the existing OnlineJudge on a small Ubuntu server without redesigning judge algorithms, database schema, or UI features.

## 3. Current Git Baseline

- Root: `E:/Github/OJ`
- Branch: `main`
- Baseline HEAD: `f88e9fa7f18a3b0b0e2bf74d29e20e8492346269`
- Baseline commit: `chore: establish repository baseline`
- Agents initialization is intentionally left uncommitted for user review.

## 4. Current Architecture Invariants

- Domain models stay in `OnlineJudge.Domain`; application contracts and DTOs stay in `OnlineJudge.Application`.
- EF Core, PostgreSQL, Redis, Docker, and external integrations stay in `OnlineJudge.Infrastructure`.
- HTTP controllers remain thin and never execute submitted code.
- `OnlineJudge.JudgeWorker` consumes judge work and invokes the sandbox.
- Judge languages currently supported by enum, DI, runner code, and tests are C11, C++17, and C#.

## 5. Current Architecture Snapshot

- Backend: ASP.NET Core and EF Core on `net9.0`, PostgreSQL persistence, Redis queue.
- Frontend: React 19, TypeScript 5.7, Vite 6, Monaco Editor.
- Queue: submission IDs are pushed right and popped left from `judge:submissions:pending`.
- Worker: one hosted worker per process; submissions and their test cases are processed sequentially in current source.
- Sandbox: host `docker run`, disposable compile and per-test-case containers, no container network, bounded memory/CPU/PIDs, transient bind-mounted workspace.
- Persistent files: API uploads below `wwwroot/uploads/images`; challenge files below `App_Data/challenge-file-submissions`; PostgreSQL uses a named Compose volume.

## 6. Known Deployment State

- `docker-compose.yml` currently defines PostgreSQL and Redis only.
- PostgreSQL and Redis currently publish host ports.
- Database and JWT configuration contains development-only credential material that production must externalize.
- Frontend API base URL is fixed to a localhost development endpoint.
- API CORS currently allows Vite localhost origins.
- No repository-managed Nginx, systemd, HTTPS, production orchestration, or backup configuration was found.
- Runtime upload and challenge-file persistence require explicit production mounts and backup policy.
- JudgeWorker production access to the host Docker daemon is not yet designed or security-reviewed.

## 7. Known Technical Debt And Risks

- Redis list pop has no explicit acknowledgement/retry ledger in the current queue path.
- Worker concurrency is sequential and has no configured multi-worker policy.
- Sandbox creates a new container for compilation and each test case; optimization is deferred.
- One submission memory limit currently feeds sandbox execution; compile/runtime limit separation is deferred.
- Production security, resource budgeting, observability, backup, and rollback are unverified.

## 8. Current Immediate Task

`OJ-PRODUCTION-DEPLOY-01`

Read first:

1. `.agent/AGENTS.md`
2. `.agents/skills/onlinejudge-project-context/SKILL.md`
3. `docs/ai/tasks/OJ-PRODUCTION-DEPLOY-01.md`
4. Current source/configuration files explicitly named by the task card

## 9. Explicit Out Of Scope

- Judge core algorithm refactoring
- Per-test-case container lifecycle optimization
- Compile/runtime memory-limit separation
- In-process parallel judging or multiple workers
- .NET 9 to .NET 10 upgrade
- Database schema redesign
- UI feature work

## 10. Verification Baseline

- Git baseline: verified at `f88e9fa7f18a3b0b0e2bf74d29e20e8492346269` before initialization.
- Initialization verification: static Git, JSON, Skill, scope, and secret checks only.
- Build, tests, frontend build, Docker execution, and deployment validation: NotRun by design for initialization.

## 11. Handoff Notes

- Treat deployment items as pending requirements, not implemented facts.
- Revalidate current HEAD and working-tree ownership before the deployment task.
- Never copy credential values into plans, logs, or governance documentation.
- Stop before any public exposure, remote write, dependency upgrade, persistent-data migration, or Docker permission expansion not explicitly authorized.
