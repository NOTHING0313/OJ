# Task Review: JUDGE-REVISION-BINDING-02

## Status

`COMPLETED`

Stage 2 binds every new submission to one immutable problem judge revision and makes the JudgeWorker build its judge request exclusively from that revision.

## Contract

- `Submission.ProblemJudgeRevisionId` is assigned once during submission creation and cannot be changed through EF Core.
- The database column is nullable only for completed legacy submissions whose original judge definition cannot be reconstructed safely.
- Pending legacy submissions are bound to the problem's current revision during migration; migration aborts if that revision is unavailable or belongs to another problem.
- Migration rejects any legacy submission already marked `Judging`, because its old Worker may already have constructed a request from mutable data.
- Submission language validation reads the selected revision, not mutable problem authoring fields.
- Redis continues to carry only the submission ID. PostgreSQL is the authority for the bound revision.
- Worker reads judge mode, function specification, resource limits, ordered test-case snapshots, and compile assets from the bound revision.
- `SubmissionJudgeRequestFactory` is the single mapping path from a bound revision to `JudgeRequest` and is reusable by the future JudgeJob processor.
- Revision test cases expose their `SourceTestCaseId` to existing result, challenge-score, and DTO contracts.
- A missing, mismatched, or empty revision produces an explicit `SystemError`; Worker never falls back to mutable problem data.

## Compatibility

- Existing HTTP request and response DTOs are unchanged.
- Existing Redis queue values are unchanged.
- Completed legacy submissions remain readable with a null revision binding.
- Existing submission case-result foreign keys and snapshots remain valid.
- The problem-scoped asset loader remains available for compatibility, but Worker uses only revision-scoped loading.

## Explicitly Deferred

- Durable `JudgeJob` persistence.
- Redis acknowledgement/retry semantics and Worker lease recovery.
- Transactional outbox or equivalent database-to-queue delivery guarantee.
- Function protocol redesign and per-case sandbox workspace changes.

## Stage Result Ledger

| Stage | Goal | Verification | State | Next Entry |
|---|---|---|---|---|
| 2A | Bind new submissions to immutable revisions | Submission contract tests and immutable-binding guard | Completed | Worker may consume the binding |
| 2B | Switch Worker test cases and assets to the bound revision | Request-factory behavior tests, Worker source contract, and retained-asset tests | Completed | JudgeJob reliability may reuse the same request factory |
| 2C | Preserve legacy data during migration | PostgreSQL valid and two invalid migration paths | Completed | Operational rollout must stop Workers, drain `Judging`, and provide revisions for Pending submissions |

## Verification Matrix

| Step | Status | Evidence |
|---|---|---|
| .NET build | Passed | `dotnet build OnlineJudge.sln --no-restore`, 0 warnings / 0 errors |
| Targeted affected tests | Passed | 83 / 83 broader affected set; 42 / 42 final focused set including request-factory tests |
| Full backend suite | Passed | 949 / 949 |
| EF model drift | Passed | No pending model changes after migration generation |
| PostgreSQL valid upgrade | Passed | Pending submission bound to revision; completed legacy submission remained null |
| PostgreSQL invalid upgrades | Passed | Missing current revision and existing Judging submission each aborted migration and rolled back the new column |
| Current development database migration | Passed | `BindSubmissionsToJudgeRevisions` applied successfully |
| Frontend production build | Passed | TypeScript and Vite production build completed |

## Known Boundary At Stop

The judge definition is now immutable end to end, but queue delivery is not yet durable. The existing database transaction plus Redis push can still lose or strand work during process or network failure. This is tracked as `JUDGE-REV-BIND-D001` and must be addressed by the JudgeJob/lease stage.
