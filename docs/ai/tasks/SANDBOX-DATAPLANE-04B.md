# Task Review: SANDBOX-DATAPLANE-04B

## Status

`COMPLETED`

Stage 4B hardens the Docker judge data plane and turns the existing function-mode coverage into a blocking reliability gate while preserving the Stage 1-3 revision and durable-job contracts.

## Risk Classification

- Initial risk: `R4` because the task changes the submitted-code isolation boundary and real Docker execution behavior.
- Final risk: `R4`; the implementation remained inside the reviewed sandbox/function-test boundary and introduced no HTTP, persistence, queue, or revision change.
- Governance mode: continuous staged governance; a failed Docker isolation or function-mode regression gate blocks completion.
- Cost ruling: use targeted review/tests during implementation and run the complete repository gate once at the end; no repeated full-repository scan.

## Contract

- Keep all submitted-code execution in `OnlineJudge.JudgeWorker`; do not move execution into the API.
- Preserve immutable `ProblemJudgeRevision`, PostgreSQL `JudgeJob` authority, Redis wake-hint payloads, lease fencing, result statuses, and persisted shapes.
- Keep compilation workspaces writable only for compiler output, but mount them read-only for every testcase execution.
- Apply an enforceable file-size ceiling and the existing bounded `/tmp` filesystem to compile and run containers.
- Treat `MaxCapturedOutputBytes` as one combined stdout/stderr budget and terminate the container as soon as that budget is exceeded.
- Keep user-visible output-limit behavior within the existing `RuntimeError`/`CompileError` status contract.
- Make function-mode entry-point detection ignore comments and string/character literals, so harmless text does not reject valid submissions.
- Add blocking unit and real Docker regressions for isolation, output limits, C11/C++17/C# function mode, and custom structs.
- Do not add HTTP DTOs, migrations, packages, services, language support, or externally published artifacts.

## Planned Stage Ledger

| Step | Goal | Required gate | State |
|---|---|---|---|
| 4B.0 | Re-review current implementation | Clean scoped diff; Docker available; targeted function/sandbox tests pass | Completed |
| 4B.1 | Freeze executable isolation/output contract | Red tests added for combined output, mount access, compile overflow, and source false positives | Completed |
| 4B.2 | Harden Docker execution data plane | 105 / 105 targeted tests; Docker `fsize` probe; real output/write smoke | Completed |
| 4B.3 | Harden function-mode source preflight | Real entry points still rejected; comments and literal forms no longer reject | Completed |
| 4B.4 | Run real function/security regression | Judge 7 / 7; function 10 / 10; struct 3 / 3; security 50-run cleanup and write quotas | Completed |
| 4B.5 | Complete repository verification and governance flush | Canonical full gate and EF result recorded below; ledgers updated | Completed |

## Verification Plan

| Check | Purpose |
|---|---|
| Sandbox/function targeted tests | Fast contract and regression feedback |
| Direct Docker argument/file-limit probes | Prove the selected Docker runtime supports the enforced limits |
| Judge sandbox security smoke | Prove network/process/memory/timeout/write isolation and cleanup |
| Function-mode E2E and custom-struct smoke | Prove generated harnesses compile and execute for supported languages |
| Canonical full repository gate | Detect cross-component regressions once the scoped changes stabilize |
| EF model drift | Prove no persistence change entered 4B |

## Verification Matrix

| Check | Result | Evidence |
|---|---|---|
| Pre-change targeted baseline | Passed | 99 / 99 function and sandbox tests |
| Stage 4B targeted suite | Passed | 105 / 105 function and sandbox tests |
| Release solution build | Passed | 0 warnings / 0 errors before real Docker gates |
| Docker file-limit capability | Passed | Docker 29.7.2 enforced `fsize=1048576` with `File too large` |
| Standard judge smoke | Passed | 7 / 7; report `artifacts/smoke/20260903-113230/result.json` |
| Function-mode E2E | Passed | 10 / 10 array, list, tree, language-rejection, and standard regression scenarios |
| Custom-struct function smoke | Passed | C11/C++17/C# each Accepted 2 / 2; report `artifacts/smoke/20260903-112312/result.json` |
| Sandbox security smoke | Passed | 50 leak runs; runtime workspace read-only; `/tmp` and file-size quota passed |
| Canonical full repository gate | Passed | Build 0 warnings/errors; backend 968 / 968; EF no drift; frontend lint, 3 / 3 tests, and production build passed |
| Public/persistence contract check | Passed | No DTO, entity, migration, Redis payload, or result-status change |

## Diff Intent Table

| Change group | Intended effect | Scope check |
|---|---|---|
| Docker command and sandbox orchestration | Read-only runtime mount, IPC/user/file limits, combined output, eager termination | Infrastructure-internal request only |
| Function user-code guard and runners | Avoid comment/string false positives while retaining preflight | No function spec or result contract change |
| Sandbox/function unit tests | Lock down isolation, output, compile and source-preflight behavior | Test-only |
| Judge/security/function scripts | Make real gates blocking and compatible with immutable publication revisions | Local verification only |
| Governance documents | Record decisions, evidence, resolved debt and residual compile boundary | Documentation only |

## Deferred Governance Queue

| Item | Disposition |
|---|---|
| Runtime workspace and output budget decision | Flushed to `DecisionLog.md` |
| Function entry-point preflight decision | Flushed to `DecisionLog.md` |
| Runtime host-disk risk `SANDBOX-04B-D001` | Resolved |
| Aggregate writable compile-workspace quota | Recorded as `SANDBOX-04B-D002` |
| HTTP/API/persistence ledger | No change; no Public API Ledger entry required |

## Documentation Update Table

| Document | Update |
|---|---|
| `docs/ai/context/DecisionLog.md` | Added data-plane and function-preflight decisions |
| `docs/ai/context/TechnicalDebtRegister.md` | Resolved runtime disk risk; recorded residual compile aggregate quota |
| `docs/ai/context/OnlineJudge-Context-Capsule.md` | Advanced verified baseline from 4A to 4B |
| `docs/ai/tasks/SANDBOX-DATAPLANE-04B.md` | Recorded contract, execution, evidence and stop boundary |

## Known Boundaries

- The compiler bind mount remains writable because supported compilers must emit runtime artifacts. Every file is capped and compilation has a 30-second timeout, but an aggregate quota is not portable for Docker bind mounts; tracked as `SANDBOX-04B-D002`.
- Hosted Docker CI remains unavailable until a Docker-capable runner is provisioned (`PLATFORM-04A-D002`).
- No remote push, deployment, or externally visible service exposure was performed.

## Stop Rules

- Stop if runtime workspaces cannot be made read-only without breaking supported submissions.
- Stop if output overflow or timeout can leave a managed container running.
- Stop if any supported function-mode Docker regression fails.
- Stop after 4B; do not begin Stage 4C without a new instruction.

## Stop Rule Confirmation

Stage 4B is complete and stops here. Stage 4C was not started.
