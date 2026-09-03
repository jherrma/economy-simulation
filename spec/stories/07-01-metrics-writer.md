# The metrics writer

**Epic:** E7 — Output
**Depends on:** 03-04
**New ground:** The engine's only output surface

## Story

As a maintainer, I want every recorded series written to CSV and nothing else, so that the engine stays small enough to audit and the analysis stays a separate choice.

## Acceptance criteria

- [ ] **CSV only.** No statistics, no plotting, no analysis inside the engine.
- [ ] Two files: `tiers.csv` (tick × category × tier: price, units, sold, blocked, unaffordable) and `run.csv` (tick: CPI, money stock, loans outstanding, pool, plus the cohort series of 07-03).
- [ ] Every row carries the run seed and the scenario id, so files concatenate without ambiguity.
- [ ] The **effective configuration** is written alongside (02-02).
- [ ] Warm-up ticks are **written and flagged**, never silently discarded — the transient can only be checked against data that was kept.
- [ ] A **completion marker** is written only on a clean finish. A halted run leaves output that no collector will accept.
- [ ] The schema is **additive**: columns are added, never renamed, reordered by meaning, or repurposed. A test reads a committed older file with the current reader.
- [ ] Writing is buffered and does not perturb the tick timing measurably.

## Where to start

The completion marker is the criterion that protects the campaign. E9 will launch hundreds of
processes and collect their files; a halted run that leaves a plausible partial CSV will be silently
aggregated into a result, and that is the one failure this project cannot tolerate. Make the
collector require the marker rather than treating the presence of a file as success.

Writing warm-up rather than discarding it costs nothing and is the only way V4's "the transient
actually decayed" claim can be checked rather than assumed.

Resist adding derived statistics. Every index, every percentile is a place the engine would start
making analytical choices that belong outside it — and outside is where they can be revised without
re-running the campaign.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~MetricsWriterTests
```

A round-trip asserting every specified series is present, and a halted run leaving no marker.
