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
| `ticks` | 600 | One tick = one month; 50 years, of which the last 30 are measured |
| `warmup_ticks` | 240 | Written to output but flagged, never silently discarded. Long because the opening tier prices are deliberately not an equilibrium (§3.3), and **set on the null run's evidence**, not chosen: relative prices take about eight times as long to converge as the price level (`01-SIMULATION.md` §10.3) |
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

**This is the v1 calibration and remains the default.** §3.6 gives a second one — eighteen goods,
three product groups per category, each with its own replacement cycle — which is a configuration
rather than a replacement, so the findings in `01-SIMULATION.md` §10 keep the table they were
measured on.

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

Two consequences: `warmup_ticks` is 240 rather than 60, and the pool carries twenty-four months of
income rather than one (§7), because the transient drains it before it converges.

The 240 is measured rather than argued. V4 reports the tick each series settles at, and the answer is
that the CPI settles in about thirty ticks while **relative** prices take eight times as long: the
leisure budget shelf opens at €120 and converges to €108.3 with a time constant near seventy ticks.
At tick 120 it is still 4.5% above where it is going, which showed up over the old measured window as
a drift of −4.1% at thirteen standard errors across thirty seeds. See `01-SIMULATION.md` §10.3.

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

### 3.5 Archetypes — added 2026-09-04

The population is drawn into named **archetypes** (`01-SIMULATION.md` §5.4). Each carries a
population `share` and, per category, a relative taste weight `w` and a quality steepness `kappa`.

**The default table is a single type with every `w` and every `kappa` at 1.0.** That is v1 exactly,
so the default configuration is still the baseline and the byte-for-byte rule needs no switch. The
table below is the `typed` table: it lives in a scenario file, not in the defaults.

#### Parameters

| Parameter | Default | Why |
|---|---|---|
| `archetypes.<name>.share` | one type at 1.0 | Population share. The shares must sum to 1.0 or the run is rejected |
| `archetypes.<name>.w.<category>` | 1.0 | Relative **score multiplier** `m` — how much better or worse than average a candidate in this category scores for this type. **Absent means 1.0**, so a type names only the categories it differs in (§2 of `01-SIMULATION.md` on absent-versus-unknown: an absent key defaults, an unknown key is an error). Under §3.7 the taste weight `ŵ = m / d` is *derived* from it, because a shorter replacement cycle raises the cost per tick and would otherwise cancel the intent; where `d = 1` — every life-1 good, and the whole of this section — the two are the same number |
| `archetypes.<name>.kappa.<category>` | 1.0 | Quality steepness, applied as `value_mult(tier)^kappa`. Absent means 1.0 |
| `replacement` | **`deterministic`** | How a durable comes to be wanted again. `deterministic` is v1: `age_h,g ≥ life_g`, with ages drawn uniform at initialisation. `hazard` is `01-SIMULATION.md` §5.5: a per-tick failure probability of `1 / life_h,g`, no age state, no `"initial_age"` draw, and the only setting under which a non-integer life means anything. The grouped calibration requires `hazard`; a configuration that sets a `d` under `deterministic` is rejected rather than rounded |
| `sigma_idio` | **0.0** | Spread of the per-household, per-category residual `ε`. Zero by default, so taste is perfectly correlated across categories exactly as in v1. Raising it walks that correlation toward zero and is the sweep for "does it matter that the same households want everything" |

`w` is authored as a **relative** weight and normalised by the loader; see the identity below.
`kappa` is authored as an absolute and is not normalised.

#### The `typed` table

Four types, chosen to span the two axes that matter — how much of the budget a category gets, and
how far up its tiers the household goes — while staying few enough to argue about one at a time.

| | share | Food | Leisure | Clothing | Hobby | Electronics | Appliances |
|---|---|---|---|---|---|---|---|
| **`prudent`** `w` | 0.30 | 0.90 | 0.80 | 0.85 | 0.75 | 0.80 | 0.90 |
| `kappa` | | 0.85 | 0.85 | 0.85 | 0.85 | 0.85 | 0.85 |
| **`health_conscious`** `w` | 0.20 | 1.20 | 1.00 | 1.00 | 0.90 | 0.85 | 1.05 |
| `kappa` | | **1.25** | 0.95 | 0.95 | 0.95 | 0.95 | 1.10 |
| **`gadget`** `w` | 0.20 | 0.95 | 1.15 | 1.05 | **1.55** | **1.60** | 1.00 |
| `kappa` | | 0.95 | 1.05 | 0.95 | 1.20 | **1.25** | 0.95 |
| **`family_practical`** `w` | 0.30 | 1.05 | 1.00 | 1.15 | 0.95 | 1.00 | 1.10 |
| `kappa` | | 0.90 | 0.90 | 0.95 | 0.90 | 0.90 | 0.90 |

Read the two rows of a type together — that is the point of the split:

- **`prudent`** wants less of everything and, more sharply, cares less about quality everywhere
  (`kappa = 0.85`): a fridge is a fridge. It is not the poor type — income is drawn independently —
  it is the type for whom the budget tier is genuinely good enough.
- **`health_conscious`** is the case that motivated the split. `w_food = 1.20` is modest; the
  `kappa_food = 1.25` is what does the work: it buys the best food, not more food. Its kitchen
  follows (`kappa` 1.10 on appliances) and its electronics do not.
- **`gadget`** wants far more hobby and electronics (`w` 1.55, 1.60) *and* wants them good
  (`kappa` 1.20, 1.25). It is the type credit is most useful to, and the one whose bidding the
  abstainer feels, because both of its categories are financeable.
- **`family_practical`** wants more clothing and appliances — more wear, more washing — and wants
  them **durable rather than fine**, which is `kappa = 0.90` on both. It is the counterweight to
  `gadget`: high demand that does not chase the premium tier.

`kappa` is nowhere above **1.25**, against the bound of 1.3245 derived in `01-SIMULATION.md` §5.4.
That reads as deliberate headroom and mostly is not: the bound holds at *opening* prices, and at
κ = 1.25 the ladder inverts as soon as the budget shelf becomes 2.9% dearer relative to standard —
a move §10.3 has already observed several times over. See §5.4 for the table. Treat 1.25 as the
value to justify or lower, not as a safe margin.

#### The taste identity, and what the loader does with it

For every category, the share-weighted mean of the **score multiplier** must be 1:

```
Σ_A share_A · m_g,A = 1                       equivalently  Σ_A share_A · ŵ_g,A · d_g,A = 1
```

The table redistributes a category's demand across the population; it does not change how much of it
there is. `v_g` (§3.1) is the parameter for that, and `base_score` (§3.6) is what the identity keeps
meaning what it says at the population mean. With `d ≡ 1` this is the plain `Σ share · ŵ = 1` and
nothing in this section changes.

The authored weights above do not satisfy the identity exactly, and they are not meant to — writing
a table that does by hand is a pointless arithmetic exercise that hides the intent. **The loader
divides each column by its share-weighted mean** and records the result in the effective
configuration, which is what the campaign manifest hashes (09-02). The column scales and the
normalised table:

```
column scale     Food 1.0150   Leisure 0.9700   Clothing 1.0100
                 Hobby 1.0000  Electronics 1.0300   Appliances 1.0100
```

| normalised `w` | Food | Leisure | Clothing | Hobby | Electronics | Appliances |
|---|---|---|---|---|---|---|
| `prudent` | 0.8867 | 0.8247 | 0.8416 | 0.7500 | 0.7767 | 0.8911 |
| `health_conscious` | 1.1823 | 1.0309 | 0.9901 | 0.9000 | 0.8252 | 1.0396 |
| `gadget` | 0.9360 | 1.1856 | 1.0396 | 1.5500 | 1.5534 | 0.9901 |
| `family_practical` | 1.0345 | 1.0309 | 1.1386 | 0.9500 | 0.9709 | 1.0891 |
| **share-weighted mean** | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |

`kappa` gets no such treatment. Its population means under this table are Food 0.965, Leisure 0.925,
Clothing 0.920, Hobby 0.955, Electronics 0.965, Appliances 0.935 — below 1 throughout, because three
of the four types are less quality-driven than v1's implicit average household. **This table is
therefore a different baseline economy**, and its credit arms must be compared against a
`credit_off` carrying the same table. Comparing a typed treatment arm against the untyped baseline
measures the table and the credit together and attributes both to credit.

#### This table is a hypothesis, and it is swept, not fitted

The four types are an arguable reading of how consumption differs across a population, not a
measured one, and nothing in this repository can calibrate them. They inherit the rule that governs
the tier mix (`stories/README.md`): **a table adjusted until the headline came out better would turn
this model from evidence into an illustration.**

"Swept, not fitted" is worth nothing unless the sweep is named **before** the first typed run, so it
is named here. The `gadget` row puts its two high weights and its two high steepnesses on exactly
the two financeable categories, and this section itself calls it "the one whose bidding the
abstainer feels" — a hypothesis pointed in the hypothesis's direction. That is legitimate only
against a fixed band. 10-04 reports all five of these, and reports them whatever they say:

| Table | What it isolates |
|---|---|
| `identity` | v1, the control |
| `typed` | the table above |
| `typed_w_only` | `w` as above, `kappa ≡ 1` — level without steepness |
| `typed_kappa_only` | `kappa` as above, `w ≡ 1` — steepness without level |
| `typed_kappa_neutral` | `typed`, with each `kappa` column rescaled to share-weighted mean 1 |

The last one is a **control the earlier draft lacked**. `kappa` is unnormalised by design, and under
the table above its population mean is 0.92–0.965 in every category — a population shifted toward
budget. That plausibly puts *more* households on the very shelves an abstainer trades down to, so a
typed-versus-untyped comparison confounds "taste is heterogeneous" with "taste is cheaper". Without
the neutral table the delta cannot be read. (Raised 2026-09-04 in review.)

#### Configuration shape

```toml
[archetypes.prudent]
share = 0.30
w     = { food = 0.90, leisure = 0.80, clothing = 0.85, hobby = 0.75, electronics = 0.80, appliances = 0.90 }
kappa = { food = 0.85, leisure = 0.85, clothing = 0.85, hobby = 0.85, electronics = 0.85, appliances = 0.85 }

[archetypes.health_conscious]
share = 0.20
w     = { food = 1.20, appliances = 1.05, hobby = 0.90, electronics = 0.85 }
kappa = { food = 1.25, appliances = 1.10, leisure = 0.95, clothing = 0.95, hobby = 0.95, electronics = 0.95 }
```

`health_conscious` shows the absent-means-1.0 rule doing its job: leisure and clothing are omitted
from `w` because it is average in both, and the file stays a statement of what makes the type
different. A misspelt category is an error, not a silent 1.0.

### 3.6 The grouped calibration — added 2026-09-04

§3.1's six categories model each as **one** good with **one** replacement cycle. A household does
not own "electronics"; it owns a phone it replaces every two or three years, a laptop every four or
five, and a television every seven. Those are three different purchases with three different lumps,
and credit's whole job is bridging a lump.

This section is a **second calibration**, not a replacement: eighteen goods, three per category,
each with its own life and price. §3.1 remains the default, so every number in `01-SIMULATION.md`
§10 keeps its referent. The grouped table is a configuration like any other
(`config/calibrations/grouped.toml`), and the engine needs no new concept for it — a group with its
own life, price, capacity and value weight is exactly what a category row already is. **"Category"
demotes from a thing to a label**: it groups goods for reporting (§7 of `01-SIMULATION.md`) and is
the level at which archetype taste is authored.

#### The eighteen goods

| Category | Group | `life` | `price_ref` | €/month | `base_score` | `necessity` | financeable | `term` |
|---|---|---|---|---|---|---|---|---|
| Food | groceries | 1 | 210 € | 210.00 | 1.42 | 0.85 | no | — |
| | consumables | 3 | 90 € | 30.00 | 1.35 | 0.75 | no | — |
| | eating out | 1 | 60 € | 60.00 | 0.95 | 0.20 | no | — |
| Leisure | going out | 1 | 120 € | 120.00 | 1.30 | 0.30 | no | — |
| | events | 3 | 150 € | 50.00 | 1.15 | 0.20 | no | — |
| | holiday | 12 | 360 € | 30.00 | 1.05 | 0.10 | no | — |
| Clothing | basics | 4 | 48 € | 12.00 | 1.30 | 0.75 | no | — |
| | everyday | 8 | 152 € | 19.00 | 1.15 | 0.50 | no | — |
| | outerwear | 24 | 576 € | 24.00 | 1.05 | 0.40 | no | — |
| Hobby | supplies | 3 | 27 € | 9.00 | 1.20 | 0.15 | no | — |
| | equipment | 18 | 216 € | 12.00 | 1.12 | 0.12 | **yes** | 12 |
| | big kit | 60 | 540 € | 9.00 | 1.02 | 0.08 | **yes** | 24 |
| Electronics | phone | 30 | 600 € | 20.00 | 1.24 | 0.45 | **yes** | 24 |
| | laptop | 54 | 900 € | 16.67 | 1.22 | 0.25 | **yes** | 24 |
| | TV | 84 | 700 € | 8.33 | 1.10 | 0.15 | **yes** | 24 |
| Appliances | small | 36 | 120 € | 3.33 | 1.30 | 0.40 | no | — |
| | medium | 96 | 640 € | 6.67 | 1.24 | 0.45 | **yes** | 24 |
| | large | 144 | 1440 € | 10.00 | 1.24 | 0.65 | **yes** | 36 |

`price_ref × price_mult / life` sums to **€650.00** per household per tick at the standard tier —
§3.4's first identity, unchanged. Splitting a category is a redistribution of its budget, never an
addition to it.

#### Authored by base score, with `v` derived

`v_g` is not a quantity anyone has an intuition about. The **base score** is: it is what a candidate
in that good scores for a household at the mean income before the tier multipliers, so it says
directly where the good sits relative to λ, and the tier a median household reaches follows from it
by arithmetic (`× 1.133` for budget, `× 0.800` for the standard upgrade, `× 0.500` for premium).

So the table states `base_score` and the loader derives

```
v_g = base_score_g · price_ref_g / (life_g · mean_income)
```

`v_g` may still be written down, and if it is it must equal the derived value or the run is rejected
— the rule `capacity` already obeys (§3.1). Nominal neutrality survives, because `price_ref` and
`mean_income` scale together and `v_g` is invariant (V3).

| | `v_g` | | `v_g` | | `v_g` |
|---|---|---|---|---|---|
| groceries | 0.4588 | basics | 0.0240 | phone | 0.0382 |
| consumables | 0.0623 | everyday | 0.0336 | laptop | 0.0313 |
| eating out | 0.0877 | outerwear | 0.0388 | TV | 0.0141 |
| going out | 0.2400 | supplies | 0.0166 | small | 0.0067 |
| events | 0.0885 | equipment | 0.0207 | medium | 0.0127 |
| holiday | 0.0485 | big kit | 0.0141 | large | 0.0191 |

`Σ v_g = 1.2555`, against §3.1's 1.2820 — the median household values its basket at about 26% above
what it costs, as before.

#### What the base scores were chosen against

§3.4's three design requirements are the constraints, and this table was written to satisfy them
rather than tuned until it did.

**1. Essentials outrank durables where the budget binds.** At €300, in order: groceries 1.305,
consumables 1.168, clothing basics 1.125, large appliances 1.006 — and nothing else clears λ at all.
That household buys food, underwear and a fridge, and owns no phone. As in §3.1 the ordering does
not hold at every income; the crossings are now spread across the distribution rather than bunched,
because `necessity` varies within a category and not just between:

```
going out overtakes groceries at   EUR 762      phone, medium appliances at  EUR 899
small appliances at                EUR 788      TV at                        EUR 938
laptop at                          EUR 835      big kit at                   EUR 1,008
```

Requirement 1 holds at €300 but **narrowly**: small appliances' budget candidate is 0.997 and the
phone's 0.989, so two euros of income or one tick of repricing moves the boundary. It is true as
stated and fragile as a design requirement, which is why the check in 11-02 asserts the four named
goods rather than "nothing else clears λ".

**2. The median sits mid-ladder.** The standard upgrade for the **four large financeable durables**
lands at phone 0.992, laptop 0.976, medium appliances 0.992, large appliances 0.992 — just under λ,
which is where a small price move flips a tier choice. §3.1 put electronics and appliances at 0.998;
the margin credit is expected to act on is deliberately the same one.

The other three financeable goods are **not** on that margin and are not meant to be: hobby
equipment 0.896, TV 0.880, hobby big kit 0.816. They are financeable because they are financed in
the world, not because the experiment needs them responsive, and any check on this requirement must
name the four rather than say "the financeable durables".

**3. Premium never clears at the median.** The highest premium candidate is groceries at 0.710.

**A fourth property the six-category table could not express.** Because `necessity` is now per good,
goods **enter the basket at different incomes**:

| Bought at any income | Enters around | | | |
|---|---|---|---|---|
| groceries, consumables, clothing basics | large appliances | €115 | hobby supplies | €448 |
| | small appliances | €302 | outerwear | €477 |
| | phone, medium appliances | €309 | hobby equipment | €493 |
| | everyday clothing | €348 | TV | €499 |
| | going out | €352 | holiday | €535 |
| | events | €461 | eating out | €592 |

A household below about €590 never eats out and never takes a holiday, while it replaces its fridge
at any income at all. That is Engel's law with a shape rather than a slope, and it matters here: the
cohort `01-SIMULATION.md` §10.1 found to be permanently excluded sits at a median income near €400,
which is exactly the region this table resolves and §3.1 did not.

#### Opening pressure is unchanged

Measured over 200,000 draws from the income distribution, at opening prices, ignoring cash. §3.3
quotes "about 23% below income" as prose; the figures below recompute **both** tables the same way,
because a number computed one way against a sentence written another way is not a comparison:

| | `w` fixed at 1 | `w` drawn, `σ_w = 0.20` |
|---|---|---|
| §3.1, six categories | 80.1% of mean income | 75.8% |
| §3.6, eighteen goods | **77.1%** (€500.90) | **72.1%** |

The grouped table opens **three to four points lower** on either method — not unchanged, as an
earlier draft of this section claimed. The disequilibrium is of the same kind: desired spending
below income, and premium in heavy surplus against a 40/40/20 supply (grouped: 0.521 / 0.464 /
0.016 of decisions at `w = 1`). But it is a somewhat slacker economy at the opening, which pushes
in the direction of *less* scarcity and therefore, if anything, against the hypothesis. That is
worth knowing before any difference is read, and it is not to be closed by adjusting base scores.

The mix figures count **(household, good) decisions**, not units per tick. Per tick the mix is
0.39 / 0.59 / 0.016, because the life-1 goods dominate the flow — quote whichever, never both under
one label.

#### The town has to get bigger

`capacity = round(households / life)`, so a long life on a thin category makes a thin shelf, and
splitting three ways makes it thinner. At `households = 1000`:

| Good | capacity | budget / standard / **premium** |
|---|---|---|
| Electronics/laptop | 19 | 8 / 7 / **4** |
| Electronics/TV | 12 | 5 / 5 / **2** |
| Hobby/big kit | 17 | 7 / 7 / **3** |
| Appliances/medium | 10 | 4 / 4 / **2** |
| Appliances/large | 7 | 3 / 3 / **1** |

**One premium washing machine per tick for a thousand households.** That shelf's price series is
noise, and `01-SIMULATION.md` §10.2 already found the trajectory chaotic with eighteen shelves of
which the thinnest was two units. Four premium shelves sit at three units or fewer; the laptop's
fourth premium unit is the largest-remainder rule handing the leftover to the biggest discarded
fraction (`Allocation.LargestRemainder`), which is why the split must be read off that rule rather
than off `round(share × capacity)`.

| Parameter | v1 | Grouped | Why |
|---|---|---|---|
| `households` | 1000 | **5000** | Every premium shelf reaches 7 units or more; no shelf is under 7. The walk scales with households × candidates, so the 150-run campaign goes from about 70 seconds to roughly ten to fifteen minutes — the price of admission for 54 shelves |
| `warmup_ticks` | 240 | **set on the null run** | §10.3 found relative prices converge eight times slower than the level with 18 shelves. Fifty-four repricing independently will be slower again. This number is **measured, not guessed** — the same rule that set 240 |

### 3.7 Replacement cycles by archetype — added 2026-09-04

Once a category is three goods with three cycles, the cycle itself becomes something a household can
have an opinion about. `d[A][g]` multiplies a good's life for archetype `A`: **below 1 replaces
sooner, above 1 keeps it longer.** Absent means 1.0, the rule `w` and `kappa` already follow (§3.5).

A life-1 good has no cycle to stretch, so `d` is only meaningful on the twelve durables.

| Good | `life` | `prudent` | `health_conscious` | `gadget` | `family_practical` |
|---|---|---|---|---|---|
| Clothing/basics | 4 | 1.20 → 4.9 | 1.00 → 4.1 | 1.00 → 4.1 | **0.80 → 3.3** |
| Clothing/everyday | 8 | 1.25 → 10.0 | 1.00 → 8.0 | 0.90 → 7.2 | 0.90 → 7.2 |
| Clothing/outerwear | 24 | **1.35 → 31.0** | 1.00 → 23.0 | 0.85 → 19.5 | 1.00 → 23.0 |
| Hobby/equipment | 18 | 1.30 → 22.9 | 1.00 → 17.7 | 0.80 → 14.1 | 1.00 → 17.7 |
| Hobby/big kit | 60 | **1.40 → 82.4** | 1.00 → 58.9 | **0.75 → 44.1** | 1.00 → 58.9 |
| Electronics/phone | 30 | **1.45 → 42.0** | 1.00 → 29.0 | **0.70 → 20.3** | 1.10 → 31.9 |
| Electronics/laptop | 54 | 1.35 → 69.8 | 1.00 → 51.7 | 0.80 → 41.4 | 1.05 → 54.3 |
| Electronics/TV | 84 | 1.30 → 105.5 | 1.00 → 81.1 | 0.85 → 69.0 | 1.00 → 81.1 |
| Appliances/small | 36 | 1.30 → 46.0 | 1.00 → 35.4 | 1.00 → 35.4 | 0.85 → 30.1 |
| Appliances/medium | 96 | 1.25 → 116.8 | 1.00 → 93.4 | 1.00 → 93.4 | 0.90 → 84.1 |
| Appliances/large | 144 | 1.30 → 180.5 | 1.00 → 138.8 | 1.00 → 138.8 | **0.90 → 124.9** |
| Leisure/holiday | 12 | **1.50 → 17.6** | 1.00 → 11.7 | 0.90 → 10.5 | **0.85 → 9.9** |

*(authored multiplier → realised life in months, after normalisation)*

`prudent` keeps a phone three and a half years; `gadget` replaces it every twenty months.
`family_practical` wears out underwear in three months and a washing machine in ten years, and
stretches the phone — hand-me-downs.

#### Taste and cycle compose: `ŵ = m / d`

`flow_cost` divides by the household's own life (`01-SIMULATION.md` §5.5), so
`score ∝ ŵ · d` and the two tables multiply. Author them separately and they fight: `gadget`'s phone
cycle of 0.676 would divide away almost exactly the 1.553 score multiplier meant to make it the top
phone bidder, and `prudent` at 1.400 would come out bidding highest — the opposite of the intent.

So §3.5's table is the **score multiplier** `m` and the loader derives `ŵ = m / d` per good. The
derived weights, which are what the effective configuration records:

| Good | `prudent` | `health_conscious` | `gadget` | `family_practical` |
|---|---|---|---|---|
| Clothing/basics | 0.684 | 0.966 | 1.014 | 1.389 |
| Clothing/everyday | 0.676 | 0.995 | 1.160 | 1.271 |
| Clothing/outerwear | 0.651 | 1.034 | 1.277 | 1.189 |
| Hobby/equipment | 0.588 | 0.918 | 1.975 | 0.969 |
| Hobby/big kit | 0.546 | 0.917 | 2.107 | 0.968 |
| Electronics/phone | 0.555 | 0.855 | **2.299** | 0.914 |
| Electronics/laptop | 0.601 | 0.861 | 2.027 | 0.965 |
| Electronics/TV | 0.618 | 0.854 | 1.892 | 1.005 |
| Appliances/small | 0.697 | 1.057 | 1.006 | 1.303 |
| Appliances/medium | 0.732 | 1.068 | 1.017 | 1.243 |
| Appliances/large | 0.711 | 1.078 | 1.027 | 1.255 |
| Leisure/holiday | 0.564 | 1.057 | 1.351 | 1.244 |

`gadget`'s phone weight of 2.299 is not a typo and is the point: it bids 1.553× on a phone while
buying phones 48% more often, so it values a phone-month at more than twice the average. **A type
that both churns and buys well must value the good a great deal** — that is the model saying the two
are competing claims on one budget, not a defect to normalise away.

Both identities hold exactly and each conserves a different thing: `Σ_A share_A · m = 1` fixes the
score level, `Σ_A share_A / d = 1` fixes the units. Verified on all twelve rows above.

#### The normalisation is harmonic, and getting it wrong is expensive

Demand per tick is `1 / life`, so what must average to one across the population is the
**reciprocal**:

```
Σ_A share_A / d[A][g] = 1
```

Normalise `d` itself instead and Jensen's inequality hands the population *more* units per tick than
capacity was sized for, permanently and invisibly — `E[1/d] > 1/E[d]` whenever `d` varies at all.
The table redistributes *who* replaces early; it does not make the town replace more. Same intent as
§3.5's taste identity, different mean, and for a reason that is arithmetic rather than stylistic.

The payoff is that `capacity = round(households / life_g)` **stays correct as written**. Nothing
downstream has to learn about heterogeneous lives.

**And the identity now holds exactly rather than to within a rounding**, because §5.5 replaced the
deterministic `age ≥ life` with a per-tick failure hazard of `1 / life_h,g`. A hazard takes any
positive real life, so the realised 20.3 and 4.9 months above need no integer to land on. Under the
old rule they had none, and rounding them per archetype broke the identity by more than an
arithmetic normalisation would have — 1.040 on clothing basics against 1.025 — while a load-time
assertion on the unrounded table passed.

#### The residue, and why it must not be corrected

Two residues remain, both small and both to be left alone.

**Assignment.** Assigning 5,000 households to four types by share leaves realised shares off nominal
by about 0.65 pp, which moves a good's true replacement demand by roughly half a percent against the
capacity it was sized for.

**The hazard itself.** Replacement demand is now `Binomial(owners, 1/life)` rather than a
near-constant, so it has a per-tick relative standard deviation of 15.9% for large appliances, 12.7%
for the TV, 8.0% for the phone and 2.5% for clothing basics at 5,000 households — falling to 0.84%,
0.67%, 0.42% and 0.13% over a 360-tick window. This is **less** noise than the deterministic rule it
replaces, not more: deterministic replacement preserves its opening cohorts for the life of the run,
and an unconstrained four-month good oscillates at a per-tick sd of 500 units where the hazard sits
at 31.2. Memorylessness is what mixes the cohorts.

Do **not** fix this by deriving capacity from the realised population. That would make the goods
table depend on the seed, and the campaign collector (09-02) refuses a scenario whose seeds ran
different effective configurations — correctly, since a seed that changes a parameter is a seed that
has become a parameter. Leave the residue where it is:

- both are supply-demand mismatches the reprice rule exists to absorb;
- both are **identical in both arms** — the archetype assignment is drawn per household and per
  seed rather than per scenario, and the failure draw is taken unconditionally, so the same
  households fail in the same months in either arm.

They therefore enter the paired difference as **variance rather than bias**, which is the same class
as the income draw and is already what §10.2 says about it. Earlier drafts of this section said the
residue "cancels exactly"; it does not, because a shelf's response to credit depends nonlinearly on
how tight it is, so per-seed tightness noise survives into the difference. It shifts levels and adds
spread; it does not move the headline in a direction. Report it once, in the run's opening state, so
nobody rediscovers it as an anomaly.

## 4. The decision

| Parameter | Default | Why |
|---|---|---|
| `lambda` (λ) | 1.00 | Take a candidate worth at least what it costs. A pure number, never indexed |
| `σ_w` | 0.20 | Spread of the **shared** household taste multiplier `w_h`, mean exactly 1. Per-category taste and quality steepness are §3.5 |
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
| Opening pool | 15,600,000 € | **Twenty-four** months of total income. The warm-up transient (§3.3) drains it while prices find the tier mix, and the residual drain of §V4 continues for the whole run; one month would halt the run around tick 11, twelve months around tick 700 |
| **`M0`** | **16,250,000 €** | The sum. Fixed for the run; only lending and repayment change the money stock |

The pool has no behaviour and its size is not economically meaningful — it is a buffer that makes
the transient survivable. Its **trajectory** is meaningful, and is a required diagnostic: a pool
still falling at the end of warm-up means prices have not converged and the measured window is
contaminated.
