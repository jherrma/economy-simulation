# Verification — v1

**There is no reference implementation to diff against.** A wrong economy does not crash; it
produces plausible numbers. No test can assert "this economy is incorrect", so verification rests
on invariants that must hold for reasons *outside* the behaviour being tested.

Six devices. All of them are cheap, all of them run in seconds, and none may be weakened to make
something pass.

| | Device | What it catches |
|---|---|---|
| **V1** | Money conservation | Almost every accounting bug, on the tick it is made |
| **V2** | Bit-identical determinism | Order dependence, uninitialised state, shared mutable state |
| **V3** | Nominal neutrality | Money illusion hidden inside the decision rule |
| **V4** | The null run | Secular drift that would be misread as a result |
| **V5** | Credit-off regression | A new mechanism disturbing something it had no business touching |
| **V6** | Bounds and sanity | Off-by-one and sign errors in the walk |

---

## V1 — Money conservation

```
Σ cash_h + pool == M0 + loans_outstanding
```

To the cent, at the end of **every** tick, in every scenario, at both settings of
`money_creation`. A violation **halts the run**; it does not warn and it does not log.

This is the single most valuable test in the project. It is trivially cheap, it is independent of
whether the economics are right, and every bug that creates or destroys money — a purchase debited
but not credited, an instalment counted twice, principal destroyed on the wrong side of a repayment
— trips it immediately rather than a hundred ticks later.

Two things make it work, and both are easy to get wrong:

- **Interest is a transfer, not a destruction.** Only the principal component of an instalment
  reduces the money stock. If interest is destroyed too, V1 fails within a tick of the first
  repayment — which is the correct outcome, and the reason to run V1 from the very first commit.
- **Money in flight counts.** Every euro is in exactly one of two places at the end of a tick,
  a household or the pool. If the implementation has a third place — an "in transit" buffer, a
  seller's till — it belongs on the left-hand side.

Also assert every tick: `pool ≥ 0`, `cash_h ≥ 0` for all `h`, `stock_g ≥ 0` for all `g`.

A negative pool is not a bug in the code but in the parameters: income exceeds what the economy can
pay. Halt and report it as a result about the calibration.

## V2 — Bit-identical determinism

Three assertions, in increasing order of strength:

1. The same seed, run twice, produces byte-identical output files.
2. The same seed, run serially and across threads, produces byte-identical output files. Not
   "close" — a floating-point difference here means an accumulation is order-dependent and it will
   drift over 360 ticks.
3. **Registering a new random stream and never drawing from it changes nothing.** A test adds a
   purpose string, consumes no values, and requires byte-identical output.

The third is the one that pays for itself later. Streams derive from
`hash(run_seed, household_id, purpose)`, never from one advancing generator, so a mechanism added
next month cannot shift the draws made by this month's model. Without it, no two versions of the
simulation can be compared on the same seed, and the paired-seed protocol in §9 is worthless.

Pin the generator explicitly in the code. A runtime that changes its PRNG between versions would
silently invalidate every stored result.

## V3 — Nominal neutrality

Multiply **simultaneously** by any constant `c > 0`: `M0`, all cash, the pool, all opening prices,
all incomes, `price_floor`, `a_g`, and all outstanding loan principals. Leave `loan_rate`, `λ`,
`b_g`, `v_g`, `necessity_g`, `k` and every share and multiplier alone — they are pure numbers.

Nearly all of those are **derived**, so a gate sets three parameters — `mean_income`, each
category's `price_ref`, and `price_floor` — and the rest follow. `a_g` follows too, and that is the
sharpest trap here: `a_g = necessity_g · v_g · mean_income` is a floor in euros per tick and must
scale, while `b_g` is a coefficient on income and must not. Scaling `b_g` as well produces a
*near*-neutral result, which is worse than an obviously broken one because it reads as noise.

**Nothing real may change.** This holds by construction, and it is worth understanding why rather
than trusting it: `flow_value` scales with income, `flow_cost` scales with price, so `score` is
homogeneous of degree zero and λ never needs indexing. Affordability compares two quantities that
both scale. This is the defect that took the longest to find in the draft model — a value in utility
units divided by a cost in euros silently pinned the real price level to the utility scale, so
doubling all prices halved every score while the threshold stood still, and demand collapsed for no
economic reason.

Run V3 with `c = 2` and `c = 0.5`, both scenarios.

> **Amended 2026-09-03, on evidence.** V3 is **not** a byte comparison, and it cannot be one.
>
> Money is integer cents and `round(c · x) ≠ c · round(x)` for about half of all `x` — at `c = 2` as
> much as at `c = 0.5` — so the two runs' household incomes differ by up to half a cent each. This
> model amplifies that: a household spends down to nearly nothing every tick, whether the last
> increment it reaches for costs one cent more than it holds is a genuine knife edge, and one
> household landing on budget instead of standard takes the last unit of a rationed shelf from
> someone else. Measured: identical for the first three or four ticks, then about 48% of every real
> cell over 360 ticks. A control that adds **one cent to `mean_income` and scales nothing** produces
> the same thing (`01-SIMULATION.md` §10.2). Requiring tick-by-tick identity would be requiring the
> model not to be what it is, and any tolerance loose enough to permit it would be loose enough to
> hide the failure V3 exists for.
>
> What neutrality says is that the **equilibrium** is unchanged, so that is what is compared: the
> mean of every series over the measured window, paired by seed, over the seed set. Three rules,
> and none of them may be relaxed:
>
> - **A series is held to nothing until it is measured well enough to be held to something.** Its
>   window mean varies from seed to seed; that spread is what the series resolves. A shelf selling
>   one unit a fortnight resolves nothing and the open-wait median resolves a quarter of itself
>   (§V4). Both are still measured and reported — the gate says which series it tested and which it
>   could not — but a series is only required to agree when it resolves better than a tenth of a
>   percent. On eight seeds that is 43 of 271 series, and it includes every price index, every
>   category's sales and the tier mix.
> - **The bar is the control, not a number chosen for the purpose.** The one-cent run is the
>   smallest *real* change this economy can express, and it moves the well-measured series by two or
>   three parts in a thousand. A scaled run may move a series no more than twice as far as the
>   control moves that same series — and never more than 0.2% whatever the control says, so a
>   control that has itself gone wrong cannot license anything.
> - **Labels and file shapes are exact.** The scenario, the category and the tier are structure, not
>   measurement. Nothing rounds them and nothing may move them.
>
> The parameters the gate *sets* must still scale exactly, and this is checked before anything runs:
> if `mean_income` had itself been rounded away from `c` times the original, the gate would be
> measuring its own arithmetic and reporting it as the model's. Derived quantities that cannot scale
> exactly are named rather than required — `a_g` for clothing is 3575 cents and half of that is not
> a whole number of cents.
>
> **`price_floor` is the likeliest first failure and the hardest to notice.** It is the one nominal
> parameter that does not look like a price: it is a guard against dividing by zero, it is written
> once, and at one euro against prices of one to sixteen hundred it never binds — so leaving it in
> old money changes nothing at all until a scenario prices a shelf near it. It is tested where it
> binds, which is the only configuration in which the mistake has a consequence.

## V4 — The null run

With `credit_enabled = false`, prices must be **stationary after the warm-up**: the CPI over the
measured window has no significant trend, and no tier price drifts.

**The realised tier mix must also be stationary.** The opening unit shares (40/40/20) are
deliberately not an equilibrium, and the warm-up exists mainly so relative tier prices can find one.
A mix still drifting at the end of warm-up means the measured window is contaminated by the
transient, and the symptom is easy to miss because the CPI can look flat while the mix underneath it
is still moving.

**The pool must have stopped falling.** It drains during the transient by design and carries
twenty-four months of income for that reason. A pool still declining at the end of warm-up is the
same failure seen from the money side, and it is the cheaper of the two to check.

Supply is fixed, income is fixed, and the money stock is constant, so there is nothing in this model
that should make the price level move. If it drifts, the price rule is not converging, and any
credit effect measured later would be that drift plus an unknown amount of signal.

Also check the warm-up actually decayed: the warm-up ticks are written and flagged, not discarded,
so that the transient can be inspected rather than assumed.

> **Amended 2026-09-03.** Before the reservation price on money (`01-SIMULATION.md` §5.3) the pool
> fell by a fifth of income a tick, structurally. With it, a residual of about 1% of income a tick
> remains and is hoarding by the top decile alone, which fixed incomes with one unit per category
> cannot avoid. The criterion is therefore: over the measured window the pool falls by **no more
> than 2% of total income per tick**, the pool never falls below zero, and the cash of deciles one
> to nine shows no trend. A drain above that, or one that is not confined to the top decile, is the
> failure this gate exists for. Measured on the current defaults, the worst of thirty seeds sits at
> **1.94%** — inside the bound and not far inside it.

> **Amended 2026-09-03, on evidence: stationarity is measured across seeds, not within a run.**
>
> A single seed's price series wanders and does not stop. The repricing rule moves a shelf by up to
> `k` a tick on its own excess demand, and on a thin shelf — appliances produce ten units a tick,
> two of them premium — a demand of nought or four against a stock of two is an ordinary tick, so
> the price takes a five per cent step in an arbitrary direction. Over a measured window that is a
> random walk with a spread of tens of per cent, and it is **not drift**: it averages to nothing
> across seeds and it is what §10.2 says the paired comparison has to live with. Secular drift does
> not average away, because a rule that is not converging pushes every seed the same way. So V4 is
> run over the **full seed set** and tests the mean.
>
> Three rules follow, and they are the same three V3 arrived at independently:
>
> - **Each series is held to the looser of a stated floor and what the seed set can resolve** —
>   three standard errors of its own drift across seeds. The floor is 1% of the level over the
>   window for a price, 2% for a mix share or for the lower deciles' cash. A bound below the
>   standard error asks thirty seeds to measure something they cannot.
> - **The band is checked on the CPI and nowhere else.** A trend test alone passes an oscillation,
>   which has no trend at all, and an oscillating price rule is what a badly chosen `k` produces.
>   But a single shelf's band cannot tell oscillation from ordinary wandering: on the defaults a
>   shelf's band runs to 27% while the CPI's is 2%, and at `k = 0.9` the CPI's is 120%. The bound is
>   20% on the CPI — an order of magnitude above the one and six times below the other. *(The
>   obvious alternative, the lag-one autocorrelation of price changes, was tried and rejected on
>   measurement: at `k = 0.9` the rule saturates its own clamp for runs of ticks, so successive
>   changes become **more** persistent, not less. It fires on the baseline and not on the failure.)*
> - **The settling tick is reported, never failed on.** Whether the measured window is inside a
>   transient is answered by the drift tests, which have the seed set behind them; the settling tick
>   is the number that says what to raise `warmup_ticks` *to*. It is also the less trustworthy of
>   the two, because the thinnest shelves have no identified level at all and their settling tick
>   moves with the length of the run (§10.3).
>
> This gate is what set `warmup_ticks = 240`, `ticks = 600` and `opening_pool_months = 24`. At the
> previous values it failed, on a −4.1% drift in the leisure budget price at thirteen standard
> errors — see `01-SIMULATION.md` §10.3, which is the whole point of running V4 before anything is
> compared to the baseline.

**`wait_median` is exempt, and must be.** It mixes a flow of freshly-opened wants against a growing
stock of wants that are never met, so it drifts and swings several-fold from tick to tick in a
perfectly stationary economy (`01-SIMULATION.md` §10.1). Requiring it to be flat would fail the gate
on arithmetic. Its stationary counterpart, `wait_median_met`, is the one a stationarity check may
use — it is zero throughout the baseline, because a household that gets served is served at once.

## V5 — Credit-off regression

`credit_high` with every `θ` forced to zero must reproduce `credit_off` **byte-for-byte on the same
seeds**.

This does two jobs, and the second is the more valuable:

- As a regression test, it says the credit machinery does not perturb the model when it is switched
  off — which, combined with V2's unused-stream test, means the θ draws themselves cost nothing.
- As a **measurement instrument**, it makes the difference between credit-on and credit-off the
  attributed effect of exactly one mechanism, on paired seeds, with everything else held bit-identical.

Every mechanism added after v1 inherits this requirement: it arrives behind a switch, and *off*
reproduces the previous version byte-for-byte. A mechanism that cannot be switched off is one whose
contribution cannot be measured.

### V5a — Archetypes off, by table rather than by switch — added 2026-09-04

The **identity archetype table** — one type, every `w` and every `kappa` at 1.0 — must reproduce a
v1 run byte-for-byte on the same seeds, with `sigma_idio = 0`. This is the §5.4 mechanism's V5
clause, and it is discharged by *data* rather than by a boolean, so there are two things to check
and not one:

- The identity table reproduces v1. That is the regression half.
- The `"archetype"` and `"taste_idio"` streams are drawn **even under the identity table**, and
  drawing them changes nothing — V2's unused-stream test, applied to the two new purposes. If the
  draws were skipped when the table looks trivial, a typed scenario and its own `credit_off` would
  sit on different random worlds and the pairing would be worthless while still looking paired.

The paired-seed check in the campaign runner (09-02) therefore compares **archetype assignment**
across arms as well as the abstainer set, and compares the assignment itself rather than the count
of each type — equal counts are not the same claim as the same households.

**The projection was relaxed on 2026-09-04, and it is worth saying what was given up.** Through E10
the gate compared `run.csv` and `tiers.csv` whole, byte for byte, on the ground that E10 added no
column to either and that adding one would be the failure the gate exists for. E11 adds columns to
both on purpose — `tiers.csv` gains `good` and `category_mix_share`, `run.csv` gains a
`cpi_category_*` block — so both moved onto the shared-columns rule, where every column the fixture
knows is still compared to the character and a new one is named and skipped. A column **added** to
`run.csv` is therefore no longer caught here. A column that **disappears** still is, and explicitly:
comparing on shared columns would otherwise let the output shrink one column at a time until the
gate compares four keys and passes. The fixture itself is unchanged and is still never regenerated.
When 11-01 landed, that comparison reported six new columns in `run.csv` and two in `tiers.csv`, and
every other number identical across both arms and four seeds — which is the evidence that E11's
output change is additive.

### V5b — The grouped calibration is a configuration — added 2026-09-04

`01-SIMULATION.md` §5.5 needs no switch either, for a different reason from V5a's: the eighteen-good
table is a **configuration file**, so the default configuration still runs §3.1's six categories and
V5 keeps its meaning untouched. What must be checked instead is that the *engine* gained nothing
category-shaped:

- The goods table's length is read from the configuration everywhere. A test loads a table of a
  length in neither six nor eighteen and runs a tick. **Done 2026-09-04** (11-01): eleven goods with
  lives 1, 1, 2, 3, 4, 5, 7, 11, 19, 37, 61 across three category labels.
- **The table replaces rather than merges when it says so.** `replace = true` is what makes an
  eighteen-good file eighteen goods; without it the same file is twenty-four and costs twice the
  mean income per tick. A test asserts both numbers, because the failure is a run that starts.
- **Both identities.** `Σ_A share_A / d[A][g] = 1` for units, and
  `Σ_A share_A · m[A][g] = 1` for the score level, asserted at load on the normalised tables.
  The first is the expensive error of the epic: normalising `d` rather than `1/d` gives the
  population permanently more replacement demand than capacity was sized for, by an amount nothing
  else measures. The second is what stops a replacement cycle silently cancelling the taste it was
  meant to accompany — `ŵ = m / d` is derived, so a test must assert the identity on `m` and **not**
  on `ŵ`, whose share-weighted mean is not 1 and is not meant to be.
- **The hazard reproduces the deterministic rule in the mean.** Over a long unconstrained run,
  realised replacement demand per tick per good matches `capacity_g` within sampling error, and
  the `"failure"` draw is taken for every household, good and tick regardless of ownership. The
  second half is what makes the arms comparable and it is invisible in any single run.
- `d ≡ 1` reproduces the same calibration run without `d`, byte for byte.
- **`capacity` is derived from `households` and `life`, never from the realised population.**
  A test asserts two seeds of one scenario produce identical effective configurations — which is
  also what the campaign collector refuses to proceed without (09-02).

What V5b cannot say is that the grouped calibration is *right*. Nothing here can. It is defended by
being checkable against observable prices and cycles, which is precisely what §3.1's blob was not.

## V6 — Bounds and sanity

Cheap assertions that catch the errors the walk is prone to:

- `sold_(g,t) ≤ units_(g,t)`, every tick, every tier.
- `D_(g,t) = sold_(g,t) + blocked_(g,t)`, and `unaffordable` appears in **neither**. Assert this
  explicitly: folding unaffordable demand into `D` is the single most likely modelling mistake in
  the implementation, and it is silent.
- A household ends the tick holding **at most one unit per category**, and consumes stock at exactly
  one tier of it.
- **Increments sum to the tier price.** A household that reaches standard has paid
  `0.60·P + 0.40·P = 1.00·P`; one that reaches premium has paid `1.80·P`. Assert to the cent — this
  is what makes the upgrade-candidate model exact rather than approximate.
- **Upgrade scores are monotone decreasing** within a category: budget > budget→standard >
  standard→premium, for every household and every price vector. If this ever fails, `value_mult`
  has been set above `price_mult` somewhere and quality has stopped having diminishing returns.
  With archetypes (`01-SIMULATION.md` §5.4) this is per household *and* per category, because
  `kappa` differs by type; the load-time bound on `kappa` is what makes the assertion unreachable
  rather than merely checked. Derive that bound from the tier table and never write the number down
  in the code, or changing a tier multiplier will move the bound without moving the guard.
  **Derive it by searching, not by the closed form.** `ln(price_mult_budget) /
  ln(value_mult_budget)` = 1.3245 is the bound at the *v1* tiers only, because there it is the first
  ordering condition that binds. Verified 2026-09-04: move `value_mult_budget` from 0.68 to 0.75 and
  that condition relaxes to 1.7757 while the second — `budget → standard` above
  `standard → premium` — takes over at 1.7073. *Which* condition binds is itself a property of the
  tier table, so the bound is the smallest `kappa ≥ 1` at which the opening ladder stops being
  ordered, found by bisection over the whole ordering predicate.
- An upgrade is never taken without the step below it.
- `blocked_(g,t) > 0` implies that tier had no stock at the end of the tick.
- Every wanted durable that is bought resets `age = 0`; nothing else does.
- Sum over households of what was paid equals the pool's increase from goods.
- With `credit_enabled = false`: `loans_outstanding == 0` at every tick.
- `Σ_t units_(g,t) == capacity_g` at initialisation, to within the rounding of the unit split.

---

## What a wrong run looks like

None of the following crashes anything. Each has been chosen because it produces a *plausible*
series, which is why it needs a named check rather than a reviewer's judgement.

| Symptom | Likely cause |
|---|---|
| Prices rise monotonically and never settle | `unaffordable` counted as demand — the price rises on goods nobody can buy, which makes more households unable to buy them |
| Prices never rise even under `credit_high` | `blocked` not counted, so demand can never exceed supply and the rule only ever sees surplus |
| A clean sawtooth in durable sales, period = `life` | Durable ages initialised to zero instead of uniform over `[0, life)`. Everyone replaces in the same month. It looks like a business cycle and it is an artefact |
| Money stock creeps down under `credit_high` | Interest destroyed along with principal instead of redistributed |
| The abstainer effect is enormous and grows without limit | Instalments not deducted before shopping, so credit is free money and the result is arithmetic |
| The credit effect vanishes entirely | Financed score not checked against λ, or the residual test using income instead of income minus running debt service |
| Cohort comparison is noisy and inconsistent between seeds | Abstainers not the same households across scenarios, so the comparison is not paired |
| Results change when thread count changes | A shared generator somewhere. V2 assertion 2 |
| Every household buys budget everything, premium never sells | Candidates ranked by *total* score per tier instead of *incremental* score. Ratio always favours the cheapest tier; only the increment can justify an upgrade |
| Nobody ever buys budget; the ladder collapses to standard/premium | The tier below not being required before an upgrade is available |
| Poor households buy nothing at all, including food | `a_g` missing — value proportional to income makes every good a luxury |
| The pool drains steadily and the run halts around tick 11 | Opening pool sized at one month instead of twelve. The opening tier mix is not an equilibrium and the transient has to be survivable |
| Replacement demand quietly exceeds capacity for every good with a varied cycle, in every run | `d` normalised arithmetically instead of harmonically (`02-PARAMETERS.md` §3.7). `E[1/d] > 1/E[d]` whenever `d` varies, so the town wants more units per tick than it was sized for. Nothing else measures it and the price level absorbs it silently |
| Premium price series for the long-lived goods are noise with a trend drawn through them | Shelves too thin. At 1,000 households the grouped calibration gives one premium large appliance per tick. Raise `households`, do not smooth the series |
| The campaign refuses to collect: two seeds of one scenario ran different configurations | `capacity` derived from the realised archetype assignment rather than from `households / life`. The seed has become a parameter |
| The share-weighted column mean of `w` in the **effective configuration** is not 1.000 | The archetype columns not normalised (`02-PARAMETERS.md` §3.5). Check it there, not in the output: a correctly normalised table still differs from the untyped baseline in every category, because wants do not depend on `w` and the tier decision is a threshold, so `E[w] = 1` conserves no observable quantity. "The typed run differs everywhere" is **not** a symptom of this bug and an earlier draft of this table wrongly said it was |
| Households stop buying a category entirely once `kappa` is raised | `kappa` above the ladder bound. Buying budget now scores below upgrading to standard, the walk's stop rule fires on the budget candidate, and the standard unit the household would have bought is unreachable because the step below it was never taken |
| A typed `credit_high` and a typed `credit_off` disagree about which households exist | Archetype assignment drawn from a stream that some other draw perturbs, or skipped under the identity table. V5a |
| Tier shares are constant across every scenario | Tier prices repriced on a category-wide signal instead of per tier, so relative prices cannot move and the mix cannot clear |

## Running order

1. V1 and V6 from the first commit that moves money. They are assertions inside the tick, not a
   separate suite.
2. V2 as soon as there is output to compare.
3. V4 as soon as the price rule exists — before credit, because a drifting baseline makes every
   later number meaningless.
4. V3 before the first result is quoted anywhere.
5. V5 the moment credit is added, and for every mechanism added after it.
