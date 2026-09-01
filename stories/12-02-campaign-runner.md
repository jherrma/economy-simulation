# The runner, one process per seed

**Epic:** E12 — Scenarios and the campaign
**Depends on:** 12-01, 11-04
**New ground:** Parallelism that cannot perturb a result

## Story

As a maintainer, I want each seed run as an independent process, with results collected only from completed runs, so that the campaign uses every core without any shared mutable state.

## Acceptance criteria

- [ ] **One process per seed. No shared mutable state.** Parallelism is across seeds only, never within a tick.
- [ ] A run's output is collected **only** if its completion marker is present (10-01).
- [ ] A halted run is recorded as a result — the halt tick and reason are data (07-09) — not silently dropped.
- [ ] Progress and failures are reported as the campaign proceeds, not only at the end.
- [ ] A partially completed campaign can be resumed without re-running finished seeds.
- [ ] A test asserts byte-identical output between a serial and a fully parallel campaign (**V2**).
- [ ] Total wall-clock is reported and compared against the estimate in `LANGUAGE-CHOICE.md` §5.3.

## Where to start

The process-per-seed design is what makes V2 achievable without effort. It is also why none of Go's or
Rust's concurrency advantages entered the language decision — there is no shared state to protect.

Resumability will matter more than it seems. A 150-scenario campaign is long enough that something will
interrupt it, and re-running finished seeds both wastes time and tempts people to shorten the campaign.

Comparing wall-clock against the estimate closes the loop on the benchmark. If the campaign takes five
times the predicted 39 minutes, the candidate-count assumption in `LANGUAGE-CHOICE.md` section 4.4 was wrong and the estimate should
be corrected in the document rather than quietly forgotten.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CampaignRunnerTests
```

Serial and parallel campaigns producing identical bytes, and the reported wall-clock against the
predicted figure.
