# Project Local AGENTS

<!-- CODEX:MANAGED:BEGIN -->

## Local Template Version

`2026.07-local-agents-v2`

## Project Identity

- Project: OnlineJudge, a simplified LeetCode-style online judge.
- Project root: `E:/Github/OJ`.

## Project Type And Toolchain

- .NET solution: `OnlineJudge.sln`; all solution projects target `net9.0`.
- Frontend: React 19, TypeScript 5.7, Vite 6, and Monaco Editor.
- Persistence and queue: EF Core with PostgreSQL; Redis list-backed judge queue.
- Judge execution: `OnlineJudge.JudgeWorker` invokes Docker-based language runners.

## Authoritative Documentation

- Root project rules: `AGENTS.md`.
- Current project context: `docs/ai/context/OnlineJudge-Context-Capsule.md`.
- Active task card: `docs/ai/tasks/OJ-PRODUCTION-DEPLOY-01.md`.
- Existing product documentation remains under `docs/` and is not moved by Agents workflows.

## Architecture And Lifecycle Boundaries

- Keep domain entities and enums in `OnlineJudge.Domain`.
- Keep use cases, DTOs, requests, results, and service contracts in `OnlineJudge.Application`.
- Keep EF Core, Redis, Docker, and external integrations in `OnlineJudge.Infrastructure`.
- Keep API controllers thin; never execute submitted code inside the API process.
- Process judge work in `OnlineJudge.JudgeWorker`; preserve API, worker, queue, and sandbox boundaries.
- Current judge languages are C11, C++17, and C#; verify enum, DI registration, runner, builder, sandbox image, and tests together before changing inventory.

## Allowed Project Areas

- Select task scope explicitly before editing.
- Prefer the smallest module-specific source and test set required by the active task.
- Reuse `docs/` for project documentation and `docs/ai/` for AI governance artifacts.

## Protected Or Forbidden Areas

- Do not commit or expose runtime uploads, `App_Data`, environment files, credentials, private keys, build output, or dependency caches.
- Treat migrations, database shapes, public DTOs, queue semantics, Docker invocation, sandbox limits, and deployment configuration as compatibility or security boundaries.
- Do not modify production deployment behavior from a planning or initialization task.

## Project-Specific Code Style

- Preserve existing C# and TypeScript style and module ownership.
- Use async APIs where appropriate and concise XML comments for complex public methods.
- Do not add unnecessary abstractions or mix responsibilities across layers.

## Public API And Data Compatibility

- Report changes to public DTOs, enums, HTTP contracts, EF migrations, persisted paths, Redis queue semantics, or judge result shapes before implementation.
- Preserve the numeric values of persisted enums unless an explicit migration is authorized.

## Build And Verification Entrypoints

- Solution build: `dotnet build OnlineJudge.sln`.
- Solution tests: `dotnet test OnlineJudge.sln`.
- Frontend build: run `npm run build` from `frontend/` with existing dependencies available.
- Existing combined entrypoint: `scripts/e2e/run-all-checks.ps1`; inspect prerequisites and scope before running it.
- Docker/runner verification requires Docker and the project sandbox images; do not claim it passed from source inspection.

## Documentation Routing

- Product documentation: `docs/`.
- AI context: `docs/ai/context/`.
- Task cards: `docs/ai/tasks/`.
- Handoffs and initialization records: `docs/ai/handoffs/`.

## Local Overrides Of Global Defaults

- Use the verified three-language inventory instead of the former C++17-only stage assumption.
- Do not run build or test for governance-only changes unless the task explicitly expands verification scope.

## Registered Local Skills

- `onlinejudge-project-context`: `.agents/skills/onlinejudge-project-context/SKILL.md`.

## Known Unknowns

- Production API binding, reverse proxy, TLS, service management, logging, and backup strategy are not established in the current repository.
- Host Docker access and sandbox permissions for production require a dedicated security review.
- Production persistence and resource budgets remain to be validated by `OJ-PRODUCTION-DEPLOY-01`.

<!-- CODEX:MANAGED:END -->

## Manual Project Rules

