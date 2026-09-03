# Demand accounting: sold, blocked, unaffordable

**Epic:** E5 — Prices
**Depends on:** 04-05
**New ground:** The three counters, and the one that must not be demand

## Story

As the model author, I want each tier's outcomes counted in three separate buckets, so that the price rule sees demand that was willing and able but had nowhere to go, and nothing else.

## Acceptance criteria

- [ ] Per tier per tick: `sold`, `blocked` (willing and able, no stock), `unaffordable` (willing, could not pay).
- [ ] **`D = sold + blocked`. `unaffordable` appears in neither.** A test asserts this explicitly and by name.
- [ ] `blocked > 0` implies that tier had zero stock at the end of the tick.
- [ ] `sold ≤ units(g,t)`, every tier, every tick.
- [ ] Counters are per tier, never aggregated to the category before repricing.
- [ ] All three are written to output, so the reason a price moved can be recovered afterwards.
- [ ] A test constructs a tick where a household is willing but broke, and asserts the tier's `D` is unchanged.

## Where to start

This is the single most likely modelling mistake in the whole implementation, and it is silent both
ways.

Fold `unaffordable` into `D` and prices rise on goods nobody can buy — which makes more households
unable to buy them, which raises `unaffordable` further. The result is a clean monotone price rise
that looks exactly like a finding.

Leave `blocked` out of `D` and demand can never exceed supply, the rule only ever sees surplus, and
prices fall forever. Under `credit_high` nothing happens at all, and the conclusion would be that
credit has no price effect.

Neither failure trips any other check in the project, which is why the assertion is written by name
rather than left implied by the repricing test.

Keeping the counters per tier is what lets relative tier prices move, and that movement is the
trade-down channel. Aggregating to the category first destroys it.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DemandAccountingTests
```

The named assertion that `unaffordable` is in neither bucket, and the willing-but-broke case leaving
`D` untouched.
