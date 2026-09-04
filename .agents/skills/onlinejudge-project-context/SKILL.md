---
name: onlinejudge-project-context
description: Load verified, stable OnlineJudge architecture, judge pipeline, persistence, verification, and modification-boundary facts. Use for tasks involving this repository, especially API, worker, Redis queue, PostgreSQL, Docker sandbox, judge languages, deployment planning, or cross-module changes.
---

# OnlineJudge Project Context

Use this context to orient work, then re-read the exact source files affected by the current task. Treat current code as authoritative if it differs from this snapshot.

## Project Goal

- Build a simplified LeetCode-style Online Judge with problem management, test cases, code submission, asynchronous judging, and result tracking.

## Solution Structure And Boundaries

- `OnlineJudge.Api`: ASP.NET Core HTTP composition and thin controllers; do not execute submissions here.
- `OnlineJudge.Application`: use-case contracts, DTOs, requests, results, and judge abstractions.
- `OnlineJudge.Domain`: entities and enums; persisted enum values and entity shapes are compatibility-sensitive.
- `OnlineJudge.Infrastructure`: EF Core/PostgreSQL, Redis, authentication, external integrations, runner implementations, and Docker sandbox.
- `OnlineJudge.JudgeWorker`: queue consumer and judge orchestration; owns asynchronous submission processing.
- `OnlineJudge.Tests`: xUnit tests for application/infrastructure behavior and judge code builders.
- `frontend`: React/TypeScript/Vite SPA with Monaco Editor.
- `sandbox`: Docker image definitions used by judge runners.

## Verified Technology Stack

- All solution projects target `.NET 10` (`net10.0`); `global.json` pins SDK `10.0.400` with `latestPatch` roll-forward.
- Backend uses ASP.NET Core, EF Core, Npgsql/PostgreSQL, StackExchange.Redis, and a .NET Worker Service.
- Frontend uses React 19, TypeScript 5.7, Vite 6, and Monaco Editor.
- Tests use xUnit and include judge function-builder and runner guard coverage.

## Judge Pipeline

- PostgreSQL `JudgeJob` rows are the durable work authority. Redis carries best-effort submission wake hints; the Worker also polls PostgreSQL when no usable signal is available.
- A worker process registers one hosted `Worker`. Its loop awaits each `JudgeJobProcessor.ProcessAsync` before claiming the next job; no in-process parallel judge concurrency is configured.
- `JudgeLanguage` contains `Cpp17`, `C11`, and `CSharp`. Dependency injection registers a matching runner for all three.
- C11 and C++17 use the GCC sandbox image; C# uses the .NET SDK sandbox image.
- `DockerJudgeSandbox` invokes the host `docker` CLI. It creates a transient workspace under the system temp directory, runs compilation in one disposable container, then runs each test case sequentially in a new disposable container.
- Docker runs with no network, a memory limit, one CPU, a PID limit, a bind-mounted workspace, and `--rm`. Treat host Docker access and sandbox permissions as high risk.

## Persistence

- PostgreSQL stores EF Core data; production Compose mounts operator-created external PostgreSQL and Redis volumes whose exact names are required in the infrastructure environment file.
- PostgreSQL stores judge-job lease, heartbeat, retry and dead-letter state. Redis loss can delay a wake-up but does not own job completion truth.
- API image uploads are written below `OnlineJudge.Api/wwwroot/uploads/images`.
- Challenge file submissions are written below `OnlineJudge.Api/App_Data/challenge-file-submissions`, with file paths stored in PostgreSQL.
- Judge workspaces under the system temp directory are transient and deleted best-effort.
- Runtime upload and `App_Data` locations are intentionally Git-ignored; production persistence must be explicitly mounted and backed up.

## Current Capacity Boundary

- One Worker process handles one submission at a time, and its sandbox executes test cases sequentially. Concurrent submissions wait in the durable queue rather than running in parallel inside that process.
- Sandbox process/output/file guards are implemented, but authoring and submission inputs do not yet have one centralized, end-to-end policy for maximum source bytes, time/memory values, test-case count, per-case bytes, or aggregate revision bytes.
- Do not increase Worker concurrency before hard input/resource limits, queue-age observability, and a 2 vCPU / 4 GB load envelope are verified.

## Verification Entry Points

- Build the solution with `dotnet build OnlineJudge.sln`.
- Run solution tests with `dotnet test OnlineJudge.sln`.
- Build the frontend with `npm run build` from `frontend/` after dependencies are available.
- Inspect `scripts/e2e/run-all-checks.ps1` before using the combined build/test/frontend/E2E workflow.
- Require real Docker image and runner execution evidence for sandbox claims; static inspection is not runtime verification.

## High-Risk Areas

- Docker host access, sandbox isolation, resource limits, and per-test-case container lifecycle.
- JudgeWorker queue consumption, failure recovery, status transitions, and concurrency.
- EF migrations, persisted enums, database credentials, and production PostgreSQL durability.
- Runtime uploads and challenge-file persistence.
- JWT, SMTP, database, and cloud credentials. Never copy actual values into plans or governance documents.
- Public DTO/API changes and frontend/backend base-URL or CORS coordination.

## Secret Configuration Boundary

- Database credentials: `docker-compose.yml`, API appsettings, and JudgeWorker appsettings; production must externalize.
- JWT secret: API appsettings; production must externalize.
- SMTP credential: API appsettings/environment binding; production must externalize.
- Record only secret type and configuration location. Never record values, tokens, private keys, or production credentials.

## AI Modification Boundaries

- Preserve module ownership and keep controllers thin.
- Do not move user-code execution into the API process.
- Do not change queue semantics, worker concurrency, sandbox permissions, persisted data shape, migrations, or public APIs without explicit scope and compatibility review.
- Do not modify runtime data, secrets, generated output, dependency caches, or build artifacts.
- Revalidate exact APIs and current configuration before implementation; this skill is context, not a substitute for Code Truth.
