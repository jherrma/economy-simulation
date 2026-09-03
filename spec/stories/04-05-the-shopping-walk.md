# The shopping walk

**Epic:** E4 — The decision
**Depends on:** 04-03, 04-04, 03-01
**New ground:** The rule itself, and the hot loop

## Story

As the model author, I want households visited in a seeded random order, each walking its candidates in descending score against a depleting cash budget, so that rationing is neutral and the tier a household lands on is decided by what it can reach.

## Acceptance criteria

- [ ] Household order is a **seeded shuffle, redrawn every tick**, from its own stream.
- [ ] Candidates across all wanted categories are ranked by `score` descending and walked; the walk **stops at the first candidate below λ**. (Implemented as "best available candidate next": an upgrade joins the ranking when its lower step is taken. Identical to one sorted pass at opening prices; differs only once repricing has inverted a ladder, where a single pass would skip a worthwhile, affordable upgrade on list position alone. `01-SIMULATION.md` §6 step 4 amended.)
- [ ] An upgrade whose lower step was not taken is not available, and is never stopped on.
- [ ] If able and the target tier has no stock, record a **blocked** unit against that tier and continue. Ability is established before stock, because blocked is demand and unaffordable is not (05-01); the specification's original order counted a broke household at an empty shelf as demand. Amended.
- [ ] If `Δcost ≤ cash_h`, the household is able. Take it: `cash_h` falls, the pool rises, and the category's chosen tier moves up one step. A negative increment (a tier repriced below the one under it) is refunded the other way, so the total paid is always the posted price of the tier held.
- [ ] Otherwise record an **unaffordable** unit against that tier and continue. (Financing is 06-02.)
- [ ] Stock is consumed **once per category, at the final tier**, and `age_h,g = 0`.
- [ ] The budget depletes **sequentially** — each candidate taken changes what is affordable next. Affordability is not precomputed for the list.
- [ ] **First-come rationing within the random order.** A test asserts that over many ticks, the probability of being served is independent of income when supply binds.
- [ ] **No allocation inside the walk.** A test asserts zero managed allocations for a full tick.
- [ ] The walk is deterministic under parallelism: households do not share mutable state except stock, which is claimed atomically or the walk is serial.

## Notes from implementation (2026-09-03)

- **Step 1, income, is filled in here.** No story owned it; the walk is the first thing that needs
  the budget to replenish. A pool that cannot pay is reported as the calibration failure §6 step 7
  describes.
- **Zero allocation needed two things beyond the buffers:** the success `Result` is one shared
  instance (`Results.Ok`) and success is recognised by reference (`Results.IsOk`), because
  FluentResults allocates a result per `Ok()` and an enumerator per `IsFailed`. Together those were
  a quarter of a megabyte per tick. The order stream is reseeded in place rather than constructed.
- **λ is per household** since §5.3: `λ_h = λ · min(1, φ / b_h)`. `Walker.LambdaFor` computes it at
  the start of each household's walk; LambdaTests covers it.
- **The pool drains at tick 32 at opening prices.** Realised spend is about €226k a tick below
  income for seed 1 — desired spend is well below income (§3.3) and rationing lowers it further —
  so with no repricing yet the twelve-month pool is gone by tick 32. The 360-tick tests run with a
  400-month pool until 05-02, which must show the default pool survives the warm-up.
- **What tick 1 looks like (seed 1):** budget and standard food both sell out, 145 households are
  blocked at budget food and get none, and 181 of 200 premium food units go unsold. The same
  pattern, smaller, in leisure and clothing.
- **Open question for the author:** a household blocked at budget cannot buy standard the same
  tick even with the cash (§6, after step 4). At tick 1 that is 145 households without food beside
  a full premium shelf. v1 implements the ladder as written.

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
