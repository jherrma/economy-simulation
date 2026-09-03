# Parameters — v1

**A parameter not in this file does not exist.** Add it here, with a default and a justification,
before any code depends on it. The full model's appendix grew to seventeen parameters with no
empirical anchor; this one is small enough that every value below can be argued about individually.

Every parameter that gates a behaviour defaults to the setting that **disables** it, so the default
configuration is the baseline and a scenario file only ever names what it changes.

## 1. Population and run

| Parameter | Default | Why |
|---|---|---|
| `households` | 1000 | Large enough for cohort statistics, small enough to run in a second |
| `abstainer_share` | 0.20 | The measured cohort. `θ = 0` for these households in every scenario |
| `ticks` | 300 | One tick = one month; 25 years |
| `warmup_ticks` | 60 | Written to output but flagged, never silently discarded |
| `seeds` | 30 | The same 30 in every scenario, compared **paired** |

## 2. Income

```
income_h = mean_income · exp(σ_income · z_h − σ_income² / 2)      z_h ~ N(0,1)
```

The `− σ²/2` term makes the mean exactly `mean_income` rather than approximately.

| Parameter | Default | Why |
|---|---|---|
| `mean_income` | 650.00 € | **Discretionary** income per month, after housing, transport, insurance, health and tax — none of which exist in this model. A German mean household nets ~€3,300 with a Mietbelastungsquote near 28%; €650 for these six categories is the right order |
| `σ_income` | 0.35 | Lognormal spread. Wide enough that some households are cash-constrained at the moment they want a durable, which is the situation credit exists for |
| `opening_cash_share` | 1.00 | Opening cash = one month of income |

Income is **fixed nominal for the life of the run**. There are no wages and no firms, so nothing can
change it. This makes the abstainer's real-income loss an upper bound (`01-SIMULATION.md` §11).

## 3. Goods

| Good | `life` | `supply` | `price₀` | `v` | financeable | `term` |
|---|---|---|---|---|---|---|
| Food | 1 | 1000 | 300 € | 0.620 | no | — |
| Leisure | 1 | 1000 | 200 € | 0.400 | no | — |
| Clothing | 6 | 167 | 400 € | 0.110 | no | — |
| Hobby items | 12 | 83 | 600 € | 0.088 | **yes** | 12 |
| Consumer electronics | 36 | 28 | 900 € | 0.048 | **yes** | 24 |
| Appliances | 96 | 10 | 800 € | 0.016 | **yes** | 24 |

**`supply` is the rounded steady-state replacement demand**, `round(households / life)`. That is the
only non-arbitrary way to size it: at that level the economy can exactly replace what wears out, and
nothing else. Where the rounding went down — appliances, `1000/96 = 10.4` against a supply of 10 —
the good is structurally tight, and that is left alone rather than fudged. Chronic mild scarcity is
where the interesting behaviour lives, and the price rule resolves it by pricing the marginal
household out rather than by running an unbounded shortage.

**Financeability** follows German point-of-sale practice: electronics, appliances and larger hobby
equipment are routinely sold on instalments; food, leisure and clothing are not. Clothing is a
useful control — a durable that cannot be financed.

### 3.1 The calibration, in full

The parameters above are not independent. They are fixed by requiring that the **median household's
desired spending exactly equals its income**, so that the budget binds and scarcity is real:

```
non-durable flow cost   300 + 200                                    = 500.00
durable flow cost       400/6 + 600/12 + 900/36 + 800/96             = 150.00
                                                                       ------
total flow cost per household per tick                                 650.00  = mean_income ✓

economy-wide supply value per tick
  1000·300 + 1000·200 + 167·400 + 83·600 + 28·900 + 10·800           = 649,800
  households · mean_income                                           = 650,000 ✓
```

The `v` weights are then set so that every good is **worth having at its opening price** but only
just, and so that the ranking is plausible. Score at the median household (`income = 650`, `w = 1`):

| Good | `flow_value` | `flow_cost` | `score` cash | `score` financed |
|---|---|---|---|---|
| Food | 403.00 | 300.00 | 1.343 | — |
| Leisure | 260.00 | 200.00 | 1.300 | — |
| Consumer electronics | 31.20 | 25.00 | 1.248 | **1.076** |
| Appliances | 10.40 | 8.33 | 1.248 | **1.076** |
| Hobby items | 57.20 | 50.00 | 1.144 | **1.059** |
| Clothing | 71.50 | 66.67 | 1.073 | — |

Three things to read off this table, each of which is a design requirement rather than an accident:

1. **Every score exceeds λ = 1**, so at opening prices the median household wants everything. Total
   desired value is `1.282 × income`, 28% more than it can afford, so the budget binds hard and
   prices have work to do.
2. **Essentials rank above durables.** Nobody finances a phone before buying food. If a later
   parameter change breaks that ordering, the walk will start skipping meals and the run is invalid.
3. **Every financed score still clears λ, but barely** — 1.06 to 1.08 against 1.14 to 1.25 for cash.
   Financing is worth doing and never free. This margin is deliberately thin: if it were wide,
   credit would be obviously correct for everyone and θ would be doing no work.

Instalments at opening prices are €54.00 for hobby items, €43.50 for electronics and €38.67 for
appliances. All three together are €136.17 against a median residual of `(1 − 0.55) × 650 = €292.50`,
so a household **can** carry all three at once. That is deliberate: loan stacking under a myopic
monthly-payment test is a mechanism to observe, not one to rule out by calibration.

## 4. The decision

| Parameter | Default | Why |
|---|---|---|
| `lambda` (λ) | 1.00 | Buy anything worth at least what it costs. A pure number, never indexed |
| `σ_w` | 0.20 | Spread of the household taste multiplier `w_h`, mean exactly 1 |
| `subsistence_share` | 0.55 | Protected from the credit residual test: `0.55 × 650 = €357.50`, roughly food plus a minimum of leisure |
| `affordability_horizon` | `myopic` | The instalment must fit **this tick**. `full_term` is the control |

## 5. Credit

| Parameter | Default | Why |
|---|---|---|
| `credit_enabled` | **false** | Default off, so the default configuration *is* the baseline |
| `loan_rate` | 8.0 %/a | German consumer instalment credit, nominal, simple interest. `interest = principal · rate/100 · term/12` |
| `money_creation` | true | Loans create deposits. `false` funds them from the pool instead, and the difference between the two is the money-creation channel measured directly |
| `theta_low` | `U(0, 0.2)` | The `credit_low` scenario |
| `theta_high` | `U(0.4, 0.9)` | The `credit_high` scenario |

Simple interest rather than an annuity: the difference is second-order for this question and the
arithmetic stays checkable by hand. Replace it with an annuity when arrears exist and the split
between principal and interest starts to matter.

## 6. Prices

| Parameter | Default | Why |
|---|---|---|
| `k` | 0.05 | Adjustment speed. A 20% shortage moves the price 1% next tick. Slow enough not to oscillate, fast enough to clear within a year |
| `price_floor` | 1.00 € | Prevents a good priced to zero from dividing by zero downstream. **Scales under the neutrality test** (`03-VERIFICATION.md`, V3) — a fixed floor would break it |
| `rationing` | `random` | First-come within a seeded random household order. `willingness` is a variant, expected to strengthen the result, and is reported separately |

## 7. Money

| Quantity | Value | Derivation |
|---|---|---|
| Opening household cash | 650,000 € | `households × mean_income × opening_cash_share` |
| Opening pool | 650,000 € | One tick of income, so step 1 can always pay |
| **`M0`** | **1,300,000 €** | The sum. Fixed for the run; only lending and repayment change the money stock |
