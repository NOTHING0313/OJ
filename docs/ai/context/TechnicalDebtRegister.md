# Technical Debt Register

| Debt ID | Area | File / concept | Reason accepted | Risk | Repayment trigger | Next action | Status |
|---|---|---|---|---|---|---|---|
| JUDGE-REV-FND-D001 | Judge asset retention | `ProblemJudgeAssetService` and judge-asset storage | Historical revisions must keep immutable asset content; safe reachability analysis is outside Stage 1 | Repeated asset replacement can increase disk use | Before production revision rollout and storage-capacity alerting | Add audited, idempotent garbage collection for soft-deleted assets with no revision references | Open |
| JUDGE-REV-BIND-D001 | Queue delivery and recovery | `SubmissionService`, `RedisJudgeQueue`, and `JudgeWorker` | Stage 2 preserved the existing submission-ID Redis contract pending the approved reliability stage | Historical risk: Redis could expose work before commit or lose popped work after failure | Repaid by `JUDGE-QUEUE-RELIABILITY-03` | PostgreSQL JudgeJob authority, fenced leases, bounded retry/dead-letter, and Redis-independent polling implemented and verified | Resolved 2026-09-03 |
