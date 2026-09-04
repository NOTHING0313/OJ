# Task Card: JUDGE-RESOURCE-INPUT-BOUNDS-05

## Status

`IMPLEMENTED_PENDING_TARGET_CAPACITY_RUN`

The user approved local implementation, including the additive submission-evaluation DTO. Database migration, Worker concurrency changes and production rollout remain out of scope.

## Implemented scope

- Added four derived submission metrics: maximum case time, average case time, maximum case peak memory and average case peak memory.
- Retained legacy cumulative `TimeUsedMs` and maximum-peak `MemoryUsedKb` semantics; no database migration was introduced.
- Added one configuration-backed resource policy used by problem authoring, test-case changes/import, submission ingress, immutable revision execution and the Worker wall-time budget.
- Added UTF-8 byte, numeric boundary, test-count, declared-time-product and aggregate test-data validation.
- Updated submission detail and user/admin list presentation.
- Changed stress queue measurement from Redis list length to PostgreSQL `JudgeJobs`, added oldest-pending age, open-loop submission arrival and four-metric judge output.

## Remaining external verification

- Audit actual target-database rows against the configured defaults before production rollout.
- Run the isolated 2C4G arrival-rate ladder and three-language judge matrix on the target host.
- Do not raise Worker concurrency until those measurements pass.

## Local verification result

- `dotnet test OnlineJudge.sln --no-restore`: passed, 1,021 tests passed and 8 Redis-dependent tests skipped by their existing environment gate.
- `npm run build` in `frontend`: passed, including TypeScript compilation, Vite production build and bundle-budget check.
- Python stress tools: argument-parser smoke checks passed.
- PostgreSQL inventory SQL, Linux collector execution and the isolated judge/capacity run are not verified locally because the target PostgreSQL/Linux host is not connected and Docker Desktop is not running.

## Original gaps addressed by the local implementation

The sandbox already limits process network/IPC access, CPU, memory, PIDs, individual files and combined output. The remaining gap is earlier and broader: problem authoring, test-case import/publish, submissions and Worker request construction do not share one maximum policy for payload and resource values.

Original source facts before this task:

- `CreateProblemRequest` exposes `TimeLimitMs` and `MemoryLimitMb` without a shared range validator.
- `ProblemJudgeDefinitionValidator` validates judge shape but not a full resource/input budget.
- `CreateSubmissionRequest.SourceCode` has no byte-length rule at the application boundary.
- Test-case count, per-case input/output bytes and aggregate immutable revision bytes have no common cap.
- `JudgeResourceLimits.ResolveRunMemoryLimitMb` enforces a minimum but not a maximum.
- One Worker handles one submission at a time, and one submission runs its test cases sequentially. An extreme accepted job therefore blocks all later jobs in that process.
- The production Worker unit has `MemoryMax=768M`, but Docker containers are created through the host daemon and require their own independently validated budget.

## Objective

Introduce one configurable, tested resource/input policy that rejects unsafe or operationally unreasonable work before it reaches the durable queue, while keeping immutable revision semantics and existing C11/C++17/C# behavior.

## Implemented default policy pending target validation

The following values are implemented configuration defaults. They are not accepted production constants until the target-data audit and isolated capacity run pass:

| Boundary | Proposed default | Reason |
|---|---:|---|
| Submission source | 512 KiB UTF-8 | Align with the existing secure judge-source upload order of magnitude |
| Problem title | 1-200 characters | Match the persisted title capacity and prevent database exceptions |
| Description / starter-code field | 256 KiB UTF-8 each | Bound API and database allocation without constraining normal course content |
| Time limit | 100-10,000 ms per test | Prevent near-zero and arbitrarily long test execution; special cases require an explicit policy change |
| Declared test-time budget | `TimeLimitMs * active test count <= 120,000 ms` | Prevent a large test suite from multiplying an otherwise valid per-test limit into a very long queue blocker |
| Submission judge wall time | 180 seconds including compile and container overhead | Provide a final operational stop when declared limits do not predict real elapsed time |
| Runtime memory | 16-512 MiB | Preserve the existing sandbox minimum and fit a small 2C4G host; validate C# at the lower practical values |
| Active test cases | 1-200 per revision | Bound sequential container starts while retaining large teaching suites |
| Test input or expected output | 1 MiB UTF-8 per field | Bound per-case disk and comparison allocation |
| Aggregate test data | 64 MiB per revision | Bound publish-time storage and Worker materialization |
| Batch import | 200 cases and 64 MiB per request | Prevent a single oversized transaction or partial resource exhaustion |
| Submission HTTP body | 1 MiB | Leave envelope headroom around source and metadata |

All byte limits must use UTF-8 byte counts, not `.Length`. Final values must be configuration-backed, validated at startup, and reconciled against existing production rows before enforcement.

## Architecture Check

- **Canonical ownership:** Application owns the policy contract; Infrastructure owns canonical validation and Worker request construction; API supplies an early request-size guard.
- **Write path:** Validate create/update/import requests before persistence; validate the complete judge definition again when publishing an immutable revision.
- **Read/execute path:** Worker treats an invalid legacy revision as a permanent configuration failure with an auditable terminal result or dead-letter transition. It must not silently clamp time or memory because that would change judge semantics.
- **Compatibility:** No migration was added. Submission responses gained an additive `evaluation` object while legacy time/memory fields retain their prior semantics. Database check constraints remain a separate option requiring persistence approval.
- **Reuse:** Extend the current problem-definition validator and sandbox resource-limit path; do not create controller-specific duplicate constants.

## Required Workstreams

### 1. Existing-data inventory — prepared, target execution pending

- Query maxima and distributions for source bytes, title/description/starter-code bytes, time/memory, test counts, per-case bytes and aggregate revision bytes.
- Record rows above proposed limits and decide whether to grandfather, repair or raise the limit before code changes.

### 2. Central policy and validation — implemented

- Add a single options/contract type with startup validation and environment overrides.
- Validate problem create/update, test add/update/import, judge revision publish and submission creation.
- Keep validation messages deterministic and avoid leaking hidden test content.

### 3. Transport and persistence defenses — implemented without database constraints

- Add endpoint request-size limits as an early rejection layer, while retaining application validation as the authority.
- Ensure oversized batch imports fail atomically before writes.
- Consider database check constraints only as a separately approved persistence hardening stage.

### 4. Worker fail-safe — implemented

- Revalidate persisted revision resource values before Docker invocation.
- Classify policy violations as permanent configuration failures; do not retry them and do not launch a container.
- Enforce a cancellation-aware total judge wall-time budget in addition to the existing per-test timeout. Map expiry to an existing auditable permanent/operational failure path unless a new public or persisted failure kind is separately approved; do not misreport it as a single-test Time Limit Exceeded result.
- Preserve PostgreSQL JudgeJob authority, lease fencing and immutable revision binding.

### 5. Capacity and observability — tooling implemented, target run pending

- Expose pending-job count, oldest pending age, job duration and result/failure rates without exposing submission content.
- Define a target SLO from measured P50/P95 judge duration and a bounded burst test on the 2C4G host.
- Evaluate a per-user active/pending cap and a global overload response only after product semantics are approved.
- Keep concurrency at one initially. Multiple Workers are a separate architecture/operations task after resource bounds and measurements pass.

## Verification Matrix Required By Implementation

- Minimum, maximum, one-below and one-above tests for every numeric boundary.
- Product and overflow-safe tests for the declared `time limit × active test count` budget, plus a total wall-time cancellation test.
- ASCII and multi-byte Unicode byte-length tests.
- Per-case and aggregate-size tests, including an oversized import proving no partial write.
- Publish-time rejection for invalid complete revisions.
- Worker test proving an invalid legacy revision launches no Docker container and is not retried as transient.
- Regression tests for normal standard/function-mode C11, C++17 and C# submissions.
- Target-host bounded burst measurement recording queue depth, oldest age, CPU, memory, swap and disk behavior.

## Acceptance Criteria

- One documented and configurable policy is used across all ingress and execution boundaries.
- Existing production data has been audited before enforcement.
- Invalid work is rejected before queueing when possible and fails permanently before Docker launch otherwise.
- No silent resource clamping changes immutable judge semantics.
- Boundary and aggregate tests pass, and normal three-language judging remains green.
- Queue/capacity metrics and a reproducible 2C4G burst report exist before any concurrency increase.

## Stop Conditions

Stop for review before:

- changing public response/DTO shapes beyond the approved additive `evaluation` object;
- adding database constraints or migrations;
- rejecting existing production rows without a migration/repair decision;
- changing queue authority, retry semantics, Worker count/concurrency, Docker privileges or sandbox trust boundaries;
- choosing admission-control semantics that affect user-visible submission guarantees.
