# Task Review: JUDGE-REVISION-FOUNDATION-01

## Status

`COMPLETED`

The Stage 0 contract and Stage 1 implementation are complete. Backend, PostgreSQL migration, and frontend production-build verification passed.

## Authorized Scope

- Define immutable problem judge revision contracts.
- Add the revision persistence model and migration.
- Create a revision when a problem is published or an already-published judge definition changes.
- Reject publishing without an active test case and reject deleting the last active case of a published problem.
- Retain judge assets referenced by historical revisions through soft deletion.
- Adjust new-problem UI so creation always starts as a draft.

Explicitly deferred: Submission revision binding, JudgeJob, Worker lease/retry, function protocol changes, and per-case workspace isolation.

## Architecture Contract

- `Problem`, `TestCase`, and active `ProblemJudgeAsset` rows remain mutable authoring state.
- `ProblemJudgeRevision` and its child rows are immutable judge-definition snapshots.
- `Problem.CurrentJudgeRevisionId` selects the definition for future submissions.
- Published judge-sensitive mutations are serialized with a PostgreSQL transaction advisory lock.
- Presentation-only edits reuse the current revision.
- New submissions do not consume the revision yet; that is the required next stage.

## Compatibility And Migration

- Existing HTTP request and response shapes are unchanged.
- Existing valid published problems receive deterministic revision 1 during migration.
- Migration aborts and rolls back when a published problem has zero active test cases or basic judge-mode/test-case shape incompatibility.
- Draft problems remain without a current revision until publication.
- Judge assets use a filtered unique index so a soft-deleted filename can be reused.

## Stage Result Ledger

| Stage | Goal | Verification | State | Next Entry |
|---|---|---|---|---|
| 0 | Freeze ownership, lifecycle, compatibility, and verification contracts | Architecture/data-model review and source audit | Completed | Revision model authorized |
| 1 | Implement immutable revisions and publication invariants | 53/53 targeted tests; 944/944 full backend tests; PostgreSQL valid/invalid migration paths; frontend production build | Completed | Stage 2 may bind submissions and Worker reads to revisions |

## Verification Matrix

| Step | Status | Evidence |
|---|---|---|
| .NET build | Passed | `dotnet build OnlineJudge.sln --no-restore`, 0 warnings / 0 errors |
| Targeted problem tests | Passed | 53 / 53 |
| Full backend suite | Passed | 944 / 944 |
| EF model drift | Passed | No pending model changes after rebuild |
| PostgreSQL valid upgrade | Passed | Legacy published problem produced revision 1 with one case and one asset |
| PostgreSQL invalid upgrade | Passed | Published zero-case problem aborted migration; schema transaction rolled back |
| Current development database migration | Passed | `AddProblemJudgeRevisions` applied successfully |
| Frontend production build | Passed | `npm ci`; `npm run build`; TypeScript and Vite production build completed |
| Diff whitespace check | Passed | `git diff --check`; only line-ending notices |

## Known Boundary At Stop

Worker still reads mutable current problem/test-case/asset data. The repository must not claim immutable submission judging until Stage 2 binds Submission to `ProblemJudgeRevision` and switches Worker reads to that revision.
