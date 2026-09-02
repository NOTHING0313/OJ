# Main Integration Handoff: 2026-09-02

## Integration Scope

- Repository: `E:/Github/OJ`
- Source branch: `feat/theme-10h-theme-library`
- Local `main` before integration: `97004bde09d977b2f3df5fb3621949833f2e6559`
- Remote `origin/main` observed before integration: `97004bde09d977b2f3df5fb3621949833f2e6559`
- Feature tip before this handoff document: `5409950a1d19bd39a65139d2510be123288d6cd2`
- Relationship before integration: local `main` is an ancestor of the source branch; the source branch is 28 commits ahead and 0 behind.
- Integration method: local fast-forward only (`git merge --ff-only`).
- Push/deployment: not authorized and not performed.

The integrated history covers the completed OnlineJudge work from hidden judge assets through theme editor production-preview fidelity. The final feature commit adds theme-pack preflight and asset metadata behavior, shared production UI views for deterministic preview reuse, accessible editor dialogs, deterministic preview fixtures, fidelity automation, and aligned static contract tests.

## Verification Evidence

- `dotnet build OnlineJudge.sln`: PASS, 0 warnings, 0 errors.
- Targeted contract tests after correcting shared-view source paths: 97 / 97 PASS.
- `dotnet test OnlineJudge.sln --no-build`: 937 / 937 PASS, 0 failed, 0 skipped.
- `frontend/npm run build`: PASS.
- Frontend warning: the existing Vite chunk-size warning remains (`index` bundle above 500 kB); no dependency or bundling change was made to address it.
- `dotnet ef migrations has-pending-model-changes --project OnlineJudge.Infrastructure/OnlineJudge.Infrastructure.csproj --startup-project OnlineJudge.Api/OnlineJudge.Api.csproj --no-build`: PASS; no pending model changes.
- `node scripts/e2e/theme-preview-fidelity.mjs`: PASS for leaderboard, problem detail, and help center. Geometry/style checks passed, preview made zero real API calls, and editor observability checks passed.
- `git diff --check`: PASS before commit.
- Generated fidelity screenshots/results are under ignored `artifacts/` and are not part of Git history.

## Deferred Risk Backlog

These items are deliberately not remediated by this integration. A future owner must independently reproduce, prioritize, design, implement, and verify each item before treating it as closed.

### Highest Priority

1. Judge queue delivery semantics: the database submission transaction and Redis enqueue are separate, while worker consumption uses destructive pop behavior without an acknowledgement/recovery protocol. Review for lost or stranded submissions and introduce a durable recovery design without weakening judge isolation.
2. Hidden judge asset isolation: hidden inputs are materialized into the contestant execution workspace. Review filesystem ownership and mount/write boundaries so submitted code cannot mutate or disclose hidden assets.
3. Empty test-suite acceptance: verify whether a problem with zero effective test cases can produce an accepted result. Define and enforce the authoritative behavior in problem validation and judge execution.

### Additional Security And Operations Work

- Strengthen registration password policy with compatibility and user-migration considerations.
- Review email/SMS verification consumption for atomicity and replay/concurrency behavior.
- Add evidence-based input, upload, batch, repository, and resource limits where currently unbounded.
- Harden production Redis persistence/health checks and complete production TLS/HTTPS work separately from application releases.
- Prevent CSV formula injection in exported user-controlled fields.
- Reassess browser token storage (`localStorage`) within a complete authentication threat model.
- Review shared runtime directory permissions, upload quota/cleanup, and configured/default path consistency.
- Add a maintained frontend test/lint/CI gate; consider SDK pinning and the .NET 9 support lifecycle.
- Reduce oversized services/styles and investigate duplicated or oversized frontend bundles only as separately scoped refactors.

## Safety Boundaries For Follow-up

- Do not weaken SSRF, authorization, sandbox, hidden-test, rate-limit, or audit-log controls to obtain a quick green test.
- Queue, judge workspace, authentication, persistent storage, and deployment changes require their own architecture and rollback plans.
- Database migrations, public contracts, Docker/Nginx/TLS, production data, and secrets must not be changed as incidental fixes.
- Re-run the complete build/test/frontend/EF/diff gates after any risk remediation, plus focused runtime/security tests for the changed boundary.

## Git And Rollback Notes

- This handoff records a local integration only. `origin/main` remains unchanged until an authorized human push.
- The source feature branch is retained as a named reference after fast-forward integration.
- If rollback of the local integration is later requested, use the recorded pre-integration `main` commit as the recovery reference only after explicit authorization; do not reset or rewrite history implicitly.

## Next Owner

Continue from local `main`, select one deferred risk as a separately scoped task, validate the finding against current code, and submit an independently reviewable change. Do not treat this document as proof that any deferred finding has been fixed.
