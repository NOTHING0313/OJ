# AGENTS.md

## Project Goal

Build an Online Judge platform similar to a simplified LeetCode.

The system should support:
- Problem management
- Test case configuration
- Code submission
- Asynchronous judging
- Judge result tracking

## Tech Stack

Backend:
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Redis for queue
- .NET Worker Service for judge worker

Frontend:
- React
- TypeScript
- Vite
- Monaco Editor

Sandbox:
- Docker-based runner
- First language: C++17 only

## Architecture Rules

- Keep domain models in OnlineJudge.Domain.
- Keep use cases and DTOs in OnlineJudge.Application.
- Keep EF Core, Redis, and external integrations in OnlineJudge.Infrastructure.
- Keep HTTP controllers thin.
- Do not execute user code inside the API process.
- Judge tasks must be processed by OnlineJudge.JudgeWorker.
- Preserve clear boundaries between API, worker, and sandbox.

## Coding Rules

- Actively use deepseek MCP to generate code as long as mission is in mass scale and at low risk, particularly the leading-end part codes.
- When using deepseek MCP,transform a package followed by the global AGENTS.md.
- To handle the codes transformed by the deepseek,follow the rules provides by the global AGENTS.md.
- Use async APIs where appropriate.
- Add concise XML comments for complex public methods.
- Keep method parameters on one line when readable.
- Do not introduce unnecessary abstractions.
- Prefer small services with clear responsibilities.
- Add unit tests for core judge result comparison logic.