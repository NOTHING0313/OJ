# Task Review: RECRUITMENT-PRODUCTION-FRONTEND-04C-04E

## Status

`LOCAL_IMPLEMENTATION_COMPLETED_TARGET_HOST_GATES_PENDING`

The approved continuous package is complete for local code and versioned deployment assets. It deliberately stops before any remote write, DNS change, certificate issuance, production migration, SMTP delivery, or public release.

## Risk Classification

- Package risk: `R4` because it changes authentication semantics and defines deployment/network boundaries.
- 4C correction: `R2/R3`, explicitly authorized product-policy changes with compatibility preserved.
- 4D local assets: `R4` design surface, but no external system was changed.
- 4E frontend: `R2`, internal module/load behavior with no HTTP or persistence shape change.
- Cost ruling: targeted gates per stage, then one production publisher run as the single package-level full gate. No duplicate full scan or second full suite was run.

## Approved Contract

- Passwords use one 8-128 Unicode-code-point policy. Common/contextual rejection, NFC normalization, maximum length, PBKDF2 versioning and legacy rehash remain unchanged.
- `POST /api/auth/register` and `POST /api/auth/register/send-code` have no per-IP rate limiter. Other login, reset, submission, upload, team and administration limiters remain unchanged.
- Phone binding/recovery exits the first-party UI and frontend client surface. Existing backend routes, DTO fields, database columns and migrations remain intact for compatibility; the development SMS sender fails closed in Production.
- Production topology is one host: Nginx on 80/443, API on loopback, host systemd Worker, Docker PostgreSQL/Redis on loopback, host Docker sandbox, external secrets and persistent file roots.
- Frontend routes are lazy loaded under one fallback. The production build must keep initial static JavaScript below 1 MiB raw and 350 KiB gzip and must not include Monaco in the initial static graph.

## Stage Result Ledger

| Stage | Goal | Verification | State | Next entry satisfied |
|---|---|---|---|---|
| 4C-R | Align safeguards with the student-club recruitment environment | 29 targeted backend tests; frontend lint, 7 tests and typecheck | Completed | 4D local production assets |
| 4D | Add a recoverable single-host production contract without touching a real host | 4 deployment contract tests; Compose render; isolated PostgreSQL/Redis healthy with loopback-only bindings; Bash and Nginx syntax; production archive inclusion | Completed locally | Target-host deployment gates remain separate |
| 4E | Reduce PC initial load and restore disabled React correctness gates | ESLint zero findings; 7 frontend tests; production build; bundle-budget gate | Completed | Target-host/browser observation may follow |
| Package | Run one release-like complete repository gate | Publisher build/test/EF/Linux artifacts/frontend/EF bundle/archive passed; real Redis 8/8 passed separately | Completed locally | Remote deployment requires separate authority |

## Verification Matrix

| Check | Result | Evidence |
|---|---|---|
| Backend Release build | Passed | 0 warnings, 0 errors |
| Complete backend suite | Passed with explicit default skips | 1003 passed; 8 Redis integration tests were discover-time skipped by the default publisher environment |
| Real Redis integration | Passed | 8 / 8 with `ONLINEJUDGE_REDIS_INTEGRATION=1` against local Redis |
| 4C focused auth/rate-limit tests | Passed | 29 / 29 |
| 4D deployment contract tests | Passed | 4 / 4 |
| EF model drift | Passed | No model changes since the latest migration |
| Frontend lint | Passed | Hooks exhaustive dependencies and Fast Refresh boundaries enabled; zero errors/warnings |
| Frontend tests | Passed | 7 / 7 |
| Frontend production build | Passed with isolated-editor advisory | Initial JS 281.5 KiB raw / 89.4 KiB gzip; Monaco absent from initial graph; optional problem-detail Monaco chunk remains about 2.69 MiB |
| Production publisher | Passed | Linux x64 API/Worker, frontend, EF bundle, sandbox and deployment assets archived from commit `52731b7` |
| Release archive integrity | Passed | SHA-256 `5f8aaaf398f29d6c9a0c71f7c9275298bca4c186198a80c0a874220d91f76424` |
| Isolated infrastructure runtime | Passed | Dedicated containers returned PostgreSQL `SELECT 1` and Redis `PONG`; bindings were `127.0.0.1:55432` and `127.0.0.1:56379`; dedicated containers/network/volumes removed |
| Active development infrastructure binding | Passed | Existing PostgreSQL/Redis services were recreated without deleting the PostgreSQL volume; both probes passed and ports now bind only to `127.0.0.1` |
| Bash script syntax | Passed | Repository scripts parsed by `bash:5.2` container |
| Nginx syntax | Passed | TLS config passed `nginx -t` with an ephemeral verification certificate |
| Target-host TLS/systemd/SMTP/restore/sandbox/resource gates | NotRun | No remote deployment or production authority was granted |

## Public And Persistence Impact

- Semantic password validation changed from a 15-character minimum to 8; hash format and existing hashes are unchanged.
- Registration and registration-email-code endpoints no longer carry the `AuthRegister` IP-rate policy. No replacement IP limiter was added in API or Nginx.
- No HTTP route, DTO, entity, migration, Redis payload, JudgeJob state or sandbox result contract was removed or reshaped.
- New versioned operations assets define environment keys, host paths, Compose services, Nginx routes and systemd units; all secret values remain external placeholders.

## Diff Intent Table

| Area | Change type | Reason | In scope |
|---|---|---|---|
| Password/rate-limit/auth tests | Semantic correction | Match explicitly approved recruitment policy | Yes |
| Account frontend/API wrappers | UI removal with backend compatibility | Remove phone verification from the current product | Yes |
| `deploy/production` and publisher | Additive operations contract | Make topology, secrets, persistence, backup and restore reproducible | Yes |
| Development Compose bindings | Network-boundary correction | Keep the actively used local PostgreSQL/Redis ports on loopback as well | Yes |
| Frontend entry, hooks and helpers | Internal refactor | Route splitting and enabled correctness lint | Yes |
| Vite manifest/budget check | Build gate | Prevent initial-bundle regression and Monaco leakage | Yes |
| `output/` | Unchanged user artifact | Explicitly excluded | Yes |

## Deferred Governance Queue

- Public API entries: password minimum and registration rate semantics flushed to `PublicApiLedger.md`; backend phone compatibility recorded.
- Technical debt: disabled lint debt resolved; target-host validation, dormant SMS compatibility and optional Monaco chunk recorded.
- Decision entries: recruitment safeguards, single-host topology and route-load budget flushed to `DecisionLog.md`.
- Current project state: context capsule refreshed at this package stop point.
- Pending unrecorded items: none.

## Stop Rule Confirmation

The continuous package stops after 4E and the local package gate. No remote host, DNS, certificate authority, SMTP account, production data, Git remote or public endpoint was changed. The next safe task is an explicitly authorized target-host deployment rehearsal using the versioned assets and its still-pending gates.
