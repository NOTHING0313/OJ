# Task Review: JUDGE-QUEUE-RELIABILITY-03

## Status

`COMPLETED`

Stage 3 makes PostgreSQL the durable judge-work authority, keeps Redis as an optional wake-up hint, and protects terminal effects with expiring fenced leases.

## Contract

- Submission and its one-to-one Pending `JudgeJob` are inserted in one database transaction.
- The transaction commits before the best-effort Redis signal. Redis failure never changes a successfully persisted submission response.
- Workers may use a Redis submission ID as a preferred candidate, but ownership is established only by an atomic PostgreSQL claim using row locking and `SKIP LOCKED`.
- Database polling is always active, including when Redis is empty or unavailable.
- A lease contains a unique token, owner, expiry, and attempt number. Heartbeats renew only the same unexpired token.
- Expired leases are reclaimable until the configured attempt bound; exhausted jobs become DeadLettered.
- Transient infrastructure failures are retried with bounded exponential delay. Permanent judge-configuration failures dead-letter immediately.
- User code execution is at-least-once. Durable terminal effects are fenced: submission result, case snapshots, challenge progress, season scoring, and JudgeJob completion commit atomically only for the current unexpired lease.
- A stale Worker discards its result. A completion transaction failure clears tracked mutations before the retry transition is persisted.
- Judge containers are labeled by submission. Startup removes only exited/dead managed containers; a reclaimed submission removes only its own prior containers.
- HTTP request and response DTOs and the Redis submission-ID payload are unchanged.
- Mixed old/new API and Worker deployment is unsupported; migrations and matching binaries are deployed as one maintenance operation.

## Stage Result Ledger

| Stage | Goal | Gate evidence | State |
|---|---|---|---|
| 3A | Add durable JudgeJob schema and lease state machine | Build, focused store/revision tests, EF drift check, valid/invalid PostgreSQL migration paths | Completed |
| 3B | Persist submission and job atomically; demote Redis to hint | Submission/caller tests including simulated Redis signal failure | Completed |
| 3C | Claim, poll, renew, retry, and dead-letter in Worker | Worker/store focused tests and source-boundary checks | Completed |
| 3D | Fence terminal effects and scope sandbox recovery | Processor, scoring, sandbox, and revision tests; two-Worker three-language Docker run | Completed |
| 3E | Fault injection, migration rollout, governance, and final verification | Redis-down pickup, forced Worker termination and lease recovery, injected PostgreSQL rollback, full repository gates | Completed |

## Verification Matrix

| Check | Result | Evidence |
|---|---|---|
| Focused processor rollback test | Passed | 6 / 6 processor tests |
| PostgreSQL valid migration | Passed | Pending backfilled; Judging reset and backfilled; terminal submission left without invented work |
| PostgreSQL invalid migration | Passed | Missing/mismatched revision aborted and rolled back |
| Migration rollback guard | Passed | Down migration refused while Pending work existed |
| Concurrent Workers | Passed | Two Workers claimed three jobs once each through PostgreSQL |
| Real Docker languages | Passed | C11, C++17, and C# each completed Accepted with one case result |
| Redis unavailable | Passed | Database polling completed a job on attempt 1 while Redis was stopped |
| Worker process termination | Passed | Expired attempt was reclaimed as attempt 2; submission-scoped orphan container was removed; one terminal result persisted |
| Atomic completion rollback | Passed | Injected case-result insert failure left submission Pending, no finish timestamp, no case results, and a retryable job; next attempt completed once |
| Development database migration | Passed | `20260903004529_AddJudgeJobs` applied |
| Release solution build | Passed | 0 warnings / 0 errors |
| Full backend suite | Passed | 962 / 962 |
| Frontend production build | Passed | TypeScript and Vite completed; existing large-chunk advisory remains non-blocking |
| EF model drift | Passed | No changes since the latest migration |

## Diff Intent Table

| Change group | Intended effect | Scope check |
|---|---|---|
| JudgeJob domain, EF configuration, migration, and store | Durable work state, claim/lease/retry/dead-letter transitions, safe legacy backfill | Stage 3 contract only |
| Submission service and Redis queue | Atomic submission/job creation; best-effort wake-up signal | HTTP and Redis payload shapes unchanged |
| Worker and processor | Database-authoritative pickup, heartbeat, failure classification, fenced atomic completion | API process remains free of user-code execution |
| Docker sandbox maintenance | Submission ownership labels and multi-Worker-safe orphan cleanup | Existing sandbox isolation flags retained |
| Runner result classification | Distinguish retryable infrastructure failure from permanent configuration failure | Successful and user-code failure statuses unchanged |
| Tests and configuration | Cover state transitions, rollback, container ownership, and updated contracts | No production dependency added |
| Governance documents | Record public contracts, architecture decisions, debt repayment, and verification evidence | Documentation only |

## Deferred Governance Queue

| Item | Disposition |
|---|---|
| JudgeJob persistence and lease semantics | Flushed to `PublicApiLedger.md` and `DecisionLog.md` |
| Redis wake-hint authority boundary | Flushed to `PublicApiLedger.md` and `DecisionLog.md` |
| Submission-scoped container cleanup | Flushed to `PublicApiLedger.md` and `DecisionLog.md` |
| Stage 2 queue-reliability debt | Marked resolved in `TechnicalDebtRegister.md` |
| New implementation shortcuts or unresolved correctness debt | None accepted |

## Documentation Update Table

| Document | Update |
|---|---|
| `docs/ai/context/DecisionLog.md` | Added durable-authority, fencing, and sandbox-ownership decisions |
| `docs/ai/context/PublicApiLedger.md` | Added persisted model and application/runtime contracts |
| `docs/ai/context/TechnicalDebtRegister.md` | Marked `JUDGE-REV-BIND-D001` resolved |
| `docs/ai/tasks/JUDGE-QUEUE-RELIABILITY-03.md` | Recorded contract, stage ledger, verification, rollout, and boundaries |

## Deployment And Operations

- Stop old API and Worker binaries before applying Stage 3 migrations; start only matching Stage 3 binaries afterward.
- Migration rejects pending/judging rows that cannot be tied to a valid immutable revision. Existing Judging rows are reset to Pending and any partial case results are cleared before jobs are backfilled.
- `JudgeJobs` settings control lease duration, heartbeat interval, database poll interval, maximum attempts, retry delay bounds, and Redis signal timeout. Heartbeat must not exceed one third of lease duration.
- Dead-letter records retain a bounded sanitized error classification. This stage does not expose a replay/admin endpoint; operators can inspect the table while a future UI/API can reuse `IJudgeJobStore` semantics.

## Known Boundaries

- At-least-once execution means submitted code can run again after an ambiguous process failure; only durable effects are at-most-once through fencing.
- PostgreSQL is a required dependency. Redis is optional for correctness but improves pickup latency.
- Lease settings must exceed realistic database and Docker scheduling pauses; aggressive values used in fault tests are not production recommendations.
- Automatic admin replay, attempt-history records, and production metrics/alerts are future operational features, not hidden correctness dependencies of this stage.
