# Initialization Record: OJ-AGENTS-INIT-01

## Current Stage

- Stage ID: `OJ-AGENTS-INIT-01`
- Goal: establish Managed Project Agents, verified project context, a reusable local Skill, and the next deployment Task Card.
- Product implementation: unchanged.

## Git Baseline

- Root: `E:/Github/OJ`
- Branch: `main`
- HEAD before initialization: `f88e9fa7f18a3b0b0e2bf74d29e20e8492346269`
- Working tree before initialization: clean
- Commit/push during initialization: prohibited and not performed

## Initialization Transition

- Before: no `.agent/`, no managed project configuration, and no registered project-local Skill.
- After: Schema V2 Managed Project Agents in `merge` mode, local template `2026.07-local-agents-v2`, registered `onlinejudge-project-context`, Context Capsule, and deployment Task Card.

## AGENTS Merge

- Previous rule: first judge language was documented as C++17 only.
- Verified reality: `JudgeLanguage`, DI registration, three runner implementations, function-builder tests, runner guard tests, and sandbox definitions support C11, C++17, and C#.
- Action: replace only the obsolete language line; preserve all other root `AGENTS.md` content.

## Created Or Modified Files

- `AGENTS.md`: minimal language-inventory correction.
- `.agent/AGENTS.md`: managed local project rules.
- `.agent/agents-mode.json`: Schema V2 mode, initialization state, artifacts, and local Skill registry.
- `.agents/skills/onlinejudge-project-context/SKILL.md`: verified stable project facts and boundaries.
- `.agents/skills/onlinejudge-project-context/agents/openai.yaml`: generated Skill UI metadata.
- `docs/ai/context/OnlineJudge-Context-Capsule.md`: current-state snapshot.
- `docs/ai/tasks/OJ-PRODUCTION-DEPLOY-01.md`: next-task contract only.
- `docs/ai/handoffs/OJ-AGENTS-INIT-01.md`: this initialization record.

## API And Data Model Changes

None. No product source, public contract, database schema, migration, configuration value, dependency, runtime data, or deployment behavior was changed.

## Verification

- Git scope, `git diff --check`, JSON parsing, Managed marker validation, manual no-dependency Skill validation, and redacted Secret scan: PASS.
- Official `quick_validate.py`: EnvironmentBlocked because both available Python runtimes lack `PyYAML`; no dependency was installed. Equivalent frontmatter, naming, TODO, and UI metadata checks passed.
- Build/test/frontend/Docker/deployment verification: NotRun by task design.

## Known Debt And Open Decisions

- Production networking, secrets, persistence, Docker access, resource budget, reverse proxy, TLS, service lifecycle, logging, backup, and rollback remain open under `OJ-PRODUCTION-DEPLOY-01`.
- Current queue recovery and sandbox container lifecycle are documented but intentionally outside initialization scope.

## Next Codex Instruction

```text
@plan
Task ID: OJ-PRODUCTION-DEPLOY-01
Read .agent/AGENTS.md, the registered onlinejudge-project-context Skill, the current Context Capsule, and the Task Card. Revalidate current code/configuration and produce a bounded production-deployment implementation plan. Do not modify files or external systems during planning.
```
