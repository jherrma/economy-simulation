# `flow_value`: the Stone-Geary floor and the tier multiplier

**Epic:** E4 — The decision
**Depends on:** 02-03
**New ground:** What a unit is worth, per tick, in euros

## Story

As the model author, I want value expressed in euros per tick with a floor that does not scale with income, so that a month of food and an eight-year appliance are comparable and a poor household still eats.

## Acceptance criteria

- [ ] `flow_value(h, g, t) = (a_g + b_g · income_h) · w_h · value_mult_t`, per `01-SIMULATION.md` §5.
- [ ] **Everything is euros per tick.** There are no utility units anywhere in the codebase.
- [ ] A test pins the Engel behaviour: at €300 of income, food's base score is above λ; **with `a_g` forced to zero it is not**, and the same test shows the household buying no food in that case.
- [ ] A test pins the income gradient: food's *share* of desired spending falls as income rises, while electronics' rises.
- [ ] The value multiplier is applied by tier and nowhere else — `value_mult` is not baked into `v_g`.
- [ ] A test doubles every nominal quantity and asserts every `flow_value` doubles exactly.
- [ ] No allocation on this path.

## Where to start

The floor is not a refinement, it is the difference between a model and a broken one. With value
strictly proportional to income, every good is a luxury: a household on €450 scores food at 0.93,
buys none, and no invariant in the project notices. The `a_g / b_g` split is the standard fix and it
is neutral at the mean income by construction, so it changes the income gradient of demand and
nothing else.

Keep the tier multiplier outside `v_g`. It is tempting to precompute eighteen value weights, and it
makes the diminishing-returns property in 04-03 impossible to state or test, because the
relationship between the multipliers is exactly what that property is about.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~FlowValueTests
```

The Engel test with and without the floor, and the doubling test — if `flow_value` does not scale
exactly linearly, something is not indexed and V3 will fail later for a much harder reason to find.
