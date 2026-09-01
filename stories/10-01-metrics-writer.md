# The metrics writer

**Epic:** E10 — Metrics and output
**Depends on:** 04-05
**New ground:** The engine's only output surface, and the boundary D27 drew

## Story

As a maintainer, I want every recorded series written to CSV or Parquet and nothing else, so that the engine stays small enough to audit and the analysis tooling remains a separate choice.

## Acceptance criteria

- [ ] **CSV or Parquet only.** No analysis, no statistics, no plotting inside the engine (D27).
- [ ] One row per tick per series group, with the run seed and scenario id on every row so files can be concatenated without ambiguity.
- [ ] The **effective configuration** is written alongside, so a result can always be traced to the parameters that produced it (03-02).
- [ ] A **completion marker** is written only on a clean finish; a halted run (07-09, 02-05) leaves output that no collector will accept.
- [ ] Writing is buffered and does not perturb the tick's timing measurably.
- [ ] Warm-up ticks are written but **flagged**, so statistics can exclude the first 240 without a second run.
- [ ] A test round-trips a run's output and asserts every §9 series is present.

## Where to start

The completion marker is the criterion that protects the campaign. E12 will launch hundreds of processes
and collect their files; a halted run that leaves a plausible partial CSV behind will be silently
aggregated into a result, and that is the failure this project cannot tolerate. Make the collector require
the marker rather than assuming presence of a file means success.

Writing warm-up rather than discarding it is a small decision with a large payoff: the §13.1 requirement
that the transient actually decayed can only be checked against data that was kept.

Resist adding derived statistics here. Every Gini, every decile, every index is a place where the engine
would start making analytical choices that belong outside it — and outside is where they can be revised
without re-running 150 scenarios.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~MetricsWriterTests
```

A halted run leaving no acceptable output, and the effective-config file matching the run's actual
parameters rather than the input file.
