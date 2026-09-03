# The goods table and the three tiers

**Epic:** E2 — Configuration and the world
**Depends on:** 02-01
**New ground:** Six categories as data, eighteen prices as a consequence

## Story

As the model author, I want the six categories as data and the tiers as multipliers off them, so that adding a category adds a row and never a branch, and so the tier system stays ten numbers rather than eighteen rows.

## Acceptance criteria

- [ ] Six categories with `life`, `capacity`, `price_ref`, `v`, `necessity`, `financeable`, `term`, per `02-PARAMETERS.md` §3.1.
- [ ] Three tiers as `(price_mult, value_mult, unit_share)`. Tier prices and values are **derived**, never stored per category.
- [ ] `a_g` and `b_g` are **computed** from `v_g` and `necessity_g` at load, not typed in. A test asserts `a_g + b_g · mean_income == v_g · mean_income` for every category.
- [ ] `units(g,t) = round(unit_share_t · capacity_g)`, and a test asserts `Σ_t units(g,t) == capacity_g` within the rounding, and that `capacity_g == round(households / life_g)`.
- [ ] A test asserts the value identity: `Σ_g Σ_t units(g,t) · price_ref_g · price_mult_t` is within 0.1% of `households × mean_income`.
- [ ] **No enum with a `switch` on category** anywhere in the engine. A test adds a synthetic seventh category and asserts the engine runs it with no code change.
- [ ] Grid membership for later experiments — financeable versus not, durable versus not — is computed from the numbers, never stored as a label.

## Where to start

Two identities in the criteria are load-bearing and both are one line to check.

The Stone-Geary split being **neutral at the mean income** is what lets `necessity_g` be tuned
without disturbing the calibration: it changes the income gradient of demand and nothing else. If
that assertion ever fails, someone has typed `a_g` in by hand.

The value identity is why the seller pool does not drain. Because the unit shares times the price
multipliers sum to exactly 1.000, capacity value is the same whatever the tier mix, so whenever the
market clears, nominal output equals nominal income. Splitting capacity by *value* instead of by
units breaks it, and the symptom appears fifty ticks later as a draining pool.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~GoodsTableTests
```

Both identities, and the synthetic-category test running without a code change.
