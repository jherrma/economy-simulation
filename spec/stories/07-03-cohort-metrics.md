# Cohort metrics: the finding itself

**Epic:** E7 — Output
**Depends on:** 07-02, 04-04
**New ground:** Four numbers about the households that never borrow

## Story

As the person writing this up, I want the abstainer and borrower cohorts measured separately on four dimensions, so that the headline claim is a recorded series rather than something reconstructed afterwards.

## Acceptance criteria

- [x] Cohorts are **abstainers** (`θ = 0` by construction, 20%) and **borrowers** (everyone else), fixed at initialisation and identical across scenarios for a given seed.
- [x] **`share_of_wanted_obtained`** per category: units bought ÷ units wanted. With supply fixed, total real consumption is capped, so the question is who gets it.
- [x] **`wait`**: for each durable want, ticks between first wanting and obtaining. Reported as a cohort median, with unfulfilled wants counted at their current age rather than dropped — dropping them would make a cohort that never gets served look patient.
- [x] **`tier_mix` by cohort** — the share of each cohort's purchases at each tier. This is the trade-down finding.
- [x] **`quality_index`**: units obtained weighted by `value_mult`, so a cohort that holds its unit count by buying worse goods is not recorded as unaffected.
- [x] Also per cohort: nominal spend, cash held, loans outstanding, debt service.
- [x] A test asserts the two cohorts partition the population exactly and that membership is identical across two scenarios on one seed.

## Where to start

The four measures are ordered by how easy they are to argue with, and the last two are the ones this
model was rebuilt to produce. `wait` and `share_of_wanted` say the abstainer is served later or less
often; `tier_mix` and `quality_index` say they are served *worse*. The second pair is the sharper
claim and the one a reader recognises: credit did not stop them owning a phone, it moved them to a
cheaper phone.

Counting unfulfilled wants at their current age rather than dropping them matters more than it
looks. Under `credit_high` the abstainers most affected are exactly the ones who never get served,
and a median computed only over completed waits would silently exclude them and report an
improvement.

Everything here is a raw series. The differencing between scenarios happens outside the engine.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CohortMetricsTests
```

The partition test, identical membership across scenarios, and a synthetic run where a cohort's
unit count is unchanged but its `quality_index` falls.

## Implementation note (2026-09-03)

`share_of_wanted_obtained` is written as its two integers, `wanted` and `obtained`, rather than as
the ratio: the differencing happens outside the engine, and a ratio with a zero denominator is a
decision the analysis should make rather than the writer.

The median wait is computed from a **histogram** rather than a sorted list, because it is computed
every tick inside a run that allocates nothing and a wait is a small non-negative integer. It covers
durable wants only — food and leisure are met or not within the same tick, so their zeros would
swamp the number — and it counts wants met this tick at the wait they were met after, together with
wants still open at their current age.

Two wait medians are recorded, not one. `wait_median` is the story's measure — every durable want
the cohort faced, unmet ones at their current age. It mixes a flow against a growing stock and is
neither stationary nor smooth (`01-SIMULATION.md` §10.1), so it is sound for the paired same-tick
comparison the headline is defined as, and unusable as a stationarity criterion. `wait_median_met`
covers the wants actually met in the tick; it is zero throughout the baseline, which is the finding
rather than a defect — the rationing is an exclusion, not a queue. Neither is sufficient alone.

The tier and the price paid are known only inside the walk: after it, all that survives is that
*something* was bought. So the walk reports each purchase to the cohort series, through a property
the simulation sets, left null by tests that drive a walker directly.
