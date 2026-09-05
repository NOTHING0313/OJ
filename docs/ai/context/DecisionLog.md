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

## Decision: Judge runtime workspaces are immutable data planes

- Status: Accepted
- Date: 2026-09-03
- Task: SANDBOX-DATAPLANE-04B
- Context: Compilation needs a writable submission workspace, but testcase execution only needs compiled artifacts and input. Keeping the bind mount writable allowed submitted programs to consume host disk, while independent stdout/stderr limits allowed twice the configured capture budget.
- Decision: Keep the compile mount writable under a fixed file-size ceiling, mount the same workspace read-only for every testcase, confine permitted runtime writes to a size-bounded `/tmp`, and share one capture budget across stdout and stderr. Timeout, cancellation, or output overflow terminates the container before cleanup.
- Rejected alternatives:
  - Poll host workspace size: detection is not an enforceable quota and can lose a race against rapid disk exhaustion.
  - Use a size-limited Docker named volume: the local volume driver does not provide a portable size guarantee across the supported Docker environments.
  - Run compile and all testcases in one mutable container: creates cross-test state and weakens deterministic testcase isolation.
- Consequences: Runtime code cannot mutate host workspace data; temporary writes and individual compiler artifacts have hard limits; output is bounded by the configured combined budget without changing judge status or persistence contracts.

## Decision: Function entry-point preflight ignores non-code text

- Status: Accepted
- Date: 2026-09-03
- Task: SANDBOX-DATAPLANE-04B
- Context: Regex matching directly against submitted source rejected valid function solutions when `main`, `Main`, or `class Program` appeared only in comments or string/character literals.
- Decision: Mask comments and supported literal forms before applying the existing entry-point patterns, using one internal guard shared by C11, C++17, and C# runners.
- Rejected alternatives:
  - Remove preflight entirely: loses the existing fast, friendly function-mode error contract.
  - Add language parser dependencies: disproportionate for a narrow preflight and introduces new toolchain coupling.
- Consequences: Real entry points are still rejected before Docker, while harmless explanatory text no longer causes false compile errors.

## Decision: Verification-code state transitions are atomic in Redis

- Status: Accepted
- Date: 2026-09-03
- Task: IDENTITY-SESSION-EXPORT-04C
- Context: Separate read/increment/write operations allowed concurrent requests to bypass cooldown, attempt, daily, or single-consumption guarantees.
- Decision: One shared Redis store uses Lua scripts for issuance and consumption, hashes codes, preserves original TTL on wrong attempts, and cleans only a matching issuance after delivery failure.
- Rejected alternatives:
  - Process-local locks: do not coordinate multiple API instances.
  - Database-backed verification rows: introduce durable schema and cleanup work for short-lived state already owned by Redis.
- Consequences: Verification fails closed when Redis is unavailable; the current scripts assume a single Redis deployment rather than arbitrary Redis Cluster slots.

## Decision: Password policy and hash evolution are versioned and shared

- Status: Superseded by `Recruitment identity safeguards match the operating environment`
- Date: 2026-09-03
- Task: IDENTITY-SESSION-EXPORT-04C
- Context: Registration and reset paths used inconsistent short minimums, and persisted hashes had no current algorithm version or automatic upgrade path.
- Decision: Use one 15-128 Unicode-code-point policy with common/contextual-password rejection. New hashes use `v2` PBKDF2-SHA256 at 600,000 iterations with NFC normalization; successful legacy `v1` login upgrades the hash in the same save.
- Rejected alternatives:
  - Force all users to reset immediately: unnecessary disruption when legacy verification remains safe.
  - Validate contextual password weakness before reset-code validation: leaks whether the supplied account exists.
- Consequences: Correct reset codes may be consumed before a context-specific weak-password error; this is accepted to preserve account-enumeration resistance.

## Decision: Recruitment identity safeguards match the operating environment

- Status: Accepted
- Date: 2026-09-03
- Task: RECRUITMENT-PRODUCTION-FRONTEND-04C-04E / 4C-R
- Context: The platform is a student-club recruitment system used mainly from stable dormitory PC networks. A 15-character minimum and five registrations per shared IP per ten minutes would create more normal-user friction than useful protection, and phone verification is not part of the current product.
- Decision: Keep the shared Unicode/common/context password policy and versioned hashes but set the minimum to 8. Remove the registration IP limiter without an API or Nginx replacement. Remove phone verification from the first-party UI while retaining backend routes and persisted fields for compatibility.
- Rejected alternatives:
  - Keep the 15-character minimum: exceeds the explicitly approved account requirement.
  - Retain or relocate the five-per-IP registration limit: shared NAT/dorm traffic can block legitimate recruitment and the user explicitly rejected it.
  - Delete phone routes and schema immediately: creates an unnecessary breaking API and persistence migration.
- Consequences: Email-code cooldown/daily/attempt controls and all non-registration limiters remain; dormant phone calls fail closed in Production; permanent phone-contract removal requires a separately approved migration.

## Decision: Production uses host services around loopback-only state containers

- Status: Accepted; target host verified 2026-09-04
- Date: 2026-09-03
- Task: RECRUITMENT-PRODUCTION-FRONTEND-04C-04E / 4D
- Context: The current Worker invokes the host Docker CLI and its sandbox relies on host-visible bind mounts. Containerizing the Worker would add Docker-socket and path-translation complexity without product benefit on the approved 2C4G single host.
- Decision: Nginx terminates TLS and serves the SPA; API and Worker run as separate systemd users; PostgreSQL and Redis run in Docker with loopback-only published ports; only the Worker joins the Docker group; persistent data and secrets live outside immutable releases.
- Rejected alternatives:
  - Containerize API/Worker and mount the Docker socket: increases privilege and workspace-path complexity.
  - Publish PostgreSQL/Redis publicly: violates the approved network contract.
  - Treat Redis as backup authority: JudgeJob truth already belongs to PostgreSQL and Redis state is transient.
- Consequences: Worker Docker-group membership remains root-equivalent and tightly scoped; target-host TLS, permissions, restore and resource gates are mandatory before launch.

## Decision: Page routes are lazy and initial payload is a blocking build contract

- Status: Accepted
- Date: 2026-09-03
- Task: RECRUITMENT-PRODUCTION-FRONTEND-04C-04E / 4E
- Context: The eager frontend entry produced an approximately 3.45 MiB main chunk and loaded Monaco-related code before ordinary pages needed it.
- Decision: Load every page route through `React.lazy` under one fallback and emit a Vite manifest. Production build fails if initial static JavaScript exceeds 1 MiB raw or 350 KiB gzip, or if the initial graph contains Monaco.
- Rejected alternatives:
  - Introduce another router/state framework: unnecessary for module splitting.
  - Replace Monaco solely to remove its optional-route warning: disproportionate for the approved stable-PC environment.
  - Raise the initial budget to hide regressions: would remove the executable acceptance contract.
- Consequences: Initial JavaScript is 281.5 KiB raw / 89.4 KiB gzip; the code-editor route retains an explicit optional Monaco payload debt.

## Decision: First-party browsers use cookie transport over existing JWT authority

- Status: Accepted
- Date: 2026-09-03
- Task: IDENTITY-SESSION-EXPORT-04C
- Context: The frontend persisted bearer credentials in Web Storage, while external/API compatibility and the existing single-active-session JWT authority had to remain intact.
- Decision: Keep `/api/auth/login` unchanged for Bearer clients and add `/api/auth/session` for a Secure, HttpOnly, SameSite=Lax host-only cookie. Unsafe cookie-authenticated requests require antiforgery tokens; explicit Bearer authorization remains exempt and takes precedence.
- Rejected alternatives:
  - Replace JWTs with a new server-side session model: duplicates current session authority and expands persistence scope.
  - Expose the cookie over HTTP in development/production: weakens the production contract and hides missing TLS configuration.
- Consequences: The browser starts through `/api/auth/me`, credentials no longer enter Web Storage, and production deployment must terminate HTTPS correctly.

## Decision: Production origin and brand assets use the verified public host

- Status: Accepted; target host verified 2026-09-04
- Date: 2026-09-04
- Task: OJ-PRODUCTION-DEPLOY-01
- Context: The deployed site is reached at `unrealstudiooj.top`, while repository deployment assets still named an obsolete domain. The live logo request returned the SPA `index.html`, which also prevented the login particle canvas from initializing.
- Decision: Treat `unrealstudiooj.top` as the production origin and certificate name. Serve `/brand/` as a strict static-file location that returns 404 instead of the SPA fallback, and fail publishing or host verification when the login logo is missing, empty, or not served as `image/png`.
- Rejected alternatives:
  - Keep the obsolete domain in repository templates and patch each server manually: guarantees configuration drift on later releases.
  - Let missing brand files fall through to `index.html`: hides packaging failures behind a misleading 200 HTML response.
  - Copy the logo into the API web root: creates a second owner for a frontend asset.
- Consequences: Certificate issuance and DNS must target `unrealstudiooj.top`; release artifacts retain the frontend as the only brand-asset owner; target-host verification now detects the observed failure before deployment is accepted.

## Decision: The www hostname is a certificate-covered redirect alias

- Status: Accepted; target host verified 2026-09-04
- Date: 2026-09-04
- Task: OJ-PRODUCTION-DEPLOY-01
- Context: Both `unrealstudiooj.top` and `www.unrealstudiooj.top` resolve to the production host, while the root hostname remains the intended public origin. Omitting `www` from TLS would leave a reachable hostname with a certificate or routing mismatch.
- Decision: Issue one certificate containing both hostnames. Serve application content only from `https://unrealstudiooj.top`; redirect HTTP requests for either hostname and HTTPS requests for `www.unrealstudiooj.top` directly to the canonical HTTPS origin while preserving the request URI.
- Rejected alternatives:
  - Serve the application independently on both hostnames: creates duplicate origins and inconsistent cookie, cache and indexing behavior.
  - Delete the existing `www` DNS record: breaks a conventional entry point that already resolves to the host.
  - Redirect HTTP `www` to HTTPS `www` before canonicalizing: adds an unnecessary second redirect.
- Consequences: Certificate issuance and renewal must retain both SANs; target-host verification must prove the 301 status and exact canonical location before deployment is accepted.

## Decision: Production state volumes are explicit external resources

- Status: Accepted
- Date: 2026-09-04
- Task: OJ-PRODUCTION-DEPLOY-01
- Context: The existing host stores PostgreSQL and Redis data in volumes created by a legacy Compose project. A new Compose project name or volume key would otherwise create different empty volumes while appearing to start successfully.
- Decision: Production Compose requires explicit `POSTGRES_VOLUME_NAME` and `REDIS_VOLUME_NAME` values and mounts both volumes with `external: true`. Operators create volumes for a first installation or select verified existing volumes during adoption; Compose never implicitly creates production state volumes.
- Rejected alternatives:
  - Keep project-scoped implicit volumes: a project rename can silently select an empty database.
  - Add a host-specific override only on the current server: creates undocumented drift from the release artifact.
  - Permanently retain the legacy Compose project: leaves infrastructure outside the versioned production contract.
- Consequences: Missing or misspelled volumes fail startup before PostgreSQL or Redis is replaced. First installation requires an explicit volume-creation step, and legacy adoption requires a verified backup plus a controlled container handoff without `--volumes`.

## Decision: CSV exports classify trusted and untrusted cells explicitly

- Status: Accepted
- Date: 2026-09-03
- Task: IDENTITY-SESSION-EXPORT-04C
- Context: RFC CSV quoting alone does not prevent spreadsheet applications from evaluating attacker-controlled cells as formulas.
- Decision: Route challenge exports through one writer. Neutralize formula-like untrusted text before CSV quoting; preserve explicitly trusted numbers, dates, enums, and identifiers.
- Rejected alternatives:
  - Prefix every cell: corrupts trusted numeric/date typing and identifiers.
  - Rely only on quotes: spreadsheet programs may still evaluate quoted formula cells.
- Consequences: Existing columns and order remain stable, while user-controlled text opens as inert content.

## Decision: Production TLS runtime paths are certificate-provider neutral

- Status: Accepted; target host verified 2026-09-04
- Date: 2026-09-04
- Task: OJ-PRODUCTION-DEPLOY-01
- Context: The production host cannot complete TLS handshakes to the Let's Encrypt ACME endpoint over IPv4 and has no IPv6 default route. The approved fallback is an Alibaba Cloud formal multi-domain certificate, while the checked-in Nginx template previously hard-coded Let's Encrypt live paths.
- Decision: Nginx reads the complete chain and private key from `/etc/onlinejudge/tls/fullchain.pem` and `/etc/onlinejudge/tls/privkey.pem` regardless of issuer. Generate the RSA private key and CSR on the production host, request both public hostnames as SANs, and keep all private key material outside releases and source control.
- Rejected alternatives:
  - Store an Alibaba-issued certificate under `/etc/letsencrypt`: misrepresents ownership and couples renewal to the wrong provider.
  - Generate or transfer the private key through a workstation or chat: unnecessarily expands secret exposure.
  - Pin the current Let's Encrypt edge address or merely force IPv4: the edge is not a stable allow-list target and the observed IPv4 TLS handshake also times out.
- Consequences: Certificate-provider changes no longer require an Nginx template change. Operators must validate SAN coverage, chain integrity, key matching, expiry and Nginx reload whenever the stable files are replaced; automated renewal remains provider-specific host configuration.

## Decision: Judge Worker parallelism is bounded inside one host process

- Status: Accepted
- Date: 2026-09-05
- Task: JUDGE-RESOURCE-INPUT-BOUNDS-05 / bounded parallelism
- Context: PostgreSQL fenced leases already support multiple claimers, while a single consumer made ten equal submissions drain in about 21.6 seconds. The user accepted localhost Docker simulation and limited the capacity gate to proving two-way CPU scheduling and bounded memory occupancy.
- Decision: One Worker process may run one or two asynchronous consumer loops. Each loop owns a distinct worker identity and creates an independent scope for claims and processing. Base, Development and the checked-in production environment example default to two. Startup rejects values outside 1-2.
- Rejected alternatives:
  - Launch two systemd Worker processes immediately: duplicates runtime memory and complicates lifecycle management on the small host.
  - Remove the upper bound or derive it from CPU count: could overcommit Docker memory and destabilize the API/database host.
  - Raise the default above two from localhost results: no evidence supports more simultaneous sandbox workloads on the intended small host.
- Consequences: Local ten-submission bursts drained in 12.4 seconds and 11.9 seconds across two runs, and a fresh twenty-submission burst completed without retries or dead letters while reaching two simultaneous leases. A sandbox-equivalent two-container probe measured 200.16% aggregate Docker CPU and 103.84 MiB aggregate memory with no OOM. Redis remains a best-effort signal; PostgreSQL `SKIP LOCKED` leases and fencing remain the authority. Windows per-case cgroup telemetry is still unavailable, and remote deployment was not performed.

## Decision: Choice content is revisioned while answer reveal policy remains editable

- Status: Accepted; implementation authorized
- Date: 2026-09-05
- Task: CHOICE-PROBLEM-PLAN-06
- Context: Authors must be able to query, add, edit, delete and reorder choice questions, options and answers both before and after publication. They must also be able to change answer reveal policy after publication, while scheduled reveal remains uniform for old and new submissions.
- Decision: Draft choice content remains fully editable in every authoring state. Saving valid content changes on a published problem creates one immutable judge revision; historical submissions keep their bound answer revision. Reveal policy and reveal time are mutable problem-level publication settings, audited separately and applied at read time across submissions from every revision. Disclosure is monotonic: after scheduled reveal or any disclosure under the post-submission policy, policy edits may not hide answers again.
- Rejected alternatives:
  - Lock questions or reveal policy after first publication: conflicts with the confirmed authoring workflow.
  - Mutate an existing published revision: changes historical judge truth and breaks submission reproducibility.
  - Snapshot reveal time independently into every revision: post-publication edits would cause old and new submissions to reveal at different times.
  - Expose one write endpoint per question and option while the problem is live: makes transient invalid public states and revision storms much harder to prevent.
- Consequences: The editor can offer complete CRUD before publication, after publication and after unpublishing, but persists child edits as one aggregate transaction. Correct answers remain revision-stable; reveal access can change after publication until disclosure, after which attempts to re-hide return `409 answers_already_revealed`.

## Decision: All problem kinds share one versioned authoring lifecycle

- Status: Accepted; implementation authorized
- Date: 2026-09-05
- Task: PROBLEM-AUTHORING-PARITY-07 / CHOICE-PROBLEM-PLAN-06
- Context: Standard-input/output and function problems already permit most edits after publication, but independent metadata, test-case and asset operations can create revision storms, cannot persist explicit test order and cannot detect stale author tabs. Choice problems require aggregate CRUD before and after publication and should not introduce a separate lifecycle.
- Decision: Add one problem-level authoring version, one capability projection and one aggregate authoring service used by all problem kinds. Published judge-affecting changes create at most one immutable revision per aggregate save; presentation-only or choice reveal-policy changes update authoring state without a judge revision. Existing endpoints remain compatibility adapters during frontend migration.
- Rejected alternatives:
  - Build a choice-only editor lifecycle: duplicates conflict, permission and publication rules and leaves current problem kinds behind.
  - Mutate published revisions in place: breaks historical submission reproducibility.
  - Keep timestamp/ID ordering for tests: cannot represent explicit author intent reliably.
  - Remove existing mutation endpoints immediately: creates an unnecessary breaking change for current callers.
- Consequences: The implementation requires an explicit persistence migration, additive authoring DTO/API, HTTP 409 contract, frontend migration and PostgreSQL concurrency tests. The authoring request is an 8 MiB incremental aggregate; 64 MiB test imports remain separate. Legacy mutation adapters may be removed after one stable frontend release and 14 consecutive zero-call days.

## Decision: Choice submissions share season base scoring but never create performance candidates

- Status: Accepted; implemented locally
- Date: 2026-09-05
- Task: CHOICE-PROBLEM-PLAN-06
- Context: Choice submissions belong in the common submission history and current season, but have no judge language, runtime, or peak-memory measurements that can be compared with code submissions.
- Decision: A full-score choice submission calls the existing season score service in the same database transaction and may earn the configured base score and first-completion time. It stores no best-performance submission, language, runtime, or memory candidate. Season administration omits choice problems from benchmark configuration, and the benchmark endpoint rejects them.
- Rejected alternatives:
  - Give choice submissions a placeholder language and zero resource use: this would make them appear to outperform real code submissions.
  - Exclude choice submissions from seasons entirely: this conflicts with the frozen product contract.
  - Add a second choice-only leaderboard: this would duplicate identity, freeze, archive and ranking rules.
- Consequences: `SeasonSubmissionResult.Language` is nullable and carries `SubmissionKind`; existing code-submission constructors retain compatibility. Choice problems still participate in base score and first-full-score ordering only.


## Decision: Require login before reading or answering questions

- Status: Accepted; explicitly requested by user on 2026-09-05.
- Decision: Use existing API authorization and frontend ProtectedRoute for problem list/detail and challenge detail/task views. Challenge detail is protected because its response embeds task statements. Overview and ranking data remain public.
- Consequences: Remove guest draft creation and login handoff; retain account-scoped drafts, choice revision isolation, retry and answer-reveal refresh. Previously stored guest/unowned drafts are neither imported nor deleted.
- Rejected alternative: Repair guest-to-account draft merging; guest participation is outside the newly approved product scope. Frontend-only hiding would leave direct API access open.
- Compatibility: Anonymous callers must authenticate. No database migration, DTO shape change, new authentication mechanism, or judging/scoring change.

## Decision: Complete choice problems can be created and published together

- Status: Accepted; explicitly requested by user on 2026-09-05; implemented and locally verified.
- Decision: Accept `IsPublished=true` when creating a complete choice set. Reuse the existing completeness validator and immutable revision publisher, with initial persistence and publication in one relational transaction. Programming problems still require saved test cases before publication.
- UI: Keep editing in the management list/editor, remove the choice player edit shortcut, and align each input, letter and Markdown first line in one grid row.
- Compatibility: No endpoint, DTO or database schema change. Creation semantics are extended only for complete choice sets.
- Verification: Focused choice/revision tests and a real local PostgreSQL browser flow from creation through publication and scored submission. See `docs/visual/CHOICE-UI-AND-SITE-AUDIT-20260905.md` for coverage limits.

## Decision: Suspend the personal season record page

- Status: Suspended by explicit user request on 2026-09-05; do not restore without confirmed product requirements.
- Context: The audit incorrectly called `/account/competition` “参赛资料”; its actual page title is “赛季战绩”. The user does not recognize this requirement. Its historical authorization was not established in this task.
- Decision: Hide the leaderboard's personal record entry and redirect the old URL to the protected personal profile. Retain the dormant component and backend/data unchanged for possible future review.
- Scope: Frontend availability only; no permission expansion, API removal, data deletion or change to other leaderboard features.
