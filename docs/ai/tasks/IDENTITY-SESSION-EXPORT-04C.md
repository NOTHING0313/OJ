# Task Review: IDENTITY-SESSION-EXPORT-04C

## Status

`COMPLETED_WITH_DECLARED_NOTRUN`

Stage 4C implementation and all executable code gates passed. A real Excel/LibreOffice import check was not run because Office automation is unavailable and the approved LibreOffice installation/download paths were blocked by the host environment. This is recorded as verification debt rather than reported as a pass.

## Risk Classification

- Initial and final risk: `R4`, because the stage changes authentication transport, password persistence, verification-code concurrency, and exported spreadsheet content.
- Governance mode: staged execution with targeted gates, one complete repository gate, independent read-only security review, and post-review focused regression.
- Cost ruling: do not repeat the complete repository suite after narrow review fixes when focused tests cover every changed path.

## Contract

- Make email and SMS verification issuance, cooldown, daily limits, failed attempts, and single consumption atomic in Redis.
- Fail closed when development-only senders are selected outside Development; clean only the matching issuance after a sender failure.
- Apply one 15-128 Unicode-code-point password policy to registration, phone reset, email reset, and root bootstrap.
- Emit versioned `v2` PBKDF2-SHA256 hashes with 600,000 iterations; retain `v1` verification and upgrade a legacy hash after successful login.
- Preserve `POST /api/auth/login` and Bearer clients; add a browser session endpoint using a host-only Secure, HttpOnly, SameSite=Lax cookie.
- Require antiforgery validation only for unsafe cookie-authenticated requests; keep Bearer requests exempt and give an explicit Authorization header precedence over a cookie.
- Remove frontend authentication secrets from Web Storage, restore browser identity through `/api/auth/me`, and synchronize session changes between tabs.
- Route challenge CSV exports through one typed writer that neutralizes untrusted spreadsheet formulas before RFC-style quoting while preserving trusted numeric, date, enum, and identifier cells.
- Do not change database entities, migrations, judge authority, queue payloads, or result semantics.

## Stage Result Ledger

| Step | Goal | Gate evidence | State |
|---|---|---|---|
| 4C.0 | Re-review 4B and current architecture | 4B judge suite 118 / 118; Docker PostgreSQL and Redis available | Completed |
| 4C.1 | Atomic verification-code lifecycle | Real Redis concurrency, attempt, TTL, daily-limit, cleanup, and sender-failure tests | Completed |
| 4C.2 | Shared password policy and versioned hashing | Policy, Unicode, malformed-hash, legacy-login rehash, registration/reset tests | Completed |
| 4C.3 | Cookie browser session and CSRF | Static/unit tests plus live login, `/me`, CSRF rejection, valid logout, and Bearer compatibility | Completed |
| 4C.4 | Spreadsheet-safe CSV exports | 8 / 8 malicious-cell and trusted-cell tests | Completed; real Office import NotRun |
| 4C.5 | Independent review and governance flush | Review findings fixed; focused regressions and ledgers completed | Completed |

## Verification Matrix

| Check | Result | Evidence |
|---|---|---|
| Complete backend suite before final review fixes | Passed | 1002 / 1002 with real Redis enabled |
| Post-review auth/account/session/Redis regression | Passed | 64 / 64; includes 8 real Redis integration tests |
| Redis integration disabled behavior | Passed | 8 / 8 reported explicitly as skipped, not passed |
| CSV security regression | Passed | 8 / 8 |
| Frontend tests | Passed | 5 / 5 |
| Frontend typecheck and lint | Passed | zero errors and zero warnings |
| Frontend production build | Passed with existing advisory | Vite build succeeded; pre-existing main-bundle size advisory remains assigned to 4E |
| Live browser-session flow | Passed | Session cookie flags verified; cookie `/me` 200; missing-CSRF logout 403; valid-CSRF logout 204 |
| Legacy Bearer flow | Passed | Existing login, `/me`, and logout remained successful |
| EF model drift | Passed | No model changes since the latest migration |
| Diff integrity | Passed | `git diff --check` clean before implementation commit `3d5c56f` |
| Excel/LibreOffice import | NotRun / blocked | Native Office automation unavailable; LibreOffice installation/download blocked by host policy |
| Hosted GitHub Actions execution | NotRun | Workflow configured with Redis service locally; no push was authorized |

## Review Findings And Resolution

| Finding | Resolution |
|---|---|
| Weak-password validation could reveal reset-account existence before code validation | Password policy now runs only after a valid code for reset flows; existing and missing accounts return the same generic failure for invalid codes |
| Frontend could appear logged out after a network/server logout failure | Local identity clears only after server success or a 401 response that also expires browser cookies |
| Oversized authentication input could reach Unicode normalization/hashing work | Added a fast UTF-16 bound and a 16 KiB authentication-controller request limit |
| Redis tests silently returned success when integration was disabled | Added a discover-time skip attribute and a GitHub Actions Redis service with the integration gate enabled |

## Public And Persistence Impact

- Added `POST /api/auth/session`; it accepts the existing login request shape and returns the existing user DTO while setting the browser session cookie.
- Existing `POST /api/auth/login` Bearer response remains unchanged.
- Password-hash persistence moves to versioned `v2.600000...`; existing `v1` values remain readable and are upgraded only after successful login.
- Redis verification keys are transient implementation state with atomic Lua semantics; no database migration or durable domain shape changed.
- CSV columns and order are unchanged; only cell safety/escaping semantics changed.

## Known Boundaries

- Browser cookie deployment requires HTTPS and a reverse proxy that preserves the original scheme. The application deliberately does not weaken Secure-cookie or antiforgery settings for plain HTTP production.
- The Redis Lua contract assumes the current single Redis deployment. A future Redis Cluster rollout must co-locate or redesign multi-key verification operations.
- Real Excel/LibreOffice import compatibility remains an explicit verification gap (`EXPORT-04C-D001`).
- No remote push, deployment, merge, external publication, or service exposure was performed.

## Stop Rule Confirmation

Stage 4C stops here. Stage 4D/4E work was not started.

## Subsequent Approved Correction

The later continuous package `RECRUITMENT-PRODUCTION-FRONTEND-04C-04E` supersedes only these product-policy details:

- the shared minimum password length is now 8 rather than 15 Unicode code points;
- the `AuthRegister` per-IP limiter was removed without a replacement limiter;
- phone binding/recovery was removed from the first-party frontend while backend routes and persisted fields were retained for compatibility.

The original session, CSRF, hash-version, Redis atomicity and CSV-export results remain unchanged.
