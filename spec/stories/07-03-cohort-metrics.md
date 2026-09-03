# Cohort metrics: the finding itself

**Epic:** E7 — Output
**Depends on:** 07-02, 04-04
**New ground:** Four numbers about the households that never borrow

## Story

As the person writing this up, I want the abstainer and borrower cohorts measured separately on four dimensions, so that the headline claim is a recorded series rather than something reconstructed afterwards.

## Acceptance criteria

- [ ] Cohorts are **abstainers** (`θ = 0` by construction, 20%) and **borrowers** (everyone else), fixed at initialisation and identical across scenarios for a given seed.
- [ ] **`share_of_wanted_obtained`** per category: units bought ÷ units wanted. With supply fixed, total real consumption is capped, so the question is who gets it.
- [ ] **`wait`**: for each durable want, ticks between first wanting and obtaining. Reported as a cohort median, with unfulfilled wants counted at their current age rather than dropped — dropping them would make a cohort that never gets served look patient.
- [ ] **`tier_mix` by cohort** — the share of each cohort's purchases at each tier. This is the trade-down finding.
- [ ] **`quality_index`**: units obtained weighted by `value_mult`, so a cohort that holds its unit count by buying worse goods is not recorded as unaffected.
- [ ] Also per cohort: nominal spend, cash held, loans outstanding, debt service.
- [ ] A test asserts the two cohorts partition the population exactly and that membership is identical across two scenarios on one seed.

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
