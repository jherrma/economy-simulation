# Failure is a hazard, not a calendar

**Epic:** E11 — Product groups and replacement cycles
**Depends on:** 11-02, 01-05
**New ground:** A per-tick failure probability replacing `age ≥ life`, and the draw that must happen anyway

## Story

As the model author, I want a durable to fail with probability `1 / life` each tick instead of at a fixed age, so that a replacement cycle can be any positive real number and the opening cohorts cannot survive into the measured window.

## Acceptance criteria

- [ ] `replacement = "hazard"` switches step 5 from ageing to failure; **`deterministic` is the default** and reproduces v1 byte for byte (V5b).
- [ ] Under `hazard`, a household owning a working unit of `g` loses it when a draw from the `"failure"` stream is below `1 / life_h,g`. `age_h,g` is kept for reporting and decides nothing.
- [ ] **The draw is unconditional** — every household, every good, every tick, whether or not a unit is owned. A test asserts two arms that ration differently consume the identical stream and that a household's failure months are the same in both.
- [ ] `life = 1` needs no special case: `p = 1` consumes the unit every tick, and `wants = life == 1 || age ≥ life` collapses to one rule. A test asserts the life-1 goods behave identically to the deterministic path.
- [ ] Every household **opens owning a working unit of every durable**; `"initial_age"` is not consumed under `hazard`. A test asserts the first-tick replacement count is `capacity_g` within sampling error — no warm-up needed for the age distribution, because there is none.
- [ ] A test asserts stationarity of replacement demand over a long unconstrained run, and records the per-tick spread: about 15.9% relative for large appliances at 5,000 households, 0.84% over a 360-tick window.
- [ ] `d` outside `deterministic` is meaningful only here: a configuration setting a non-integer `life_h,g` under `deterministic` is rejected, never rounded.
- [ ] V4 is re-stated with the hazard's per-tick variance in mind, on `wait_median_met` as §10.1 requires.

## Where to start

The unconditional draw is the criterion that will be optimised away by somebody reading this code in a year, and it must not be. A household with no unit cannot fail, so drawing for it looks like waste. Skip it and the stream position depends on who was rationed — which differs between arms by construction, since being rationed is the thing under measurement. The two arms then sit on different worlds, every household is a different household, and the paired comparison keeps returning plausible numbers. 02-04 had this argument about `θ` and 10-02 had it about the archetype assignment; this is its third and least obvious instance, because here the *state* that would gate the draw is itself an outcome.

The second thing worth knowing before starting is that the hazard is **quieter** than the rule it replaces, which is not the intuition. Deterministic replacement separates its opening cohorts once, at initialisation, and then never mixes them: a household replacing in month 7 replaces in month 7 + life forever. Only rationing decorrelates them. Memorylessness does it for free, and an unconstrained four-month good oscillates at a per-tick standard deviation of 500 units under the calendar against 31 under the hazard. Expect the null run to get *easier*, not harder.

What the hazard cannot do belongs in the commit message and in §11: a constant hazard has no wear-out, and it models a thing breaking rather than a household choosing to replace something that still works. `prudent` keeping its phone for forty-two months is expressed here as a phone that fails less often, which is not what is meant.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~HazardTests
dotnet run --project tools/Gates -- nullrun
dotnet run --project tools/Gates -- creditoff
```

Identical stream consumption across differently-rationed arms; first-tick replacement equals capacity; V4 stationary; V5 green on the default `deterministic`.
