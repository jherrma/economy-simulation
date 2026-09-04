# Assignment, and the taste vector inside `flow_value`

**Epic:** E10 — The population has types
**Depends on:** 10-01, 02-04, 04-01
**New ground:** Two new draws, and `w_h` becoming `w_h,g`

## Story

As the model author, I want each household assigned an archetype from its own stream and its taste applied per category, so that the decision rule sees a population with structure instead of one taste multiplier repeated six times.

## Acceptance criteria

- [ ] `archetype_h` drawn from purpose `"archetype"`, by share, **in every run including the identity table** — the rule `θ` already obeys (`01-SIMULATION.md` §8).
- [ ] A test runs two scenarios on one seed and asserts the **assignment is identical**, household by household, exactly as 02-04 asserts for the abstainer set.
- [ ] The assignment is **independent of the abstainer draw**. A test asserts the archetype distribution among abstainers matches the population distribution within sampling error.
- [ ] `flow_value(h, g, tier) = (a_g + b_g · income_h) · w_h · ŵ_g,A(h) · ε_h,g · value_mult(tier)^κ_g,A(h)`.
- [ ] `ε_h,g` drawn from purpose `"taste_idio"` with `sigma_idio`, mean exactly 1; at the default `sigma_idio = 0` it is exactly 1 and **no draw is consumed differently** for it than at any other setting.
- [ ] The exponent is applied to the **tier value multiplier only**, never to price, and `value_mult(standard) = 1` remains exactly 1 for every `κ` — a test asserts the standard tier's value is independent of `κ`.
- [ ] The campaign's paired-seed check (09-02) compares archetype assignment across arms as well as the abstainer set, and compares the **assignment**, not the count per type.
- [ ] V6's monotonicity assertion holds per household and per category under the `typed` table.
- [ ] Money stays integer cents; the exponent is applied in the valuation, which is euros per tick, never to a price.

## Where to start

The two new draws are the whole risk, and the risk is not that they are wrong — it is that they are *skipped*. Under the identity table an archetype assignment looks like wasted work, and removing it is the natural optimisation for someone reading the code later. If it is removed, a typed scenario and its own `credit_off` sit on different random worlds, every household is a different household, and the paired comparison keeps returning numbers that look plausible. 02-04 already had this argument about `θ`; the conclusion has not changed and the reason belongs in a comment where the temptation is.

Independence from the abstainer draw is a modelling requirement rather than an implementation detail. If abstainers were systematically more prudent, the headline would confound *does not borrow* with *wants less* and no invariant would notice. Someone may want to run that deliberately one day — it is an interesting scenario — but it must be something a configuration says out loud, never something the draw order arranges by accident.

On the exponent: apply it where the tier multiplier is read, not by pre-multiplying it into the household's taste. They are different numbers doing different jobs — the level decides whether the category is worth entering at all, the steepness decides how far up its ladder the household climbs — and collapsing them is precisely the conflation §5.4 exists to undo.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ArchetypeTests
dotnet test --filter FullyQualifiedName~DrawTests
```

Identical assignments across scenarios on one seed; archetype and abstainer independent; the standard tier's value unchanged by `κ`.
