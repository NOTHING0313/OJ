# Decision Log

## Decision: Published judge definitions are immutable revisions

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-REVISION-FOUNDATION-01
- Context: Worker currently reads mutable problem data after submission creation, so later edits can change a queued submission's judge definition.
- Decision: Mutable problem/test-case/asset rows remain authoring state; publication creates an immutable `ProblemJudgeRevision` snapshot selected by `Problem.CurrentJudgeRevisionId`.
- Rejected alternatives:
  - Snapshot on every submission: duplicates large test and asset metadata and makes submission creation unnecessarily expensive.
  - Treat current mutable rows as historical truth: cannot reproduce prior judge decisions.
- Consequences: Published judge-sensitive edits create a new revision; Stage 2 must bind each new Submission to one revision.

## Decision: Published edits auto-promote a new revision

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-REVISION-FOUNDATION-01
- Context: The current UI has no separate draft-versus-live editing workflow for an already-published problem.
- Decision: Judge-sensitive edits to a published problem automatically create a new revision. Presentation-only edits reuse the existing revision. Unpublish then republish creates a new revision.
- Rejected alternatives:
  - Add a second explicit draft publication workflow now: materially expands UI and API scope.
  - Create a revision for every title or description change: creates snapshots with no judge-semantic difference.
- Consequences: Existing administration behavior remains familiar while judge history becomes stable.

## Decision: Referenced judge assets are soft-deleted

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-REVISION-FOUNDATION-01
- Context: Physical asset deletion would make historical revisions incomplete.
- Decision: Asset removal hides the active attachment but retains its integrity-checked file and row for historical revisions.
- Rejected alternatives:
  - Copy asset contents into every revision: causes significant storage duplication.
  - Continue immediate physical deletion: breaks revision immutability.
- Consequences: Safe unreferenced-asset garbage collection is tracked separately as `JUDGE-REV-FND-D001`.

## Decision: PostgreSQL submission binding is the judge-definition authority

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-REVISION-BINDING-02
- Context: Redis currently carries only a submission ID, while mutable problem data could change before Worker execution.
- Decision: A new submission captures `ProblemJudgeRevisionId` once in PostgreSQL. Redis retains its existing submission-ID payload, and Worker resolves the immutable definition from the submission row.
- Rejected alternatives:
  - Copy the revision ID into a new Redis payload: duplicates authority and creates message/database mismatch handling.
  - Resolve `Problem.CurrentJudgeRevisionId` in Worker: allows later publication to change queued work.
- Consequences: Queue protocol stays compatible, and later JudgeJob/lease work can reuse the same binding.

## Decision: Legacy completed submissions are not assigned invented revisions

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-REVISION-BINDING-02
- Context: Historical submissions predate immutable revision snapshots, so their exact submission-time definition cannot be reconstructed.
- Decision: Completed legacy submissions keep a null revision binding. Pending legacy submissions bind to the migration-time current revision; migration fails if no valid current revision exists. Existing `Judging` submissions block migration because their actual request source may already have been read by an old Worker.
- Rejected alternatives:
  - Backfill every historical submission to revision 1: falsely claims historical provenance.
  - Bind already-Judging submissions to the current revision: may falsely describe work already constructed from mutable data.
  - Leave pending submissions unbound: forces Worker fallback or guaranteed system errors after deployment.
- Consequences: Historical truth is not fabricated, pending work gains a deterministic definition, and deployment must reconcile in-flight judging before migration.

## Decision: PostgreSQL JudgeJob is authoritative and Redis is only a wake-up hint

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-QUEUE-RELIABILITY-03
- Context: A destructive Redis list pop cannot prove durable ownership or recover work after a Worker or Redis failure.
- Decision: Submission creation persists a one-to-one `JudgeJob` in the same PostgreSQL transaction. Redis retains the submission-ID payload but only accelerates pickup; Workers always claim work from PostgreSQL and poll the database when Redis is empty or unavailable.
- Rejected alternatives:
  - Redis acknowledgement lists as the authority: still split submission and queue state across two systems and complicate atomic creation.
  - A transactional outbox plus separate dispatcher: valid but adds another durable state machine without improving the current submission-ID signal contract.
- Consequences: A failed Redis signal cannot lose work, duplicate signals are harmless, and PostgreSQL availability is now required for task pickup.

## Decision: Judge execution is at-least-once with fenced durable effects

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-QUEUE-RELIABILITY-03
- Context: Process termination can occur after claim or during Docker execution, so exactly-once execution cannot be guaranteed without an impractical distributed transaction.
- Decision: Workers acquire expiring leases with unique tokens, renew them while executing, and may re-execute after expiry. Result rows, submission terminal state, challenge/season scoring, and job completion commit atomically only when the same unexpired lease token still owns the job.
- Rejected alternatives:
  - Exactly-once code execution: cannot be guaranteed across PostgreSQL, the Worker process, and Docker.
  - Persist results without a final fence check: a late Worker could overwrite a newer attempt.
- Consequences: User code may run more than once, but stale attempts cannot produce durable result or scoring effects.

## Decision: Sandbox cleanup is scoped by submission ownership

- Status: Accepted
- Date: 2026-09-03
- Task: JUDGE-QUEUE-RELIABILITY-03
- Context: Global startup deletion of managed containers can interrupt valid work owned by another Worker.
- Decision: Every judge container carries a submission label. Startup reconciliation removes only exited/dead containers; a Worker that acquires a submission removes containers carrying that submission label before re-execution.
- Rejected alternatives:
  - Delete every managed container at Worker startup: unsafe with multiple Workers.
  - Leave all orphan cleanup to operators: leaks resources and can interfere with retries.
- Consequences: Concurrent Workers do not delete one another's active containers, while lease recovery cleans prior-attempt leftovers deterministically.

## Decision: .NET 10 is the repository-wide runtime baseline

- Status: Accepted
- Date: 2026-09-03
- Task: PLATFORM-BASELINE-04A
- Context: Host projects, EF tooling, and the C# sandbox must target one supported runtime before deeper sandbox and operations work.
- Decision: Pin SDK `10.0.400`; target `net10.0` in every project and generated C# submission project; align ASP.NET Core and EF Core packages/tools to `10.0.11`, Npgsql EF provider to `10.0.3`, and the C# sandbox to the .NET 10 SDK image.
- Rejected alternatives:
  - Upgrade only API/Worker: leaves test, launcher, and submitted C# compilation on mixed runtime contracts.
  - Defer the upgrade until after sandbox changes: forces 4B to validate two runtime baselines and increases rework risk.
- Consequences: .NET 10 restore/build/test, EF drift, release packaging, and Docker judge smoke are mandatory gates for subsequent stages.

## Decision: One repository verification entry point backs local and hosted CI

- Status: Accepted
- Date: 2026-09-03
- Task: PLATFORM-BASELINE-04A
- Context: Backend, frontend, and EF checks previously lived in separate ad hoc commands, while the production publisher did not run frontend lint/tests or EF model drift.
- Decision: Extend `scripts/e2e/run-all-checks.ps1` as the reusable fast verification entry point, invoke its scoped modes from GitHub Actions, and make the production publisher enforce the same frontend and EF gates.
- Rejected alternatives:
  - Duplicate all commands directly in CI YAML: local and hosted verification would drift.
  - Add hosted Docker judge CI without a confirmed Docker-capable runner: creates a permanently queued or misleading gate.
- Consequences: Push and pull-request CI cover backend and frontend fast gates; real Docker judge/security checks remain mandatory local gates until runner ownership is established.
