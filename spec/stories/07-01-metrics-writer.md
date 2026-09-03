# The metrics writer

**Epic:** E7 — Output
**Depends on:** 03-04
**New ground:** The engine's only output surface

## Story

As a maintainer, I want every recorded series written to CSV and nothing else, so that the engine stays small enough to audit and the analysis stays a separate choice.

## Acceptance criteria

- [x] **CSV only.** No statistics, no plotting, no analysis inside the engine.
- [x] Two files: `tiers.csv` (tick × category × tier: price, units, sold, blocked, unaffordable) and `run.csv` (tick: CPI, money stock, loans outstanding, pool, plus the cohort series of 07-03).
- [x] Every row carries the run seed and the scenario id, so files concatenate without ambiguity.
- [x] The **effective configuration** is written alongside (02-02).
- [x] Warm-up ticks are **written and flagged**, never silently discarded — the transient can only be checked against data that was kept.
- [x] A **completion marker** is written only on a clean finish. A halted run leaves output that no collector will accept.
- [x] The schema is **additive**: columns are added, never renamed, reordered by meaning, or repurposed. A test reads a committed older file with the current reader.
- [x] Writing is buffered and does not perturb the tick timing measurably.

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

## Implementation note (2026-09-03)

Two traps the writer exists to avoid, both of which would have produced a plausible file with the
wrong numbers in it. Repricing (step 6) runs *before* the tick ends, so the market holds the **next**
tick's prices by the time anything can ask; and restocking clears the demand counters at the start
of the next tick. `TickRecord` is filled at the two moments the answers are still true — the prices
when the shelves are stocked, the aggregates after the check has passed — and the writer reads it.

`run.scenario` was added to `02-PARAMETERS.md` §1 for this story: a label, read by nothing in the
model, written into every row so that hundreds of runs' files concatenate without ambiguity.

"Does not perturb the tick timing measurably" is tested as *buffered rather than flushed per row*
(a hundred ticks of rows still in the buffer when the file is inspected), not as a wall-clock
comparison, which would be a flaky assertion about the machine rather than about the writer.
