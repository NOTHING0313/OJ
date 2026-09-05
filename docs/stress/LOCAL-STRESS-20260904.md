# OnlineJudge localhost stress calibration — 2026-09-04

## Result

The current working tree passed a bounded localhost Docker calibration for judge correctness, sustained submission arrival and 10/20-request submission bursts. This is not a 2 vCPU / 4 GB capacity certification because the API and Worker ran as Windows host processes on a 32-logical-CPU, 34 GB machine.

Docker Desktop peak-memory telemetry was unavailable to the Windows Worker because the current implementation reads Linux host cgroup files. Time metrics were measured for every executed case; memory metrics remained null and still require a Linux-host run.

## Isolated environment

- API: `http://127.0.0.1:15101`
- PostgreSQL: isolated ephemeral container on `127.0.0.1:15433`, limited to 1 CPU / 1 GiB
- Redis: isolated ephemeral container on `127.0.0.1:16379`, limited to 0.25 CPU / 256 MiB
- Worker: one Windows host process, in-process concurrency 1
- Dataset: 100 users, 50 problems, 500 test cases, 1,000 historical submissions
- Existing development PostgreSQL volume and Redis data were not used

## Judge matrix

All 16 expected outcomes matched across C++17, C11 and C#: Accepted, Compile Error, Wrong Answer, Time Limit Exceeded and Memory Limit Exceeded, plus the bounded file-write case.

The matrix artifact is `artifacts/stress/local-judge-matrix.json`.

## Sustained submission calibration

Each stage used nine rotating accounts and one Worker. PostgreSQL `JudgeJobs` was the queue authority.

| Configured arrival | Actual request rate | Requests | HTTP P95 | Max pending | Max oldest pending | Result |
| ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 0.05/s | 0.056/s | 4 | 15.511 ms | 0 observed | 0 s observed | Pass; final queue verified empty |
| 0.10/s | 0.097/s | 6 | 154.300 ms | 1 | 1 s | Pass |
| 0.15/s | 0.138/s | 9 | 44.148 ms | 0 | 0 s | Pass; one leased job observed |
| 0.20/s | 0.178/s | 11 | 37.035 ms | 1 | 1 s | Pass |

The 0.05/s generator emitted four requests around the 60-second boundary rather than the nominal three. Later stages were evaluated using actual request rate and count.

## Burst calibration

| Burst | Accepted | HTTP P95 | Peak queue | Max oldest pending | Drain time | Result |
| ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 10 | 10 | 53.592 ms | 10 pending | 18 s | about 22 s | Pass |
| 20 | 20 | 87.797 ms | 19 pending + 1 leased | 39 s | about 42 s | Pass |

The API remained responsive while judge result wait increased approximately linearly, which matches the single-Worker sequential execution model.

## Login burst

Twenty simultaneous logins produced 10 HTTP 200 and 10 HTTP 429 responses, with no 5xx or transport failure. This matches the current `AuthLogin` fixed-window policy of 10 attempts per client IP per minute. Users sharing one NAT address can therefore hit login throttling even though the Worker is healthy.

## Final integrity snapshot

- Judge jobs: 77 completed, 0 pending, 0 leased, 0 dead-lettered
- Maximum attempt count: 1
- Job created-to-finished duration: average 9.101 s, P95 33.770 s, maximum 41.583 s
- Case results: 74 time values measured; 0 memory values measured on Windows
- API working set snapshot: 239.92 MiB; peak 242.79 MiB
- Worker working set snapshot: 147.32 MiB; peak 152.05 MiB
- PostgreSQL: 112 MiB, healthy, restart count 0, not OOM-killed
- Redis: 11.19 MiB, healthy, restart count 0, not OOM-killed
- API health response: HTTP 200
- Residual judge containers: none

## Limitations of the initial single-consumer run

- Run the same matrix and ten-minute arrival ladder on the target Linux 2C4G host.
- Verify maximum and average case memory from Linux cgroup telemetry.
- This initial result alone did not approve additional Worker concurrency; the 2026-09-05 follow-up below supplies the later user-approved CPU/memory and scheduling evidence.

## Bounded Worker parallelism follow-up — 2026-09-05

The user authorized Docker Desktop as the localhost capacity substitute for limited Worker parallelism and narrowed the gate to CPU scheduling and memory occupancy.

| Worker consumers | Burst | Drain time | Average job time | Maximum job time | HTTP result |
| ---: | ---: | ---: | ---: | ---: | --- |
| 1 | 10 | 21.648 s | 12.027 s | 21.156 s | 10/10 HTTP 201; no 429/5xx |
| 2 | 10 | 12.380 s | 7.729 s | 11.926 s | 10/10 HTTP 201; no 429/5xx |
| 2 repeat | 10 | 11.912 s | 7.078 s | 11.145 s | 10/10 HTTP 201; no 429/5xx |

The first valid comparison reduced drain time by 42.8%; the repeat reduced it by 45.0%. Database integrity checks found ten distinct jobs and submissions per batch, attempt count exactly one, ten completed jobs, zero dead letters and one case-result row per submission. The repeat collector observed a maximum of two leased jobs, proving actual overlap between consumers.

The local collector records explicit API/Worker PIDs, host CPU and memory, isolated state-container memory, transient judge-container count/CPU/memory, PostgreSQL pending/leased/completed/dead counts, oldest-pending age and API health. One earlier collector attempt failed and was excluded before the valid baseline was repeated. Future open-loop runs use corrected deadline scheduling, and submission account setup no longer consumes an extra root login.

### CPU/memory acceptance probe

A fresh two-consumer burst submitted twenty jobs in 0.102 seconds: all returned HTTP 201, all twenty completed with attempt count one, no dead letters and exactly one case-result row per submission. The queue reached two simultaneous leases and drained with an average created-to-finished duration of 11.673 seconds and a maximum of 20.929 seconds. During the run host CPU peaked at 29%, API peak working set at 201.09 MiB, Worker peak working set at 148.38 MiB, PostgreSQL at 164.20 MiB and Redis at 7.76 MiB; API health stayed HTTP 200.

The real compile/run containers are too short-lived for the roughly 4-5 second Docker Desktop no-stream sample, so zero observations from that collector are treated as missing samples, not zero memory. A supplemental two-container probe used the production C++ sandbox image and the same essential limits: 1 CPU and 128 MiB per container, no network or IPC, read-only root filesystem, dropped capabilities, 64-PID cap and 64 MiB tmpfs. Both containers overlapped, reached 200.16% aggregate Docker CPU and 103.84 MiB aggregate memory, exited zero and were not OOM-killed. Host free memory remained at least 5.34 GiB and API health stayed HTTP 200.

This separates two claims cleanly: the real Worker burst proves queue correctness and two-way scheduling; the sandbox-equivalent probe measures the requested two-container CPU/memory envelope. It does not repair Windows per-case cgroup telemetry.

The Worker accepts `JudgeWorker:Concurrency` only in the range 1-2. Base, Development and the checked-in production environment example now default to two. Remote deployment has not been performed.
