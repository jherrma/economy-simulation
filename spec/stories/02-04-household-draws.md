# Household draws: income, taste, θ, abstainers and durable ages

**Epic:** E2 — Configuration and the world
**Depends on:** 02-03, 01-05
**New ground:** The population, drawn once, from named streams

## Story

As the model author, I want every per-household attribute drawn at initialisation from its own named stream, so that the population is reproducible and adding an attribute later disturbs nothing.

## Acceptance criteria

- [ ] `income_h = mean_income · exp(σ_income · z − σ_income²/2)`, and a test asserts the **sample mean converges to `mean_income`**, not merely to something near it — the `−σ²/2` term is the point.
- [ ] `w_h` drawn the same way with `σ_w`, mean exactly 1.
- [ ] `abstainer_h` drawn from its own stream, so the abstainer set is **the same households in every scenario for a given seed**. A test runs two scenarios on one seed and asserts the sets are identical.
- [ ] `θ_h` drawn from its own stream and set to 0 for abstainers in **every** scenario.
- [ ] **`age_h,g` drawn uniform over `{1 … life_g}`** for every durable, and a test asserts the replacement demand per tick is flat rather than clustered. (Was `{0 … life_g − 1}`; corrected in 04-04. Wants are asked before ageing and a unit is wanted at `age ≥ life`, so an age of `life` is due in tick 1 and an age of 1 in tick `life`. Over `{0 … life − 1}` nothing is due in tick 1 and every durable's series opens with a one-tick hole.)
- [ ] Households are index-addressed parallel arrays, not objects.
- [ ] θ is drawn even when `credit_enabled = false`, and doing so changes nothing else (01-05).

## Where to start

The uniform ageing criterion is small and its absence is spectacular. Initialise every durable at
age zero and every household replaces its appliances in the same month for the life of the run. The
output is a clean sawtooth with period `life`, it looks exactly like a business cycle, and there is
nothing in the model that should produce one.

Drawing θ even when credit is off is the concrete form of the seam in 01-05: the draw has to happen
in the same place in the same stream in both scenarios, or `credit_off` and `credit_high` sit on
different random worlds and the paired comparison is worthless. It costs one draw per household.

Same reasoning for the abstainer set. The finding is a difference between two runs for the same
twenty per cent of households; if the membership moves, the difference includes a composition
change nobody can decompose.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DrawTests
```

The mean-convergence assertion, the identical abstainer sets across scenarios, and the flat
replacement profile over the first `life` ticks.
