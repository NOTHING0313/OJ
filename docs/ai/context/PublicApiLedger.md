# Public API And Persistence Ledger

| Task/Stage | API or contract | Kind | Reason | Expected next use | Stability | Tests / notes |
|---|---|---|---|---|---|---|
| JUDGE-REVISION-FOUNDATION-01 / Stage 1 | `ProblemJudgeRevision`, `ProblemJudgeRevisionTestCase`, `ProblemJudgeRevisionAsset` | Domain and persisted model | Represent an immutable judge definition separately from mutable authoring rows | Submission binding and Worker revision loading in Stage 2 | Experimental until Stage 2 | `ProblemJudgeRevisionTests`; PostgreSQL upgrade verification |
| JUDGE-REVISION-FOUNDATION-01 / Stage 1 | `Problem.CurrentJudgeRevisionId` | Persisted authority pointer | Select the judge definition for future submissions | Submission creation transaction in Stage 2 | Experimental until Stage 2 | Publication, edit, unpublish/republish tests |
| JUDGE-REVISION-FOUNDATION-01 / Stage 1 | `ProblemJudgeAsset.IsDeleted` / `DeletedAt` | Persisted lifecycle fields | Preserve files referenced by historical revisions | Revision-aware asset loading and later safe garbage collection | Stable | Asset revision and retention tests |
| JUDGE-REVISION-FOUNDATION-01 / Stage 1 | Problem create/publish and last-test-case deletion behavior | Semantic API contract; signatures unchanged | Enforce that every published revision has at least one valid active test case | Submission revision binding | Stable | Creation, publication, deletion, incompatible-mode tests |
| JUDGE-REVISION-BINDING-02 / Stage 2 | `Submission.ProblemJudgeRevisionId` / `ProblemJudgeRevision` | Domain and persisted binding | Freeze the authoritative judge definition selected when a submission is created | JudgeJob and retry processing | Stable | Submission binding, authority, and immutability tests; PostgreSQL migration verification |
| JUDGE-REVISION-BINDING-02 / Stage 2 | `IJudgeCompileAssetLoader.LoadRevisionAsync` | Application service method | Load exactly the assets captured by a revision, including retained soft-deleted assets | Worker retries and future JudgeJob processors | Stable | Historical revision asset-loading test |
| JUDGE-REVISION-BINDING-02 / Stage 2 | `SubmissionJudgeRequestFactory.Create` | Infrastructure runtime factory | Keep revision-to-runner mapping canonical and independently testable outside the Worker loop | Durable JudgeJob processor | Stable | `SubmissionJudgeRequestFactoryTests` |
| JUDGE-REVISION-BINDING-02 / Stage 2 | Submission creation and Worker definition selection | Semantic API/runtime contract; HTTP and Redis shapes unchanged | Prevent mutable authoring edits from changing queued judging | Durable JudgeJob processing | Stable | Worker source contract; submission revision tests |
| JUDGE-QUEUE-RELIABILITY-03 / Stage 3 | `JudgeJob`, `JudgeJobStatus`, `Submission.JudgeJob` | Domain and persisted model | Make judge work durable and independently recoverable from Redis/Worker failures | Operations tooling and dead-letter administration | Stable | Store tests; PostgreSQL migration/backfill; multi-Worker and recovery runs |
| JUDGE-QUEUE-RELIABILITY-03 / Stage 3 | `IJudgeJobStore` and lease result models | Application service contract | Centralize atomic claim, renewal, retry, dead-letter, and fenced transition semantics | Worker scaling and future operator tooling | Stable | `JudgeJobStoreTests`; live PostgreSQL `SKIP LOCKED` execution |
| JUDGE-QUEUE-RELIABILITY-03 / Stage 3 | `IJudgeQueue` best-effort methods and `JudgeQueueReadResult` | Application integration contract | Define Redis as a non-authoritative wake-up optimization that can report unavailability | Alternative signal transports without changing durable ownership | Stable | Redis-down runtime verification; submission queue-failure test |
| JUDGE-QUEUE-RELIABILITY-03 / Stage 3 | `JudgeResult.FailureKind` and `JudgeFailureKind` | Application/domain result contract | Separate retryable infrastructure failures from permanent judge configuration failures | Additional runners and operational reporting | Stable | Runner and processor classification tests |
| JUDGE-QUEUE-RELIABILITY-03 / Stage 3 | `IJudgeSandboxMaintenance.ReconcileSubmissionContainersAsync` | Application infrastructure contract | Remove only containers belonging to a reclaimed submission | Lease recovery across multiple Workers | Stable | Sandbox security contract test; forced process-termination run |
| IDENTITY-SESSION-EXPORT-04C / Stage 4C | `POST /api/auth/session` and browser session cookies | HTTP and browser authentication contract | Keep JWT authority while moving the first-party browser away from Web Storage credentials | HTTPS production frontend and later session UX work | Stable | Secure, HttpOnly, SameSite=Lax, host-only session cookie; cookie `/me` and logout live verification |
| IDENTITY-SESSION-EXPORT-04C / Stage 4C | Cookie-authenticated unsafe requests require `X-CSRF-TOKEN` | HTTP security semantic contract | Prevent cross-site state changes without altering Bearer API clients | All current and future unsafe browser API calls | Stable | Antiforgery middleware tests; Authorization header has precedence and Bearer requests are exempt |
| RECRUITMENT-PRODUCTION-FRONTEND-04C-04E / 4C-R | Shared 8-128 Unicode password policy and versioned `v2` PBKDF2 hashes | Validation and persisted-value contract | Apply the explicitly approved recruitment-platform minimum to every password creation path while retaining weakness checks and hash migration | Future hash-cost upgrades and password UI | Stable | 7/8/128 Unicode boundaries; 600,000-iteration v2; NFC normalization; v1 verify-and-rehash tests |
| RECRUITMENT-PRODUCTION-FRONTEND-04C-04E / 4C-R | Registration and registration-email-code endpoints have no per-IP rate policy | HTTP semantic contract; signatures unchanged | Avoid blocking normal club recruitment from shared dormitory/NAT networks | Current recruitment workflow | Stable for current product profile | Reflection and repeated same-IP acquisition tests; login/reset/other limiters remain |
| RECRUITMENT-PRODUCTION-FRONTEND-04C-04E / 4C-R | Phone verification backend routes and account fields retained without first-party frontend entry | Compatibility HTTP/persistence contract | Remove an unused product feature without a breaking API/schema migration | Existing external callers, if any; future explicit removal review | Stable compatibility surface | Frontend references removed; Production development sender fails closed; backend tests retained |
| IDENTITY-SESSION-EXPORT-04C / Stage 4C | Atomic Redis verification-code lifecycle | Transient integration contract | Prevent concurrent issuance, attempt, and consumption races | Email and SMS verification flows | Stable for single Redis | 50-way concurrency, TTL, daily-limit, failure-cleanup tests; CI uses real Redis |
| IDENTITY-SESSION-EXPORT-04C / Stage 4C | Challenge CSV cell classification and formula neutralization | Export semantic contract; shape unchanged | Prevent spreadsheet formula execution from user-controlled text | Existing challenge result/review downloads | Stable | Malicious formula-prefix tests; numeric/date/GUID cells preserved |
| JUDGE-RESOURCE-INPUT-BOUNDS-05 | `SubmissionEvaluationDto` and `SubmissionDto.Evaluation` / `SubmissionListItemDto.Evaluation` | Additive DTO and JSON response contract | Expose maximum case time, average case time, maximum case peak memory, and average case peak memory without changing legacy aggregate fields | Submission detail, user/admin lists, and capacity reports | Stable | `SubmissionEvaluationMetricsTests`; legacy `TimeUsedMs` remains cumulative and `MemoryUsedKb` remains maximum peak |
| JUDGE-RESOURCE-INPUT-BOUNDS-05 | `JudgeResourcePolicy` and `SubmissionJudgeRequestFactory.Create(..., JudgeResourcePolicy)` | Configuration and runtime contract | Share configurable UTF-8 payload, time, memory, test-count, aggregate-data and judge wall-time bounds across API and Worker | Problem authoring, submission ingress, immutable revision execution and capacity tuning | Stable | `JudgeResourcePolicyTests`; `JudgeJobProcessorTests`; two-argument factory overload retained for compatibility |
| JUDGE-RESOURCE-INPUT-BOUNDS-05 / bounded parallelism | `JudgeWorker:Concurrency` | Worker configuration contract | Permit measured in-process parallel judging without changing PostgreSQL lease authority or adding another service process | Bounded local and production parallel judging | Stable; valid range 1-2 | `JudgeWorkerOptionsTests`; localhost one-versus-two consumer bursts; fresh twenty-job integrity run; two-container CPU/memory probe; checked-in defaults are 2 |
| CHOICE-PROBLEM-PLAN-06 | `ProblemKind`, choice draft/snapshot entities, `SubmissionKind`, normalized choice results/selections, nullable programming-only fields | Domain and persisted model | Add multi-question single/multiple-choice problems without overloading code judge modes or the Worker pipeline | Choice authoring, immutable publication, historical result display | Stable; locally PostgreSQL-verified | Migration `20260904212001_AddChoiceProblemsAndAuthoringVersion`; PostgreSQL 16 empty-database up/down/up, column semantics and choice constraints pass |
| CHOICE-PROBLEM-PLAN-06 | `GET/PUT /api/problems/{id}/authoring`, `AuthoringVersion`, `409 authoring_version_conflict`, `409 answers_already_revealed` | HTTP and authoring concurrency contract | Provide aggregate CRUD before/after publication and reject stale or disclosure-reversing saves | First-party problem editor and later programming-authoring parity | Stable; locally PostgreSQL-verified | 8 MiB request limit; two independent PostgreSQL contexts with one expected version produce one success and one conflict; reorder uniqueness passes |
| CHOICE-PROBLEM-PLAN-06 | `POST /api/choice-submissions` and additive choice fields on problem/submission/profile/season DTOs | HTTP response/request contract | Submit exact option sets synchronously, reveal answers by policy, preserve one submission history, and award season base score without performance metrics | Public choice player, submission history, profile and season UI | Stable; locally verified | 64 KiB request limit; no `JudgeJob`; answer-redaction, scoring, revision-conflict, season and PostgreSQL-backed authoring gates pass |

No existing network DTO shape was added or removed in Stages 1-4C. Stage 4C added one endpoint that reuses existing request/user DTOs.


### 2026-09-05 — Authenticated problem access

| API | Change | Reason / next use | Compatibility | Verification |
|---|---|---|---|---|
| `GET /api/problems`, `GET /api/problems/{id}`, `GET /api/challenges/{id}` | Require existing authentication; anonymous HTTP clients receive 401 | User explicitly requires login before reading or answering questions; challenge detail embeds task statements | Intentional anonymous-access restriction; DTO and storage shapes unchanged; authenticated roles and embargo rules preserved | `ProblemAccessTests`, `CurrentRoleAuthorizationTests`, `ContentEmbargoTests` |

Frontend guards the corresponding problem/challenge-detail/task routes before mounting. Challenge overview and leaderboard APIs remain public. Existing programming and choice submission authentication remains required.

### 2026-09-05 — Direct choice publication on creation

| API | Change | Compatibility | Verification |
|---|---|---|---|
| `POST /api/problems` | Accept existing `IsPublished=true` for a complete choice set; validate before persistence and create its immutable revision in the same relational transaction | Additive request semantics; unchanged DTO/schema/authorization; programming creation still saves a draft first | `ChoiceProblemTests`, `ProblemJudgeRevisionTests`, local PostgreSQL browser create/publish/submit flow |

### 2026-09-05 — Private season results / Root audit

| API / contract | Change and reason | Compatibility / verification |
|---|---|---|
| `GET /api/leaderboards/season/current`, `current/problems/{problemId}` | Full standings require Root; no anonymous or Answerer access to other participants' season scores | Intentional authorization restriction; `SeasonResultAccessTests` and service tests |
| `GET /api/leaderboard-seasons/history`, `history/{seasonId}`; admin aliases `GET /api/admin/leaderboard-seasons/current/leaderboard`, `history`, `history/{seasonId}` | Root-only current and archived full results | Removes prior ProblemSetter audit access; guards in controller and service |
| `GET /api/leaderboard-seasons/current/me`, `me/history` | Signed-in active Answerer reads only their own data; history no longer has a contradictory ProblemSetter controller policy | Existing shapes preserved apart from additive identity fields below; no target identity accepted |
| `GET /api/admin/leaderboard-seasons/users/{userId}/current`, `users/{userId}/history` | New Root-only personal audit endpoints; reuse self-query core and DTOs; unknown user returns 404 | Adds `GetUserCurrentPersonalAsync` and `GetUserPersonalHistoryAsync` to the existing service interface; no new service or storage |
| `LeaderboardSeasonPersonalDto.UserId`, `.UserName` | Add current identity so the Root detail screen identifies the queried user, including empty seasons | Additive response fields; self projection only exposes its caller; identity/isolation assertions |

Season summary/configuration responses carry no participant results and keep existing consumers working. Challenge rankings and the legacy challenge aggregates at `/api/leaderboards/users` and `/api/leaderboards/users/history` are not season-results endpoints and are unchanged. No persisted shape change.


### 2026-09-05：功能可靠性查询增量
- 新增 `GET /api/problems/query?keyword=&page=1&pageSize=20`，返回既有 `PagedResult<ProblemListItemDto>`；默认 20、上限 100，越界页归入有效范围。保持登录要求与 ContentVisibilityPolicy，旧 `/api/problems` 数组接口保留供既有选题调用。
- `SubmissionQueryRequest` 新增可选 `submissionKind=1|2`，分页前过滤，非法枚举由模型验证拒绝；原用户可见性与现有字段不变。
- 本地出题草稿 `authoring-v1`：按账号、题目/新建隔离；schema=1，保存基准 authoringVersion 及全部可编辑字段。缓存不具备服务器写入权，旧版本只允许下载/丢弃。
- 无数据库迁移。新增查询测试覆盖可见性、搜索、页界、题型与用户隔离。


### 2026-09-05：统一文件与网络错误
- 前端新增 `requestFile(path, options)` 返回 `{ blob, headers }`，与 JSON 请求共用状态解析和 AUTH_* 会话失效处理；支持取消信号与认证处理抑制。
- 测试点导出、挑战用户/任务 CSV、文件作业 ZIP、主题 ZIP 全部接入该路径；不改变后端下载协议和文件名规则。
- 网络/响应体中断使用 `ApiError(status=0, errorCode=NETWORK_ERROR)`，主动取消保留原异常；成功响应的无效 JSON 使用 `INVALID_RESPONSE`。HTTP 状态及中文业务原因保留。
- 单元测试覆盖 5 个具体下载调用及断网、流中断、取消、401 和 429；无数据库变更。

### 2026-09-05：题库难度分级
- `ProblemDifficulty`：0 未分级、1 简单、2 中等、3 困难；题目列表与详情 DTO 增加 `difficulty`，创建请求缺省为 0，更新请求省略/null 保留现值、显式 0 清除分级。非法值被 API/服务验证拒绝。
- 难度沿用题目元数据编辑权限、并发版本与审计，仅修改难度不增加判题版本。旧调用方省略字段不会清除已有分级。
- 持久化增加 `Problems.Difficulty` 整型列，默认 0，数据库约束 0..3；迁移 `20260905125143_AddProblemDifficulty` 已在本地应用。
- `authoring-v1` 草稿增加 difficulty，兼容旧草稿缺失值为 0。验证证据见 `docs/visual/PROBLEM-DIFFICULTY-20260905.md`。
