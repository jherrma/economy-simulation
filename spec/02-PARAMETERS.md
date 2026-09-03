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
| `ticks` | 360 | One tick = one month; 30 years |
| `warmup_ticks` | 120 | Written to output but flagged, never silently discarded. Long because the opening tier prices are deliberately not an equilibrium (§3.3) |
| `seeds` | 30 | The same 30 in every scenario, compared **paired** |
| `scenario` | `"credit_off"` | A label for the output only — no part of the model reads it, and setting it changes no draw. Every CSV row carries it, so the files of hundreds of runs concatenate without ambiguity (`spec/stories/07-01`) |

## 2. Income

```
income_h = mean_income · exp(σ_income · z_h − σ_income² / 2)      z_h ~ N(0,1)
```

The `− σ²/2` term makes the mean exactly `mean_income` rather than approximately.

| Parameter | Default | Why |
|---|---|---|
| `mean_income` | 650.00 € | **Discretionary** income per month, after housing, transport, insurance, health and tax — none of which exist in this model. A German mean household nets ~€3,300 with a Mietbelastungsquote near 28%; €650 for these six categories is the right order |
| `σ_income` | 0.35 | Lognormal spread. Wide enough that the tier ladder in §3.2 is populated at both ends |
| `opening_cash_share` | 1.00 | Opening cash = one month of income |

Income is **fixed nominal for the life of the run**. There are no wages and no firms, so nothing can
change it. This makes the abstainer's real-income loss an upper bound (`01-SIMULATION.md` §11).

## 3. Goods

Six categories, each sold in **three quality tiers**. A household buys at most one unit of a
category, and the tier is its choice.

### 3.1 Categories

| Category | `life` | `capacity` | `price_ref` | `v` | `necessity` | financeable | `term` |
|---|---|---|---|---|---|---|---|
| Food | 1 | 1000 | 300 € | 0.620 | 0.70 | no | — |
| Leisure | 1 | 1000 | 200 € | 0.400 | 0.35 | no | — |
| Clothing | 6 | 167 | 400 € | 0.110 | 0.50 | no | — |
| Hobby items | 12 | 83 | 600 € | 0.088 | 0.15 | **yes** | 12 |
| Consumer electronics | 36 | 28 | 900 € | 0.048 | 0.25 | **yes** | 24 |
| Appliances | 96 | 10 | 800 € | 0.016 | 0.45 | **yes** | 24 |

`capacity` is `round(households / life)` — **the steady-state replacement demand in units**, the
only non-arbitrary way to size it. It is therefore **derived, not chosen**: a configuration that
omits it gets it computed, and a configuration that states it must state the value the identity
gives, or the run is rejected. This is what lets `households` be changed in a scenario without
six other numbers having to be recomputed by hand. `price_ref` is the standard tier's opening price; the other two
are derived from it.

`necessity_g` splits `v_g` into a part that does not scale with income and a part that does:

```
a_g = necessity_g · v_g · mean_income          floor, euros per tick
b_g = (1 − necessity_g) · v_g                  income-linked
flow_value(h, g) = (a_g + b_g · income_h) · w_h · value_mult(tier)
```

The split is **exactly neutral at the mean income** — `a_g + b_g · 650 = v_g · 650` — so it changes
the income gradient of demand and nothing else. Without it, willingness to pay is proportional to
income and *food becomes a luxury*: a household on €450 scores food at 0.93 and buys none. Engel's
law is not decoration here; without it the poor end of the distribution starves and the run is
invalid in a way no invariant would catch.

| Category | `a_g` | `b_g` | `flow_value` at €650 |
|---|---|---|---|
| Food | 282.10 | 0.1860 | 403.00 |
| Leisure | 91.00 | 0.2600 | 260.00 |
| Clothing | 35.75 | 0.0550 | 71.50 |
| Hobby items | 8.58 | 0.0748 | 57.20 |
| Consumer electronics | 7.80 | 0.0360 | 31.20 |
| Appliances | 4.68 | 0.0088 | 10.40 |

**Financeability** follows German point-of-sale practice: electronics, appliances and larger hobby
equipment are routinely sold on instalments; food, leisure and clothing are not. Clothing is a
useful control — a durable that cannot be financed.

### 3.2 Tiers

| Tier | `price_mult` | `value_mult` | Opening unit share |
|---|---|---|---|
| Budget | 0.60 | 0.68 | 0.40 |
| Standard | 1.00 | 1.00 | 0.40 |
| Premium | 1.80 | 1.40 | 0.20 |

**`value_mult` rises more slowly than `price_mult`.** That single property is what makes quality
behave like quality: each step up is worth having and each step up is worse value for money than the
one below it. Diminishing returns are a consequence, not a parameter.

The resulting multipliers on a category's base score are uniform across categories:

| Candidate | Multiplier | Derivation |
|---|---|---|
| Buy budget | **1.133** | `0.68 / 0.60` |
| Upgrade budget → standard | **0.800** | `(1.00 − 0.68) / (1.00 − 0.60)` |
| Upgrade standard → premium | **0.500** | `(1.40 − 1.00) / (1.80 − 1.00)` = `0.40 / 0.80` |

Units per tier are the category's capacity split by the **unit** share:

```
units(g, t) = round(unit_share_t · capacity_g)
```

Implemented by largest remainder rather than by rounding each share on its own. The two agree on
every value in this table; largest remainder also **guarantees** `Σ_t units(g,t) = capacity_g`,
which plain rounding does not, and a residue there is a unit of supply appearing from nowhere every
tick for thirty years.

Splitting units rather than value is what keeps the calibration exact. Total units stay at
`capacity_g`, so unit demand and unit supply match by construction; and because
`Σ_t unit_share_t · price_mult_t = 0.24 + 0.40 + 0.36 = 1.000`, the **capacity value is unchanged by
the tier system**:

```
Σ_g Σ_t units(g,t) · price_ref_g · price_mult_t  =  650,240 €
households × mean_income                         =  650,000 €     ✓
```

That identity is the reason the pool does not drain in equilibrium: whenever the market clears at
any tier mix, nominal output equals nominal income — **at opening prices**. Once tiers have
repriced independently the identity no longer holds, and nothing restores it; see
`01-SIMULATION.md` §7.2 (2026-09-03).

### 3.3 The opening tier mix is not an equilibrium

At opening prices, aggregate desired spending is about **23% below income**, and the premium tiers
are in heavy surplus — most households want budget or standard, while supply is 40/40/20. This is
deliberate and it is **not** to be calibrated away.

The tier mix is an **output**. Premium prices fall until enough households upgrade to clear the
premium shelf; budget prices rise where budget demand exceeds its 40%. The mix that emerges is the
one the price mechanism finds, and *pinning it by choosing the unit shares to match opening demand
would be assuming the answer*, since the whole question is how credit shifts that mix.

Two consequences: `warmup_ticks` is 120 rather than 60, and the pool carries twelve months of income
rather than one (§7), because the transient drains it before it converges.

### 3.4 The calibration, in full

```
flow cost per household per tick, at the standard tier
  300 + 200 + 400/6 + 600/12 + 900/36 + 800/96                        = 650.00 = mean_income ✓

capacity value per tick (any tier mix, by §3.2's identity)            = 650,240
households × mean_income                                              = 650,000 ✓

ceiling on desired spend (every household at premium): 1.80 × 650     = 1,170.00
floor  (every household at budget):                    0.60 × 650     =   390.00
```

The tier system is what gives income an outlet. Before it, every household — on €300 or €3,000 —
faced the same €650 basket, so the top of the distribution accumulated cash that could never be
spent and the pool drained without limit. The ladder that emerges:

| Income | Food | Leisure | Clothing | Hobby | Electronics | Appliances | Spend |
|---|---|---|---|---|---|---|---|
| 300 | budget | — | — | — | — | — | 180.00 |
| 450 | budget | budget | budget | — | budget | budget | 360.00 |
| 650 | standard | standard | budget | budget | budget | budget | 590.00 |
| 900 | standard | standard | standard | standard | standard | standard | 650.00 |
| 1500 | standard | premium | standard | premium | premium | premium | 876.67 |
| 3000 | premium | premium | premium | premium | premium | premium | 1170.00 |

Read at the median (€650, `w = 1`), by candidate:

| Category | base score | budget | → standard | → premium | chosen |
|---|---|---|---|---|---|
| Food | 1.343 | 1.522 | **1.075** | 0.672 | standard |
| Leisure | 1.300 | 1.473 | **1.040** | 0.650 | standard |
| Consumer electronics | 1.248 | **1.414** | 0.998 | 0.624 | budget |
| Appliances | 1.248 | **1.414** | 0.998 | 0.624 | budget |
| Hobby items | 1.144 | **1.297** | 0.915 | 0.572 | budget |
| Clothing | 1.073 | **1.216** | 0.858 | 0.536 | budget |

Three design requirements are encoded here, and a parameter change that breaks any of them
invalidates the run:

1. **Essentials outrank durables where the budget binds.** At the median and below, food's base
   score is above every durable's. It is *not* true at every income: because food's value is
   mostly floor (`necessity = 0.70`) it grows slowly with income, while a durable's grows fast,
   so the base scores cross — electronics overtakes food at about €770, appliances and hobby
   items at about €870, clothing near €1,970. Roughly a quarter of households sit above the first
   crossing. For them the walk can take a durable increment before food, and if cash then falls
   short of food's budget increment the household skips a month of food; food's `unaffordable`
   counter is where that would show. (Corrected 2026-09-03 while implementing 04-03: the original
   text claimed the ordering at every income, and CandidateTests showed it fails from €766 upward.)
2. **The median sits mid-ladder**, with upgrades to standard hovering around 1.00 for the durables.
   That is where the model is most responsive: a small price move flips a tier choice, which is
   exactly the margin credit is expected to act on.
3. **The premium step never clears at the median** (0.54–0.67), so premium is bought by the upper
   part of the distribution and by anyone credit lifts there.

## 4. The decision

| Parameter | Default | Why |
|---|---|---|
| `lambda` (λ) | 1.00 | Take a candidate worth at least what it costs. A pure number, never indexed |
| `σ_w` | 0.20 | Spread of the household taste multiplier `w_h`, mean exactly 1 |
| `subsistence_share` | 0.55 | Protected from the credit residual test: `0.55 × 650 = €357.50`, roughly food plus a minimum of leisure |
| `buffer_months` (φ) | 2.0 | The reservation price on money (`01-SIMULATION.md` §5.3): cash above φ months of own income lowers a household's λ in proportion, `λ_h = λ · min(1, φ / b_h)`. Below φ, λ is unchanged. This is what anchors the price level; 0 switches it off and the pool drains a fifth of income a tick (§7.2). Chosen over 1 and 3 on seeds 1–3: at 1 durables are widely cash-unaffordable, at 3 the pool dips to half and premium is still converging at tick 360 |
| `affordability_horizon` | `myopic` | The instalment must fit **this tick**. `full_term` is the control: the whole repayable amount must fit the residual instead (`01-SIMULATION.md` §6 step 4) |

## 5. Credit

| Parameter | Default | Why |
|---|---|---|
| `credit_enabled` | **false** | Default off, so the default configuration *is* the baseline |
| `loan_rate` | 8.0 %/a | German consumer instalment credit, nominal, simple interest |
| `money_creation` | true | Loans create deposits. `false` funds them from the pool instead, and the difference between the two is the money-creation channel measured directly |
| `theta_min` | 0.0 | Lower bound of the uniform `θ_h` draw |
| `theta_max` | 0.2 | Upper bound. The default pair is `theta_low`; `θ` is drawn even with credit off, and unused |
| `theta_low` | `U(0, 0.2)` | The `credit_low` scenario — `theta_min` / `theta_max` |
| `theta_high` | `U(0.4, 0.9)` | The `credit_high` scenario — `theta_min` / `theta_max` |

`theta_low` and `theta_high` are not separate parameters: they are the two scenario settings of
`theta_min` / `theta_max`. The default is `theta_low`, because `θ_h` is drawn in every scenario —
including the baseline, where it is never read (`01-SIMULATION.md` §5, and `spec/stories/01-05`). The
draw has to happen in the same place in the same stream in every scenario, or the paired comparison
is between two different random worlds.

Financing multiplies a candidate's cost by `(1 + loan_rate · term / 1200)` — **1.08** over twelve
months, **1.16** over twenty-four — so a financed candidate's score is the cash score divided by
that. Financing is therefore never free and never improves a ranking; what it does is put a tier
within reach that cash could not pay for.

Because the multiplier is uniform, financing an upgrade increment and financing the whole unit come
to the same total interest. The implementation may treat each candidate as its own small loan; the
arithmetic is identical and the bookkeeping is simpler.

Simple interest rather than an annuity: the difference is second-order for this question and the
arithmetic stays checkable by hand. Replace it with an annuity when arrears exist and the split
between principal and interest starts to matter.

Instalments at opening prices, standard tier: €54.00 for hobby items, €43.50 for electronics,
€38.67 for appliances. All three together are €136.17 against a median residual of
`(1 − 0.55) × 650 = €292.50`, so a household **can** carry all three at once. That is deliberate:
loan stacking under a myopic monthly-payment test is a mechanism to observe, not one to rule out by
calibration.

## 6. Prices

| Parameter | Default | Why |
|---|---|---|
| `k` | 0.05 | Adjustment speed, applied per **tier**. A 20% shortage moves that tier's price 1% next tick |
| `price_floor` | 1.00 € | Prevents a tier priced to zero from dividing by zero downstream. **Scales under the neutrality test** — a fixed floor would break it |
| `rationing` | `random` | First-come within a seeded random household order. `willingness` is a variant, expected to strengthen the result, and is reported separately |

Each of the eighteen tiers carries its own price and its own excess-demand signal. Relative tier
prices within a category are therefore free to move, and they must be — that movement *is* the
trade-down channel.

## 7. Money

| Quantity | Value | Derivation |
|---|---|---|
| Opening household cash | 650,000 € | `households × mean_income × opening_cash_share` |
| Opening pool | 7,800,000 € | **Twelve** months of total income. The warm-up transient (§3.3) drains it while prices find the tier mix; one month would halt the run around tick 11 |
| **`M0`** | **8,450,000 €** | The sum. Fixed for the run; only lending and repayment change the money stock |

The pool has no behaviour and its size is not economically meaningful — it is a buffer that makes
the transient survivable. Its **trajectory** is meaningful, and is a required diagnostic: a pool
still falling at the end of warm-up means prices have not converged and the measured window is
contaminated.
