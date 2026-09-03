# The shopping walk

**Epic:** E4 — The decision
**Depends on:** 04-03, 04-04, 03-01
**New ground:** The rule itself, and the hot loop

## Story

As the model author, I want households visited in a seeded random order, each walking its candidates in descending score against a depleting cash budget, so that rationing is neutral and the tier a household lands on is decided by what it can reach.

## Acceptance criteria

- [ ] Household order is a **seeded shuffle, redrawn every tick**, from its own stream.
- [ ] Candidates across all wanted categories are ranked by `score` descending and walked; the walk **stops at the first candidate below λ**.
- [ ] An upgrade whose lower step was not taken is skipped, not stopped on.
- [ ] If the target tier has no stock, record a **blocked** unit against that tier and continue to the next candidate.
- [ ] If `Δcost ≤ cash_h`, take it: `cash_h` falls, the pool rises, and the category's chosen tier moves up one step.
- [ ] Otherwise record an **unaffordable** unit against that tier and continue. (Financing is 06-02.)
- [ ] Stock is consumed **once per category, at the final tier**, and `age_h,g = 0`.
- [ ] The budget depletes **sequentially** — each candidate taken changes what is affordable next. Affordability is not precomputed for the list.
- [ ] **First-come rationing within the random order.** A test asserts that over many ticks, the probability of being served is independent of income when supply binds.
- [ ] **No allocation inside the walk.** A test asserts zero managed allocations for a full tick.
- [ ] The walk is deterministic under parallelism: households do not share mutable state except stock, which is claimed atomically or the walk is serial.

## Where to start

The sequential budget is not an implementation detail dressed up as a rule; it is how spending
actually works, and it is what makes the tier a household lands on depend on what it bought first.
Precomputing which candidates are affordable would let a household take an upgrade it can no longer
pay for.

Neutral rationing is a deliberate choice and it is the conservative one. First-come within a random
order means nobody is favoured by wealth or willingness, so any crowding-out the model produces is
caused purely by *ability to bid at all* — which is exactly the mechanism under test. The
`rationing = willingness` variant is expected to strengthen the result and must therefore never be
the default.

Stock is claimed at the final tier only. A household that walks budget → standard has consumed one
standard unit, not one of each, and the increments it paid sum to the standard price. Getting this
wrong shows up immediately as stock draining twice as fast as it should.

This is ~95% of the tick. Keep it allocation-free from the first commit rather than optimising later.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~WalkTests
```

The zero-allocation assertion for a full tick, the income-independence of rationing, and stock
consumed once per category.
