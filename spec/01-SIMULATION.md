# The minimal simulation — v1

> **This is the implementation target.** Everything in [`../draft/`](../draft/) is a first idea and
> is not scheduled.

## 1. The question

> If A buys a good on credit because they cannot pay for it now, does the same good become more
> expensive — or harder to get — for B, who never borrows?

Everything in this specification exists to make that question answerable and, crucially, **refutable**.

## 2. The model in one paragraph

A thousand households receive a fixed monthly income. Six categories of goods are produced in
**fixed quantity** every tick, each in three quality tiers with its own price. Households decide what
they want, rank every option by value for money, and buy the best tier they are willing to pay for
and able to afford.
A household that wants something it cannot pay for in cash may — with probability `θ` — finance it,
which creates new money and obliges it to pay instalments out of future income. At the end of the
tick, any category that sold out raises its price and any category with stock left lowers it. A
fifth of households have `θ = 0` and never borrow. **What happens to them is the finding.**

## 3. Scope

**In.** Households, six goods in three quality tiers, one posted price per tier, fixed supply per
tier, adaptive pricing, consumer credit with repayment, money creation and destruction, rationing
when demand exceeds supply.

**Deliberately out**, each because it is an objection to answer later rather than a mechanism the
question needs: firms and wages, supply response, unemployment, default and repossession, housing,
second-hand markets, social status, saving targets, taxes, the state, and more than one bank.

The list of what to add and in what order is [`../draft/`](../draft/). Nothing there is scheduled.

## 4. Entities

### 4.1 Households

`N` households, index `h`. Each carries:

| Field | Set at | Changes |
|---|---|---|
| `income_h` | initialisation, drawn | never — fixed nominal income |
| `w_h` | initialisation, drawn | never — the shared taste multiplier, mean 1 (§5.4) |
| `archetype_h` | initialisation, drawn | never — an index into the archetype table (§5.4), drawn in **every** scenario |
| `θ_h` | initialisation, by scenario | never |
| `abstainer_h` | initialisation, drawn | never — `θ = 0` in **every** scenario |
| `cash_h` | `opening_cash_share · income_h` | every tick |
| `age_h,g` | drawn uniform over the good's life | +1 per tick |
| `loans_h` | empty | on origination and repayment |

A household is an index into parallel arrays, not an object.

### 4.2 Goods

Six **categories**, each with a fixed `life_g` (ticks a unit lasts), a `capacity_g` (units produced
per tick), a value weight `v_g`, a `necessity_g` share, a `financeable_g` flag and a loan `term_g`.

The count is configuration, not structure: the goods table is however many rows the configuration
gives it, and §5.5 splits each category into three product groups with their own replacement cycles,
making eighteen. Where this section says *category* it means **a row of that table**; where the
output rolls rows up for reporting, it means the label they share.

Each category is sold in **three quality tiers** — budget, standard, premium — each with its own
posted price and its own fixed unit supply. A household buys at most **one unit** of a category, and
which tier it buys is its choice. Tiers are not separate goods: their prices and values are
multipliers off the category's reference (`02-PARAMETERS.md` §3.2), so the table stays six rows.

Categories with `life_g = 1` are consumed within the tick and wanted again immediately. Categories
with `life_g > 1` are durable and wanted only when the household's unit has reached its life.

Supply is **fixed and does not respond to price**, per tier. That is the sharpest possible version
of the experiment: it isolates the bidding effect with nothing else able to absorb it. Adding a
supply response is the first thing that would soften the result, and it is a later milestone.

**Why tiers are in the minimal model at all.** They are the one deliberate complication beyond the
barest sketch, and they earn it three times over:

1. **Income needs an outlet.** With one quality per category, a household on €3,000 faces exactly
   the same €650 basket as one on €650, so the top of the distribution accumulates cash that can
   never be spent, the pool drains without limit, and the run halts for a reason that has nothing to
   do with credit.
2. **They create the trade-down margin.** Without tiers, a household priced out of a good can only
   wait. With them it buys a worse one — which is both what actually happens and a far sharper
   finding: *credit does not stop the abstainer owning a phone, it moves them to a cheaper phone.*
3. **They give credit its real job.** Financing is not mainly what lets a household own a thing
   sooner; it is what lets it own a **better** thing than its cash allows. That mechanism is absent
   from a single-quality model.

### 4.3 The seller pool

One account, `pool`. Every euro spent on goods goes into it; every euro of income comes out of it.
It stands in for the whole supply side. It has no behaviour: it does not price, produce, hire or
save. Its only job is to make the money circuit closed so that conservation is checkable.

### 4.4 The bank

Implicit — a single number, `loans_outstanding`, and a rate. Lending creates money in the borrower's
hands; repayment of principal destroys it; interest received is paid straight back out to households
pro rata by income share, because in a closed economy the bank's income is somebody's income.

## 5. Value, cost and the decision

Everything the household compares is **euros per tick**, so a month of food and a washing machine
that lasts eight years are on the same footing.

```
flow_value(h, g, t) = (a_g + b_g · income_h) · w_h · value_mult_t
flow_cost(g, t)     = price_(g,t) / life_g          (life_h,g under §5.5)
finance_mult(g)     = 1 + loan_rate · term_g / 1200
```

**`loan_rate` is a flat add-on rate, not an APR.** `finance_mult` charges the whole term's interest
on the original principal, and §6 step 2 then spreads it over equal instalments, so a borrower whose
balance is falling still pays interest on the opening amount. The default `loan_rate = 8.0` over a
24-month term is `finance_mult = 1.16` — 16% of the price in interest — which is an effective APR
near **15%**, not 8%. Anyone comparing this to a real card or BNPL rate must convert first: an APR
of `r` is roughly a flat rate of `r · (n + 1) / 2n`, so a real-world 13% APR is `loan_rate ≈ 6.8`.

`w_h` is written as a single number here and generalises to a per-category taste and a
per-category quality steepness in §5.4; with the default archetype table the two forms are
identical, so everything below reads the same either way.

`a_g` is a floor in euros per tick that does **not** scale with income; `b_g · income_h` does. The
split is neutral at the mean income, so it changes only the income gradient of demand. It is not
optional: with value strictly proportional to income, a household on €450 scores food below λ and
buys none, and the poor end of the distribution starves for a reason that no invariant would catch.

### 5.1 Candidates are upgrades

A household does not choose a tier and then decide whether to buy. It faces up to **three
candidates** per wanted category, each with its own incremental value and incremental cost:

| Candidate | Δvalue | Δcost |
|---|---|---|
| buy budget | `V · 0.68` | `P · 0.60 / life` |
| budget → standard | `V · 0.32` | `P · 0.40 / life` |
| standard → premium | `V · 0.40` | `P · 0.80 / life` |

where `V = (a_g + b_g · income_h) · w_h` and `P = price_ref_g`. An upgrade candidate exists only if
the one below it was taken. The increments sum exactly to the chosen tier's price, so a household
that takes all three has paid `1.80 · P` and holds one premium unit.

```
score = Δvalue / Δcost                     dimensionless
```

A candidate is **worth taking** when `score ≥ λ`. One rule decides both *whether* to buy and *which
tier*, which is why the tier ladder is an outcome rather than a second decision procedure.

Because `value_mult` rises more slowly than `price_mult`, each upgrade scores lower than the one
below it: **diminishing returns to quality fall out of the parameters rather than being imposed.**
The candidate list for a category is therefore already sorted, and the walk can stop at the first
candidate below λ.

λ is a pure number and is never indexed: `Δvalue` scales with income, `Δcost` scales with price, so
doubling every nominal quantity in the model leaves every score unchanged. That is what makes the
model nominally neutral, and it is checked (`03-VERIFICATION.md`, V3).

### 5.2 Financing

A financed candidate's cost is multiplied by `finance_mult(g)` — 1.08 over twelve months, 1.16 over
twenty-four — so its score is the cash score divided by that. Since `finance_mult > 1` always,
**financing strictly worsens a candidate's score**. Credit never makes anything look cheaper here.
What it does is put within reach a tier that cash could not pay for. If an implementation ever makes
financing attractive on price, the hypothesis is being assumed rather than tested.

### 5.3 The reservation price on money

λ is not quite a constant. A household holding more than φ months of its own income in cash
(`buffer_months`, default 2) lowers its threshold in proportion:

```
b_h = cash_h / income_h                  months of own income, after income and debt service
λ_h = λ · min(1, φ / b_h)
```

Below φ months λ is unchanged, so nobody skips a meal to build a buffer. Above it, money the
household has been banking starts buying quality it would not otherwise have judged worth the
price. Saving is therefore a reservation price on money, not a pre-commitment, and the ratio is
dimensionless so nominal neutrality is untouched.

This is the anchor the price level otherwise lacks (§7.2). Without it, a household's tier choice is
a ratio of income to price that never looks at its cash, one unit per category caps what it can
spend, and unspent income sits in a balance that affects no decision — so nominal output settles
wherever the transient leaves it, and the pool drains a fifth of income a tick, forever. With it,
hoarded cash raises demand on the higher tiers, their prices rise, and spending is pulled back
toward income from both sides: too little cash makes candidates unaffordable and prices fall, too
much lowers λ and prices rise.

What it cannot do is make the top spend everything. The richest households earn more than any
basket price the rest of the town can clear, so a residual of about 1% of income a tick is hoarded
by the top decile alone; deciles one to nine are flat. That residual is a fact about fixed incomes
with one unit per category, and V4 is stated with it in mind (`03-VERIFICATION.md`).

### 5.4 The population has types — added 2026-09-04

v1 gives every household **one** taste multiplier `w_h`, applied identically to all six categories,
and **one** tier value ladder shared by everybody. So a household that loves food loves hobby
equipment exactly as much, and nobody can care more about the quality of their food than about the
quality of their washing machine. Neither restriction is a claim about the world; both are accidents
of keeping the minimal model minimal.

Two generalisations, and deliberately no third:

```
flow_value(h, g, tier) = (a_g + b_g · income_h) · w_h,g · value_mult(tier)^κ_g,A(h)

w_h,g                  = w_h · ŵ_g,A(h) · ε_h,g
```

`A(h)` is the household's **archetype**; `ŵ` is that archetype's taste for the category, `κ` its
quality steepness there, and `ε` an optional idiosyncratic residual.

**Level and steepness are different questions, and v1 conflates them.** Take a household that eats
well but does not overeat: it buys the best food and one portion of it. In v1 that is unsayable,
because the only way to spend more on food is to buy a better one, so "wants good food" and "wants a
lot of food" are the same number. Splitting the level `ŵ` from the steepness `κ` says it in two:
`κ_food` high, `ŵ_food` average. The same split separates the household that owns a cheap phone
because it does not care about phones from the one that owns a cheap phone because it cannot afford
the good one — and that distinction is the subject of this model.

**Why a table of types rather than six independent draws per household.** Three reasons. Nobody can
hand-write a thousand households, so what is configurable is a distribution either way, and a short
table of named types is the one a reader can argue with. Taste across categories is *correlated* in
reality — independent draws assert that correlation is zero, v1 asserts it is one, and neither is
defensible unstated. And the finding is reportable by type: *the gadget cohort bids electronics up;
the prudent abstainer, who never wanted a phone, pays more for food anyway.* This also extends a
mechanism that is already here — `abstainer_share` is a two-type population — rather than adding a
second one beside it.

#### The three levels of taste

| Level | Symbol | Varies over | What it says |
|---|---|---|---|
| Shared | `w_h` | households | a big spender or a small one, the same in every category — v1's only channel |
| Systematic | `ŵ_g,A` | (archetype, category) | this kind of household wants this kind of thing |
| Idiosyncratic | `ε_h,g` | (household, category) | everything else; `σ_idio = 0` by default, so this level is off |

The three are a decomposition of correlation, not three ways to say the same thing: `w_h` alone is
taste perfectly correlated across categories, `ε` alone is taste uncorrelated across them, and the
archetype table is the structured middle where the correlation has a name.

#### There is no switch, because the default table is the identity

v1 is the exact special case `ŵ ≡ 1`, `κ ≡ 1`, `σ_idio = 0` — not a close approximation to it. So
§12's rule that a new mechanism must arrive behind a switch whose *off* setting reproduces the model
byte for byte is satisfied by the default archetype table rather than by a boolean, in the same way
`credit_off.toml` is satisfied by an empty file (09-01). A boolean would be a second place to say
the same thing, and the two would eventually disagree.

The archetype assignment is drawn from its own stream **whether or not the table is the identity**,
following the rule θ already obeys (§8): a draw that happens in both arms disturbs nothing, and
skipping it when it looks unnecessary is how two scenarios end up on different random worlds.
Assignment is independent of the abstainer draw. If abstainers were systematically more prudent, the
comparison would confound *does not borrow* with *wants less*, and the paired design would not catch
it — that is a scenario someone may want to run deliberately, never a default.

#### The taste identity

For every category:

```
Σ_A share_A · ŵ_g,A = 1
```

The archetype table redistributes a category's demand across the population; it does not change how
much of it there is. `v_g` is the parameter for that, and two parameters for one quantity is how a
calibration stops being arguable. The table is therefore written as **relative** weights and the
loader divides each column by its share-weighted mean, so the identity holds by construction and the
normalised values are what the effective configuration records — which is what the campaign manifest
hashes (09-02).

`κ` is **not** normalised, and the asymmetry is deliberate. "What if the population became more
quality-conscious" is a real question and pinning the mean would forbid asking it. The price is that
a table whose mean `κ` differs from 1 is a **different baseline economy**, so its credit comparison
must be run against `credit_off` *under the same table* and never against the v1 baseline. Both arms
already share the table, because the archetype is drawn per household and per seed, not per scenario.

#### The bound on κ

§5.1's diminishing returns are a consequence of `value_mult` rising more slowly than `price_mult`,
and an exponent can destroy that. Candidate scores as multiples of `V / (P / life)`:

| κ | budget mult | premium mult | buy budget | budget → standard | standard → premium |
|---|---|---|---|---|---|
| 0.85 | 0.720 | 1.331 | **1.201** | 0.699 | 0.414 |
| 1.00 | 0.680 | 1.400 | **1.133** | 0.800 | 0.500 |
| 1.20 | 0.630 | 1.497 | **1.049** | 0.926 | 0.622 |
| 1.3245 | 0.600 | 1.562 | **1.000** | 1.000 | 0.702 |
| 1.50 | 0.561 | 1.657 | 0.935 | **1.098** | 0.821 |

The ladder inverts at `ln(price_mult_budget) / ln(value_mult_budget)` = `ln 0.60 / ln 0.68` =
**1.3245**. Above it `0.68^κ < 0.60`, buying budget scores below upgrading to standard, and the
candidate list is no longer sorted. The **walk** survives that — §6 step 4 ranks every candidate
descending and does not rely on list position — but the **stop** rule does not: a household whose
budget candidate falls below λ halts, while the standard unit it would gladly have bought scores
higher and is unreachable, because the step below it was never taken. That is exactly the open
modelling question already recorded in §6 step 4 (2026-09-03), and `κ > 1.3245` promotes it from a
curiosity to the thing that decides the run.

So `0 < κ < κ_max`, rejected at load above it. `κ_max` is **derived from the tier table**, never a
literal in the code: the tier multipliers are themselves parameters, and a hard-coded 1.3245 would
silently become wrong the first time somebody changes one. At the v1 tiers the second ordering
condition — `budget → standard` above `standard → premium` — binds only at 2.3194, so the first is
what actually constrains; at another tier table it need not be.

**But the bound is measured at opening prices, and the headroom it appears to give is small once
prices move.** The scores use *actual* prices, and inversion happens when
`price_budget / price_standard > value_mult_budget^κ`. The opening ratio is 0.60, so the budget
shelf has to become dearer relative to standard by:

| κ | inverts once budget/standard rises by |
|---|---|
| 1.00 | +13.3% |
| 1.20 | +5.0% |
| 1.25 | **+2.9%** |

§10.3 watched food's budget shelf move 22% in a single run. So for `gadget` (electronics κ 1.25) and
`health_conscious` (food κ 1.25) the halt-on-budget pathology is **routine rather than exceptional**,
in exactly the cohorts and categories under test — and the load-time bound does not prevent it, it
only keeps the *opening* ladder sorted. §3.5's "deliberate headroom" claim was written against the
static bound and is too comfortable. Either lower the table's κ, or resolve §6 step 4's open
question about buying the next tier directly when the one below is sold out, before this matters.
(Raised 2026-09-04 in review.)

#### What archetypes deliberately do not carry

- **θ.** The scenario sets the credit level; two places to set it is one place for them to disagree,
  which is the reason 09-01 refuses a scenario file that names itself. A per-category *shape* on θ —
  a `finance_affinity` multiplier, so a household will finance a washing machine but not a phone —
  is the obvious next extension and is recorded here, not scheduled.
- **`buffer_months` (φ).** A per-household φ is genuinely new information: it is a nonlinearity in
  cash, not a scale on value, so nothing else reproduces it. It belongs to §5.3, not to taste.
- **`life`.** Replacement discipline — running a phone into the ground versus replacing it early —
  is real and is not taste. It also interacts with a calibration identity: `capacity_g` is *derived*
  as `round(households / life_g)`, so a per-household life whose mean is not `life_g` changes the
  steady-state replacement demand and quietly changes the scarcity the experiment is about.
- **λ.** Never, and not merely for now — see below.

#### Two parameters that look obvious and buy nothing

Both are recorded because they are what a reader reaches for next.

**Per-household `necessity_h,g` is exactly absorbed into `w_h,g`.** Income is fixed for the life of
the run, so each household sits at one income, `(a_g + b_g · income_h)` is a single number, and
`w` already spans every positive number it could take. Per-*category* necessity keeps doing its
work — it varies across the population *through income*, which is the whole point of §5's split.
The redundancy ends the moment incomes are allowed to move, and this note should be re-read then.

**Per-household `λ_h` is redundant against the shared level `w_h`.** The test is `score ≥ λ_h` and
`score ∝ w_h`, so a picky household with a high threshold is algebraically a low-taste household;
§5.3's modulation `λ · min(1, φ / b_h)` is multiplicative and preserves the equivalence exactly. λ
stays global, and the heterogeneity goes where it can be read.

#### What it costs

More heterogeneity is more trajectory noise, and §10.4's comfortable margin is a margin, not
immunity. Pairing cancels the *draw* — the same household is the same archetype in both arms — but
not *which* households are marginal, and that is where the noise lives. The power probe
(`tools/Gates pilot`) is the instrument; it is re-run after the table changes rather than assumed to
still hold. A table that pushes the headline below its own resolution is a finding about the table,
and it is reported, not tuned away.

### 5.5 Product groups and replacement cycles — added 2026-09-04

§4.2 models each category as one good with one life. A household does not own "electronics"; it owns
a phone it replaces every two or three years, a laptop every four or five, and a television every
seven. Three purchases, three lumps of three different sizes at three different frequencies — and
bridging a lump is the entire job of the credit this model is about.

**A product group is not a new dimension. It is a good.** A category in this model is a row of
`(life, capacity, price_ref, v, necessity, financeable, term)`, and a group with its own replacement
cycle and price is exactly that row. So the goods table goes from six rows to eighteen, `category`
demotes from a thing to a **label** on each row, and the *engine proper* learns no new concept: the
tier overlay is untouched, `Σ_t units(g,t) = capacity_g` still holds per row, 18 × 3 = 54 shelves
reprice where 18 did, and `GoodsTable`, `Market`, `MetricsWriter` and `CohortMetrics` already read
their length from the configuration. `02-PARAMETERS.md` §3.6 is the table.

**The loader was a different story, and an earlier draft of this section was wrong about it.**
`ConfigurationLoader.ReadCategories` *merges*: a row the file does not name is kept from the basis,
by design, so that `[categories.food]` changes food and leaves the other five alone (02-02). A file
naming eighteen goods therefore loaded as **twenty-four**, and `Σ price_ref / life` came to 1,300
rather than 650 — a run that starts, produces numbers and looks like the one that was asked for.

**Resolved 2026-09-04 by `replace = true`** (11-01), a scalar in `[categories]` that says "this
section is the table" rather than "these are edits to it". The alternative, a removal syntax, is
worse in a way worth naming: six `[categories.food] delete = true` stanzas is a file that says what
it is not. It is stated per file rather than inferred from how many rows are present, because
inferring it is the failure mode — a file naming eighteen goods is indistinguishable from a file
editing eighteen of them, and guessing wrong is silent in both directions. The overlay stays the
default, so every scenario file already written means what it always meant.

Two things fell out of building it. The goods table is now read in **file order** rather than sorted
by name (`[archetypes]` still sorts, because there the order decides which household is assigned to
which type, and two files stating the same population differently have to be the same population).
Rows are walked in order — the shopping walk, the per-good residual draw — so sorting them would
turn §3.1's `food, leisure, …` into `appliances, clothing, …` and change every run. And an effective
configuration now always carries `replace = true`, because a resolved configuration that has to be
overlaid on the right basis to mean what it says is not a resolved configuration.

The label earns its keep twice. It is the level at which output is rolled up — a CPI for
*electronics* is what a reader can hold in their head, not one for laptops — and it is the level at
which archetype taste is authored (§5.4), with per-group overrides where a type has an opinion about
one good and not its neighbours. Twenty-four numbers and a handful of exceptions, rather than a
hundred and eight.

#### What splitting exposed

The groups could not be made to fit the old category budgets. A €600 phone replaced every thirty
months is €20 a month **on its own**, four fifths of what §3.1 allots the entire electronics
category; and appliances at €8.33 a month was modelling a household that owns one appliance rather
than a fridge, a washing machine, an oven, a dishwasher and a kettle. The four durable categories
had to be reweighted within their €150:

```
clothing 66.67 -> 55.00     electronics 25.00 -> 45.00
hobby    50.00 -> 30.00     appliances   8.33 -> 20.00
```

This is the strongest argument for the split, and it is about falsifiability rather than realism.
"€900 every 36 months" is a number nobody can look at and call wrong. "A €600 phone every 30 months"
is checkable against a shop window. Splitting the categories did not merely make the calibration
better; it made it **arguable**, which is the standard the rest of this specification is held to.

The consequence has to be stated plainly: **the grouped table is a different baseline economy.** The
numbers in §10 were measured on §3.1 and do not carry over. The two calibrations are comparable in
kind — §3.6 opens at the same 77% of income and the same premium surplus — never in value.

#### Replacement cycles differ by type

Once a category is three cycles, the cycle is something a household can have an opinion about.
`d[A][g]` multiplies a good's life for archetype `A`: below 1 replaces sooner, above 1 keeps it
longer. `prudent` keeps a phone forty-two months; `gadget` replaces it every twenty.
`family_practical` wears out underwear in three months and stretches the phone.

#### Wearing out is a hazard, not a calendar — decided 2026-09-04

Two questions were open here and both are now answered; the answers turn out to be the same answer
seen twice.

**A durable fails with probability `1 / life_h,g` each tick, rather than at a fixed age.** The
deterministic rule `age ≥ life` cannot represent §3.7's realised lives at all — `Life` is an `int`,
`Age` an `int[]`, and 20.3 months is not a number a want can fire at. Rounding per archetype breaks
the harmonic identity by **more than the error that identity exists to prevent** (clothing basics
1.040 from rounding, against 1.025 from normalising `d` arithmetically), and worse, the load-time
assertion would pass on the unrounded table while the running engine violated it. A hazard takes any
positive real life and the identity then holds **exactly** rather than to within a rounding.

Three things fall out that were not the reason for choosing it:

- **The initialisation sawtooth cannot happen.** The geometric distribution is memoryless, so there
  is no age to initialise: every household opens owning a working unit of every durable, a fraction
  `1/life` of them fails in tick 1, and that is already the steady state. 02-04's uniform age draw
  and the `"initial_age"` purpose exist only to spread the first cohort, and they do not mix the
  cohorts afterwards — under deterministic replacement a household that replaces in month 7 replaces
  again in month 7 + life, forever, and only rationing ever decorrelates them.

  **The difference is structure, not size — corrected 2026-09-04 against the measurement.** An
  earlier draft of this bullet claimed the calendar "oscillates with a per-tick standard deviation
  of 500 units about a mean that never reaches capacity" against 31 for the hazard, and concluded
  the null run should get *easier*. Unconstrained at 5,000 households, a four-month good sits at
  **1250.0 ± 35.8** under the calendar and **1251.8 ± 31.6** under the hazard: the same spread, and
  the calendar's mean is capacity exactly. What separates them is that the calendar's series is
  *periodic* — its cohorts are fixed at initialisation and never mix, so its autocorrelation at lag
  `life` is essentially 1 — while the hazard's is essentially 0 at every lag. Expect the null run
  to behave about the same, not better; what the hazard removes is the cohort to initialise and the
  cohort to warm out, which is worth having on its own.
- **The life-1 special case disappears.** `p = 1/life` gives `p = 1` at `life = 1`, so a consumable
  is consumed every tick by the same rule that fails a washing machine. `wants = life == 1 || age ≥ life`
  becomes one draw.
- **`flow_cost = price / life_h,g` becomes literally true.** Under a constant hazard the expected
  cost per tick of owning the good *is* `price / life_h,g`, rather than an amortisation of a cycle
  the household is assumed to complete. Which answers the second question below by construction.

The cost is real and is stated so nobody discovers it: per-tick replacement demand is now
`Binomial(owners, p)` rather than a near-constant, with a relative standard deviation of
`√((1 − p) / (N · p))`. At 5,000 households that is 16.9% for large appliances, 12.9% for the TV,
7.6% for the phone and 2.5% for clothing basics; measured on seed 11 over a 360-tick window,
**16.5%, 13.6%, 7.5% and 2.5%** — and **0.87%, 0.72%, 0.39% and 0.13%** once divided by the length of
that window, which is the number a result is actually read off. The failure draw is taken
**unconditionally**, for every household and every good and every tick, whether or not a unit is
owned. That is the rule `θ` obeys (§8), and here it means the same households fail in the
same months in both arms, so the noise cancels in the paired difference rather than merely
averaging out.

#### `flow_cost` uses the household's own life — decided 2026-09-04, built 2026-09-06 (11-04)

```
flow_cost(h, g, t) = price_(g,t) / life_h,g
```

A household that replaces its phone every twenty months is paying for a phone at a higher rate per
month than one that keeps it forty, and the decision rule should see that. It also keeps §5's
"everything is euros per tick" footing true of every household rather than of the average one.

**The consequence is that taste and cycle compose, and they must be authored together.** Since
`score = flow_value / flow_cost`, the score multiplier for archetype `A` in good `g` is

```
score multiplier = ŵ_g,A · d_g,A
```

Author them separately and they fight: `gadget` replacing phones 1.48× as often divides its score by
1.48, which very nearly cancels the 1.55 taste weight meant to make it the top phone bidder — and
`prudent`, keeping its phone forty-two months, would come out bidding *highest*. That inversion is
not a defect in the mechanism, it is the model correctly saying that **churning and buying well are
competing claims on the same budget**, and that a household which does both must value the good a
great deal.

So the archetype table states the **score multiplier**, which is the quantity with a meaning, and
`ŵ` is derived from it: `ŵ = m / d`. `02-PARAMETERS.md` §3.5's numbers are unchanged and are now read
as `m`; where `d = 1` — every life-1 good, and the whole of E10 — the two are identical and nothing
about §5.4 moves.

**The effective configuration records `m` and `d`, not `ŵ`** — an earlier draft of §3.7 said
otherwise. `ŵ` is the quotient of two columns that are both written down, so recording it as well
would be seventy-two numbers of pure redundancy in a file the campaign manifest hashes, and a third
place for the derivation to mean something different. What guards §3.7's table of derived weights is
a test that re-computes it, which is the same answer 11-02 gave for `base_score` and `v`.

**The two tables are keyed at two different levels, and this is deliberate.** `w` and `kappa` are
authored per **category label**: a type is a statement about wanting electronics, not about wanting a
laptop more than a phone. `d` is authored per **good**, because a replacement cycle is a fact about a
particular product — `gadget` churns its phone every twenty months and leaves its television alone
for nearly six years, and §3.7's table has three different multipliers inside electronics to say so.
Under §3.1 a label *is* a row and the distinction is invisible, which is why every file written
before E11 still means exactly what it meant. The loader will not accept a good's name in a `w` or a
label in a `d`.

**The normalisation is harmonic** — `Σ_A share_A / d[A][g] = 1` — because demand per tick is
`1 / life`, so it is the reciprocal that must average to one. Normalising `d` itself would hand the
population more units per tick than capacity was sized for, permanently and invisibly, by Jensen's
inequality. Done correctly, `capacity = round(households / life_g)` stays true as written and
nothing downstream learns about heterogeneous lives. The finite-sample residue and why it must be
left alone are in `02-PARAMETERS.md` §3.7.

#### This one adds a channel

§5.4 was a generalisation: archetypes changed what a household was willing to *pay*, and its off
setting was a table of ones. Replacement cycles change **how often a household turns up at the shelf
at all**. `gadget` demanding phones half again as often is new crowding, not merely a higher bid,
and quantity heterogeneity is a mechanism rather than a loosened parameter.

It still switches off exactly — `d ≡ 1` is the identity, and the grouped calibration is a
configuration whose absence leaves §3.1 running — but it belongs behind §12's discipline and not
beside §5.4's. In particular it must be built **after** §5.4 and measured separately: §5.4 preserves
the baseline byte for byte and its effect is therefore attributable, while this changes the
calibration, the population size and the warm-up, and cannot. Stack the two and there are two
changes and one number.

#### The question it makes askable

The model currently has one lump per category, so it can only ask whether credit hurts the abstainer
in *electronics*. With groups there is a spectrum of lump sizes and frequencies inside one category,
and a sharper question:

> Does the harm concentrate in the frequent-medium lump — a €600 phone every two and a half years —
> or in the rare-huge one, a €1,440 washing machine every twelve?

Those are different credit propositions and there is no reason to expect the same answer. §10.4
found the effect living in the durables that credit is used for; this is the next cut down.

## 6. The tick

Seven steps, in this order. The order matters and is asserted.

Before step 1 the tick **restocks**: every tier's stock is set back to `units(g,t)`. That is what
"fixed supply per tick" means — unsold premium units do not pile up into a glut, they are simply
production that was not taken. It is not one of the seven steps because nothing decides and nothing
moves; it is the definition of the shelf the steps then act on.

### Step 1 — Income

Each household receives `income_h` from the pool. `income_h` is fixed for the life of the run.

### Step 2 — Debt service

For every outstanding loan, the instalment is deducted **before any shopping**, so a household can
never spend money it owes this month:

```
instalment   = (principal + interest_total) / term
principal_part = principal / term
interest_part  = interest_total / term
```

`cash_h` falls by the instalment. `loans_outstanding` falls by `principal_part`, and that money is
**destroyed**. The `interest_part` is pooled across all loans and paid out to all households pro
rata by `income_h` — bank profit returning as household income.

The pooling has a place on the balance sheet: the interest sits in the bank's till between its
collection and its distribution, the till is a money holder like any other (V1 counts money in
flight), and it is **empty again before step 3** — the check refuses a tick that ends with anything
in it. The bank keeps nothing. The dividend is split by 01-02's remainder-distributing rule, so it
sums to the interest collected to the cent, and abstainers receive their share like anyone else.

### Step 3 — Wants

For each category, the household wants one unit if `life_g = 1`, or if `age_h,g ≥ life_g`.
Otherwise it wants nothing. **Under §5.5's hazard the rule is simply that it wants one unit if it
does not own a working one**, everything else having been decided in step 5. Wants are quantities, not budgets, and never more than one unit of a
category per tick — the tier, not the count, is where extra income goes.

### Step 4 — The shopping walk

Households are visited in a **seeded random order, redrawn every tick**. Within a household, all
available candidates across all wanted categories are ranked by `score` descending and walked:

1. Take the best-scoring candidate that is **available** — a purchase, or an upgrade whose step
   below has been taken. If its `score < λ_h` (§5.3), **stop**. Nothing further can clear the threshold.
   (An upgrade becomes available the moment the step below it is taken and is then ranked among
   what remains. At opening prices the ladder is already sorted so this is the same as one pass
   down a sorted list; once tiers have repriced independently it need not be — budget above 0.68
   of standard puts the upgrade above the purchase — and a single pass would skip an upgrade that
   is both worth taking and affordable for no reason but list position.)
2. Establish **ability** first. If `Δcost_cash ≤ cash_h`, the household can pay cash. Otherwise,
   if `financeable_g`, **and** a draw from the household's finance stream is `< θ_h`, **and** the
   financed score clears λ, **and** the instalment fits:

   ```
   residual_h  = income_h − debt_service_running_h − subsistence_share · income_h
   instalment  = Δcost_cash · finance_mult(g) / term_g
   instalment ≤ residual_h
   ```

   the household can finance. If it can do neither, record an **unaffordable** unit against that
   tier and continue.
3. Only then look at the shelf. If the target tier has no stock, record a **blocked** unit against
   that tier and continue. Ability comes before stock because *blocked* is demand (step 6) and
   demand is what would have sold with unlimited stock — a household that could not have paid
   would not have bought from a full shelf either. The earlier ordering counted a broke household
   at an empty shelf as demand.
4. Take it. For cash: `cash_h −= Δcost_cash`, `pool += Δcost_cash`. Financed: originate a loan for
   `Δcost_cash`; new money appears as `cash_h += Δcost_cash` and `loans_outstanding += Δcost_cash`,
   and it is spent into the pool in the same step. Either way the category's chosen tier moves up
   one step.

At the end of the walk each category the household bought in has exactly one unit at its final tier:
stock is consumed once, at that tier, and `age_h,g = 0`.

A consequence worth knowing: because stock is checked step by step and consumed at the final tier,
a household blocked at *budget* cannot reach *standard* this tick even with the cash for it — the
upgrade's prerequisite was never taken. It leaves the category empty-handed and its want persists.
"Step up when budget sells out" therefore works through prices, not within the tick: the budget
price rises, the increment to standard shrinks, and more of the households that *did* get budget
upgrade. Whether households should be allowed to buy the next tier directly when the one below is
sold out is an open modelling question (2026-09-03); v1 implements the ladder as written.

**Increments, not whole units.** A household that takes *buy budget* and then *budget → standard*
has paid `0.60 · P` and `0.40 · P` and holds one standard unit. Financing an increment and financing
the whole unit come to identical total interest, because `finance_mult` is uniform, so the
implementation may treat each candidate as its own small loan. The arithmetic is exact and the
bookkeeping is simpler.

The budget depletes **sequentially** — each candidate taken changes what is affordable next. Do not
precompute affordability for the whole list.

This is where the trade-down channel lives. A household that cannot afford the *budget → standard*
increment in cash, and will not or cannot finance it, keeps the budget unit. It is not excluded from
the category; it is moved down it.

The affordability horizon is **one tick**. A household checks that the instalment fits *this month*,
not that the loan is wise over its term. That asymmetry is the model's version of the observation
that a household short of cash looks at the monthly payment while a household with cash looks at the
price, and it is a behaviour under test, not an assumption to hide: `affordability_horizon` switches
it to the full term as a control. Under `full_term` the household asks the **whole repayable
amount**, `Δcost_cash · finance_mult(g)`, to fit the residual — the price test a cash buyer applies,
put to the borrower — so a €540 budget appliance needs a residual of €626.40 and the median
household finances nothing. Loan stacking all but disappears under it, which is the point of the
control (defined 2026-09-03, 06-02).

**Rationing is first-come within the random order.** Nobody is favoured by wealth or willingness, so
any crowding-out that appears is caused by *ability to bid at all*, which is exactly the mechanism
under test. `rationing = willingness` is available as a variant and is expected to strengthen the
result; it should be reported separately, never as the default.

### Step 5 — Ageing, or failure

Every held durable's `age_h,g` increases by one.

**Under §5.5's hazard this step replaces ageing with failure.** Every household draws, for every
good, from the `"failure"` stream — **unconditionally, whether or not it owns a working unit** — and
a unit it does own fails when the draw is below `1 / life_h,g`. A life-1 good has `p = 1` and is
consumed by the same rule. `age_h,g` is kept for reporting and decides nothing.

One consequence of opening with everything working: under `hazard` **tick 1 has no wants at all**,
and the first replacements are bought in tick 2. That is the opposite hole from the one 02-04's
`{1 … life}` age draw was written to avoid, it is one tick at the very start of a six-hundred-tick
run, and it is inside the warm-up. It is stated here so that nobody reads it off a plot as a result.

The draw is unconditional for the reason `θ` is drawn with credit off (§8): a household rationed out
of a good in one arm and served in the other must still consume the same stream position, or the two
arms drift onto different worlds and the paired comparison quietly stops being paired. It costs one
draw per household per durable per tick.

### Step 6 — Repricing

For each **tier** of each category — eighteen prices in all — demand is what would have sold with
unlimited stock:

```
D_(g,t) = sold_(g,t) + blocked_(g,t)
```

**`unaffordable` is not demand.** Counting it would raise prices on goods nobody can buy. Getting
this wrong is silent: the run works and the price series is meaningless.

```
price_(g,t) ← price_(g,t) · (1 + k · clamp((D_(g,t) − units_(g,t)) / units_(g,t), −1, +1))
price_(g,t) ← max(price_(g,t), price_floor)
```

`k` is the adjustment speed. The rule is symmetric: sold out raises the price in proportion to the
shortage, stock left lowers it in proportion to the surplus.

Each tier reprices on **its own** excess demand, so relative prices within a category move. They must
be free to: that movement is how the tier mix clears. If premium sits unsold its price falls until
enough households take the upgrade; if budget sells out its price rises until some households step
up or drop out. **The realised tier mix is an output of this rule, never a parameter** — pinning it
would assume the answer, since the question is precisely how credit shifts it.

### Step 7 — The check

```
Σ cash_h + pool == M0 + loans_outstanding
```

To the cent, every tick, in every scenario. A violation halts the run — it does not warn. The pool
must also stay positive; a negative pool means income exceeds what the economy can pay and the
calibration is wrong, which is a result about the parameters and must be reported as one.

## 7. Money

There are exactly four things that move money, and no fifth may be added without changing §6:

| | Effect on the money stock |
|---|---|
| Income (pool → household) | none |
| Purchase (household → pool) | none |
| Loan origination | **+ principal** |
| Repayment of principal | **− principal** |

Interest is a transfer, not a creation: out of the borrower, back to all households.

The switch `money_creation = false` funds loans from the pool instead of creating them, and returns
repaid principal to the pool. The money stock is then constant and credit is pure reallocation.
**The difference between the two settings is the money-creation channel, measured directly** — and
it is the cheapest interesting experiment this model can run.

### 7.1 V1 at both settings

The identity in §6 step 7 is the `money_creation = true` form. With creation off, loans exist and no
money was made, so `Σ cash + pool == M0 + loans_outstanding` is false by exactly the amount lent.
The form that holds at **both** settings, and is what the engine asserts, is:

```
Σ cash_h + pool == M0 + net_money_created
```

with a second assertion tying the two together, which is where the switch is actually policed:

```
money_creation = true   →  net_money_created == loans_outstanding
money_creation = false  →  net_money_created == 0
```

This is strictly stronger than the original, and not by a little. The sum alone catches nothing
that the operations do not already maintain: destroying money lowers both of its sides at once, so
**interest destroyed along with principal — the bug §6 step 7 exists to catch — leaves the sum
perfectly balanced.** It is the second assertion that fails, because money destroyed was not
matched by a claim released. The same assertion is what catches a loan funded the wrong way: an
origination modelled as a transfer from the pool builds the `money_creation = false` variant and
calls it the default, and the money stock alone would never notice.


### 7.2 The price level is unanchored — found 2026-09-03, resolved by §5.3

Found when the reprice rule first ran (E5). With repricing on and every other default of the time,
the pool was exhausted at about tick 60 and the run halted in the income step. It was **not** the
warm-up transient: with a pool that could not drain the town settled by tick 80 — prices converged,
shelves cleared — and the pool still fell by about **€135k a tick, a fifth of income**, for the rest
of the run, with sales near €515k against €650k of income and cash accumulating in every income
group, the bottom half included.

The cause was structural. The reprice rule sees only unit excess demand, which is zero at any price
level once every shelf clears, so the §3.2 identity (capacity value equals income) held at opening
prices only. And a household's tier choice was a ratio of income to price, blind to its cash, with
one unit per category capping what it could spend, so unspent income had nowhere to go.

Three resolutions were put to the author: endogenous income (the pool pays out last tick's
receipts), a cash-sensitive λ, or a price level that responds to the pool. **Income stays fixed;
the cash-sensitive λ of §5.3 was adopted**, with φ = 2 chosen on three seeds. Measured afterwards,
default pool of twelve months (€7.8M — the default at the time; it is twenty-four months now, §10.3):

| φ | drain, ticks 200–360 | lowest pool below opening | durable candidates unaffordable, per tick |
|---|---|---|---|
| off | −€135k/tick | −€47M (halts at tick 60) | — |
| 1 | −€5k to −€7k | −€1.4M to −€2.2M | ~720 |
| **2** | **−€7k to −€9k** | **−€2.8M to −€3.3M** | **~320** |
| 3 | −€9k to −€11k | −€3.9M to −€4.4M | ~270, premium still converging at 360 |

The remaining drain is hoarding by the top decile alone (€60–100 per household per tick; deciles
one to nine flat). φ is a calibration lever that also moves the durables' liquidity channel —
lower φ leaves households unable to pay cash for a durable more often — and results should be
reported with their sensitivity to it.

### 7.3 The money-creation channel is zero by construction — found 2026-09-03

Found when the switch was first tested (06-04). `credit_high` with `money_creation = true` and with
`money_creation = false`, same seed, are **identical in every quantity a household can see** —
every price, every balance, every loan, every tick. The story expected the two runs to differ; they
cannot, and the reason is structural rather than a bug:

- A borrower's cash is the same after origination and purchase whether the principal was created or
  drawn from the pool. The household side of the walk does not know where the money came from.
- Income is fixed and the pool pays it either way. The pool has no behaviour (§4.3): it does not
  spend, lend, price or produce, so a bigger or smaller pool changes nothing downstream.
- Repayment destroys principal or returns it to the pool; again the household side is identical.

The entire difference between the settings therefore sits in the pool's balance and in the money
stock, one for one with `loans_outstanding`. The one observable effect the switch can have is
**rationing** — origination failing because the pool cannot cover the principal — and that needs the
pool to be below a *single* principal at the moment of the loan, because the principal drawn out is
spent straight back in by the same purchase. The default pool never comes near it. The tests assert the identity (MoneyCreationSwitchTests) and the rationing case with a
€400 pool.

What this means for the question: in v1 the price effect of credit is **entirely** the reach-and-
timing effect — households buying now, and buying better, what cash alone would not have let them
— and none of it is a money-stock effect. "Credit raises prices because lending creates money" is
not something this model can support or refute; it is switched off by the passive supply side. The
switch stays, because the identity is worth asserting every run (it is what V1's second assertion
polices), and because the moment the pool acquires behaviour — income indexed to the pool, supply
responding to it, a bank that lends only what it holds — the channel opens and the paired runs will
start to differ. Until then the "money-creation channel" series (07-02) is the pool difference, and
should be labelled as such rather than as a price effect.

## 8. Randomness

Every draw comes from a stream derived as `hash(run_seed, household_id, purpose)`, where `purpose`
is a string constant — `"income"`, `"willingness"`, `"theta"`, `"abstainer"`, `"initial_age"`,
`"finance"`, `"archetype"`, `"taste_idio"` — plus two per-tick streams,
`hash(run_seed, 0, "order", tick)` for the shopping order and
`hash(run_seed, household_id, "failure", tick, good)` for §5.5's failure draw.

There is **no single shared generator**. Adding a new consumer of randomness must not shift any
existing draw, and a test asserts exactly that by registering an unused purpose and requiring
byte-identical output. This costs about thirty lines now and is what makes every later comparison
between two runs mean something.

`"archetype"` and `"taste_idio"` are drawn unconditionally, exactly as `"theta"` is when credit is
off (§5.4). The identity archetype table consumes the same draws as any other, so the population is
assigned in every run and only the *table* differs — which is what lets a typed scenario be compared
against its own `credit_off` on the same seeds.

Durable ages are drawn **uniformly over each good's life** at initialisation — over `{1 … life}`,
so that with wants asked before ageing the first replacement cohort falls in tick 1 rather than
tick 2. Without the spread every household replaces its appliances in the same month and the model
produces a sawtooth that looks like a business cycle and is an artefact of initialisation.

The spread is a **partial** fix and §5.5 explains why: it separates the opening cohorts and then
nothing ever mixes them, because a household replacing in month 7 replaces again in month 7 + life
for the life of the run. Only rationing decorrelates them. Under the hazard the question does not
arise — the geometric distribution is memoryless, so every household opens owning a working unit,
`"initial_age"` is not consumed, and the steady state is the opening state.

## 9. Scenarios

Three, run on the **same seeds**, compared paired:

| Scenario | θ for non-abstainers |
|---|---|
| `credit_off` | 0 — nobody borrows. The baseline |
| `credit_low` | drawn from `U(0, 0.2)` |
| `credit_high` | drawn from `U(0.4, 0.9)` |

Plus two variants of `credit_high`: `money_creation = false`, and `rationing = willingness`.

The abstainer cohort is the **same households in every scenario for a given seed**. That is what
makes the comparison paired rather than merely averaged.

## 10. The measurement

Aggregate, per tick: `cpi`, money stock, `loans_outstanding`, pool balance, and per good the price,
units sold, blocked units and unaffordable units.

**Every series is reported per good and per category** (E11, §5.5). A good is a row of the goods
table; a category is the label rows are grouped under, and under §3.1 the two coincide, so
`cpi_food` and `cpi_category_food` carry the same number for that calibration and always will. The
duplication is deliberate: a schema that emitted the roll-up only where it differed from a good
would be a schema a reader has to inspect the goods table to parse. The roll-up is **unit-weighted**
— the weights are the supply units, the same fixed basket the index itself uses — so a €1,440
washing machine replaced every twelve years counts for its seven units rather than for its price.
Value-weighting instead would let one expensive, rarely-replaced good speak for a category of three,
and the two diverge sharply on exactly the categories §3.6 splits.

```
cpi_t = Σ_g price_g,t · supply_g  /  Σ_g price_g,0 · supply_g
```

A Laspeyres index on the fixed supply basket. Since supply is fixed, that basket is the only one
that cannot be argued with.

**By cohort — abstainers versus borrowers — this is the finding:**

- **`share_of_wanted_obtained`** per good: units bought ÷ units wanted. With supply fixed, total
  real consumption is capped, so the question is not how much the economy consumes but **who gets
  it**.
- **`wait`**: for each durable want, the number of ticks between first wanting a unit and obtaining
  one. Reported as a cohort median, and read as **`wait_median_met`** — the median over the wants
  that were actually met — never as `wait_median`, which mixes that flow against a growing stock of
  wants that are never met and therefore drifts in a perfectly stationary economy (§10.1). This is
  the cleanest available statement of the timing channel: the borrower gets it now, the abstainer
  gets it later or not at all. **In the creditless baseline it is identically zero**, on thirty
  seeds and under both replacement rules (V4, 2026-09-04) — the null run has no queue, so any wait
  that appears in a credit arm is credit's.
- **`tier_mix`**: the share of each cohort's purchases at each tier, **within a good or within a
  category, never pooled across categories** (§10.4 — pooled, it reports the trade-down with the
  wrong sign). One category is the widest denominator this model will report a share over.
  **This is the trade-down finding.** If credit moves borrowers up the ladder and abstainers down it, that shows
  here before it shows anywhere else, and it is a more concrete claim than a price index.
- **`real_units`**: total units obtained, per category and in total.
- **`quality_index`**: units obtained weighted by `value_mult`, so a cohort that keeps its unit count
  by buying worse goods is not recorded as unaffected.
- **nominal spend**, and the price index faced.

The headline is the difference in the abstainer cohort's `cpi`, `share_of_wanted_obtained` and
`wait` between `credit_high` and `credit_off`, paired by seed, over the measured window. §10.4
measures it: it resolves at ten to sixty times the spread across thirty seeds, and it lives in the
per-category price and access series rather than in the level.

**The result may be null, and a null result is publishable.** If abstainers are no worse off, the
hypothesis is not supported by this mechanism, and that is a finding about the mechanism.

### 10.1 The rationing is an exclusion, not a queue — found 2026-09-03

Found on the first full 360-tick run with output (E7), `credit_off`, seeds 1 and 2, and unchanged
from about tick 120 onwards.

Of roughly four thousand durable wants, about 640 are open at any moment and about 460 of those
have been open for more than fifty ticks. Both counts are **stationary**; the ages of the stuck ones
are not, since the same households are never served. Those households sit at the poor end of the
distribution — median income around €400 against a mean of €650 — and the categories are the scarce
ones, appliances (about 275 open) and electronics (about 155).

The rest are served **at once**. `wait_median_met`, the median wait of the durable wants actually
met in a tick, is **zero at every tick of the baseline**: a household that gets served is served the
tick it asks. So the model's rationing is not everyone waiting a little. It is most households
served immediately and a stable minority excluded outright — which is the sharper of the two losses
the question is about, and the one the abstainer cohort is there to measure.

**`wait_median` is not a stationary series, and must not be used as one.** It is the median over
every durable want the cohort faced, unmet ones counted at their current age, which mixes a flow of
freshly-opened wants against a growing stock of stuck ones. On seed 1 it swings between 19 and 66
within ticks 121–180 alone, and its mean over that window against ticks 301–360 moves from 41.9 to
44.1; on seed 2, from 40.6 to 51.7 with a range of 11.5 to 132.5. The drift is mild and the
tick-to-tick variance is several-fold, so **V4 must not test it for stationarity** and no write-up
may report its movement as an effect.

What it is good for is the comparison the headline is actually defined as: **same tick, across
scenarios, paired by seed**. That is sound, and it is what says whether credit lengthens the
abstainer's wait or widens the excluded set.

### 10.2 The trajectory is chaotic; the equilibrium is not — found 2026-09-03

Found while building V3 (`spec/stories/08-02`), on `credit_off` and `credit_high`, eight seeds,
360 ticks — before §10.3 lengthened the run.

**One cent decorrelates a run.** Add a single cent to `mean_income` and change nothing else: the run
is identical for three or four ticks, and then about **48% of every real cell** in a 360-tick run
differs from the baseline's. Nothing about that is a fault. Money is integer cents, a household
spends down to nearly nothing every tick, and whether the last increment it reaches for costs one
cent more than it holds is a genuine knife edge. A cent puts a few households on the other side of
it; one household landing on budget instead of standard takes the last unit of a rationed shelf from
someone else; and the difference spreads through the price rule into everything.

Scaling every nominal quantity by `c` does exactly the same thing, for the same reason and to the
same degree: `round(c · x) ≠ c · round(x)` for about half of all `x`, at `c = 2` as much as at
`c = 0.5`, so the two runs' household incomes differ by up to half a cent each and the trajectories
part company within a few ticks.

**What survives is the measured window.** Over the ticks past the warm-up, averaged across the seed set, the
series that are measured finely enough to compare — those whose window mean varies by less than a
tenth of a percent from seed to seed — agree to two or three parts in a thousand, and the scaled
runs agree with the baseline **at least as closely as the one-cent control does**. The equilibrium
is invariant even though the path to it is not.

Four consequences, and they bind on everything downstream:

1. **No result may be read off a single seed's series.** A tick-by-tick difference between two runs
   is not a measurement of anything; only a window mean over the seed set is. This is why §9's
   protocol is what it is.
2. **Pairing by seed does not cancel trajectory noise.** It cancels the population draw — the same
   incomes, tastes and initial ages in both arms — which is worth having and is why it is done. It
   does not make two trajectories comparable tick by tick. Precision comes from the number of seeds.
3. **Only about a sixth of the recorded series resolve well enough to support a claim** at the
   tenth-of-a-percent level on eight seeds: 43 of 271. A shelf that sells one unit a fortnight and
   the open-wait median (§10.1) are not among them. They need either the full thirty seeds or no
   claim at all, and the gate reports which is which rather than assuming.
4. **V5's byte-identity is untouched by any of this**, and that is the point of it. It compares two
   configurations whose inputs are identical to the cent, so no rounding difference ever arises. An
   off switch has to reproduce the previous version *bit*-identically, because "closely" is not
   something this model can do.

Re-check by running `dotnet run --project tools/Gates -- neutrality`: it reports the tick at which
each arm first diverges, the share of cells that differ, and how each arm compares against the
one-cent control.

### 10.3 Relative prices converge eight times slower than the level — found 2026-09-03

Found by V4 (`spec/stories/08-03`), `credit_off`, thirty seeds, on runs of 360 and 720 ticks.

The CPI settles in about **thirty** ticks. Individual tier prices take **two hundred and forty or
more**, and the two facts sit on top of each other so neatly that the second was invisible until the
gate looked for it. Leisure's budget shelf, mean over thirty seeds:

| tick | 1 | 60 | 120 | 180 | 240 | 300 | 360 | 720 |
|---|---|---|---|---|---|---|---|---|
| price | 120.00 | 117.15 | 113.31 | 110.55 | 109.04 | 108.36 | 108.37 | 108.30 |

An exponential approach to €108.30 with a time constant near seventy ticks. At tick 120 — the end of
the warm-up as it then was — it is still **4.5% above where it is going**. Food's budget shelf does
the same thing upwards, 180.00 → 212.99 at tick 120 → 219.5 at rest.

Over the old measured window of ticks 121–360 that showed as a secular drift in the leisure budget
price of **−4.09%, with a spread of 1.72% across thirty seeds** — thirteen standard errors, and not
something that averages away. A credit effect measured against that baseline would have been drift
plus signal, which is precisely what V4 exists to prevent.

**Why it hid.** The CPI is a unit-weighted index and the moves offset inside it: food and the
durables rise while leisure falls, so the level is flat by tick 30 while the relative prices
underneath it are still finding each other for another two hundred ticks. V4 anticipated exactly
this shape of failure and named the tier *mix* as the place to look for it; here it was in the
prices, and the mix was the quieter of the two.

**Parameters changed on this evidence**, not on judgement:

| | was | is | because |
|---|---|---|---|
| `warmup_ticks` | 120 | 240 | the transient is over by then and not before |
| `ticks` | 360 | 600 | so the measured window is still thirty years |
| `opening_pool_months` | 12 | 24 | the residual drain of about 1.9% of a tick's income per tick empties a twelve-month pool around tick 700; the worst of thirty seeds halted at 697 |

**A second finding, and it limits what may be claimed.** The thinnest shelves have no identified
price level at all. Appliances produce ten units a tick, two of them premium, so a demand of nought
or four against a stock of two is an ordinary tick and the price takes a five per cent step in an
arbitrary direction. Its thirty-tick smoothed price still wanders several per cent after six hundred
ticks, and the tick at which it "settles" moves with the length of the run rather than with the
economy — 350 in a 360-tick run, 637 in a 720-tick one. That is a statement about the measurement,
not about the model. **No claim may be made about a single thin shelf's price level**; what those
shelves support is a distribution across seeds, and the gate holds each series only to what its own
spread across seeds can resolve.

Re-check with `dotnet run --project tools/Gates -- nullrun`, which reports every drift, every
settling tick, and the seed spread behind both.

### 10.4 The effect is large; the pooled tier mix reports it backwards — found 2026-09-04

Found with `tools/Gates` `pilot`, which is not a gate: it runs both arms of the experiment over the
campaign's own thirty seeds at the campaign's own parameters and asks whether the difference the
write-up is for is larger than the spread across seeds. Sixty runs of 600 ticks take sixteen
seconds, so this is a question that can be asked before the campaign exists rather than after it
produces a number nobody can defend. §10.2 is what made it worth asking: pairing removes the
population draw, not the trajectory noise, so the paired difference carries the noise of both arms.

**It resolves, and not narrowly.** `credit_high` against `credit_off`, paired by seed, thirty seeds,
window from tick 241, against the smallest difference thirty seeds could distinguish from zero:

| | `credit_off` | `credit_high` | difference | 95% detectable |
|---|---|---|---|---|
| `cpi` | 1.0432 | 1.0489 | **+0.55%** | 0.26% |
| abstainer share of wanted obtained | 0.7598 | 0.7172 | **−5.61%** | 0.48% |
| abstainer electronics obtained | 0.1224 | 0.0805 | **−34.3%** | 3.47% |
| abstainer appliances obtained | 0.0263 | 0.0198 | **−24.5%** | 2.74% |
| `cpi_electronics` | 0.9901 | 1.0987 | **+10.97%** | 0.59% |
| `cpi_appliances` | 1.1781 | 1.2667 | **+7.53%** | 0.53% |

The headline is ten to sixty times its own resolution. **Thirty seeds are more than the question
needs**; eight would carry it. The 271 shelf-level series of §10.2 still do not resolve, and still
do not have to — the headline is not made of them.

**Where the effect is.** Not in the price level: the CPI moves half a percent, and the money stock
moves +0.44%, which is the §7.3 channel and about the same size. It is in **relative prices, in
exactly the goods credit is used for** — electronics +11%, appliances +7.5%, hobby +5.7%, against
clothing −3.6% — and in who ends up holding them. The abstainer's total unit count barely moves
(−0.36%); their access to durables collapses. That is the answer to §1 in the model's own terms:
the good B never borrowed for gets **more expensive and harder to get**, and it is not general
inflation doing it.

**Nobody gains units.** Town-wide consumption falls: −0.36% of units and −0.39% of value-weighted
units, and both cohorts lose (borrowers −0.35% units, −0.50% quality). Credit creates no goods; in a
stationary window the borrower's head start was taken during the warm-up and what remains of it is
debt service. Any write-up claiming borrowers do better must say **at what** — it is timing and
composition, never quantity.

**The pooled `tier_mix` of §10 reports the trade-down backwards.** Pooled over categories, the
abstainer's premium share *rises* 2.1% under credit. Within category it falls everywhere it
matters:

| abstainer, within category | budget share | premium share |
|---|---|---|
| appliances | **+11.7 pp** | −4.0 pp |
| electronics | **+7.1 pp** | −3.6 pp |
| hobby | +1.8 pp | −0.8 pp |
| food | −1.5 pp | +0.7 pp |
| leisure | −0.4 pp | +0.3 pp |

All at |t| > 4. Being priced out of appliances moves those units out of the pooled denominator and
the money into better food, and the pooled share reports that as trading up. **`tier_mix` is a
within-category measure and must never be pooled across categories** — the same failure as §10.1's
`wait_median`, and the same remedy: the number is sound, the aggregate over it is not.

**`k` sets the price level, not just the speed it is reached at.** Re-running the probe at
`k` = 0.02, 0.05, 0.1, 0.2 moves the baseline economy a long way — `cpi` 1.027 → 1.043 → 1.235 →
1.426, and the abstainer's share of wanted obtained 0.78 → 0.76 → 0.59 → 0.49. A multiplicative rule
on lumpy demand does not average to its midpoint. **The difference between the arms survives it**:
`cpi_electronics` +8.7% to +11.6%, abstainer electronics obtained −22% to −32%, the sign and the
order of magnitude unchanged across a tenfold range. So the comparison is robust and the baseline is
a calibration. Every reported percentage must be reported **as a difference**, and the write-up owes
the reader the sensitivity band rather than a single number carrying four digits.

**The loan rate matters, monotonically, and the default is the conservative end.** Re-running the
probe at `loan_rate` = 6.8, 8, 13, 20 — an effective APR of roughly 13%, 15%, 25%, 38% — makes
credit steadily less attractive, so less of it is taken (`loans_outstanding` 76.9k, 71.0k, 51.6k,
32.5k) and the externality shrinks with it:

| `loan_rate` (effective APR) | abstainer appliances | abstainer electronics | `cpi_electronics` |
|---|---|---|---|
| 6.8 (≈13%) | −26.0% | −35.8% | +11.6% |
| **8.0 (≈15%, default)** | **−24.5%** | **−34.3%** | **+11.0%** |
| 13 (≈25%) | −15.2% | −26.2% | +7.7% |
| 20 (≈38%) | −9.6% | −14.6% | +3.9% |

The effect survives across the whole range at |t| > 11, and the default sits at the **cautious** end
of the real-world band rather than the flattering one: at the flat rate matching a typical 13% card
or BNPL APR the measured harm to abstainers is slightly *larger* than what §10.4 reports.

**Credit is small; the durable shelves are thin.** New lending is **10% of the town's durable
spending** — a share a reader will recognise from the real world — and debt service is 1.27% of all
spending, interest 0.18%. That modest a flow moves the abstainer's access to appliances by a
quarter, because the marginal durable market is where the whole adjustment lands. The leverage, not
the volume, is the finding.

**Two things the campaign will find missing.** `rationed` — credit rationing, the pool refusing to
fund — is **identically zero on all sixty runs**: the pool always funds, so `credit_high` is
unconstrained credit and the loan-supply channel is inert at these parameters. And the "price index
faced" per cohort that §10 lists is not a column; `abstainer_spend / abstainer_obtained` is a unit
value, which moves with composition and is not a price index. Whoever builds the analysis needs the
cohort's basket priced at both arms' prices, or the price claim has to rest on `cpi_*` and the
per-category shares, which carry it perfectly well.

**Supply never binds.** About a quarter of appliance capacity and a fifth of electronics capacity go
unsold every tick in both arms, while abstainers obtain 2.6% of the appliances they want. The
constraint on B is cash, not stock — B is priced out, not queued out, which is what §10.1 saw from
the other side. A write-up must not describe this as A taking the last unit off the shelf.

### 10.5 A typed population halves the access effect and leaves the price effect alone — found 2026-09-04

The archetype table of §5.4 was built without knowing whether the headline would survive it, and
§3.5's sweep grid was named before the first typed run. This is what the five tables say, each arm
against **its own** control — a typed table is a different baseline economy, so comparing a typed
`credit_high` against the untyped `credit_off` would measure the table and the credit together.

Measured with `dotnet run --project tools/Gates -- pilot table=<row>`, thirty paired seeds, 600
ticks, window from tick 241. The headline measure is the abstainer's share of wanted units obtained.

| table | `credit_off` level | difference | *t* | 95% detectable |
|---|---|---|---|---|
| `identity` (v1) | 0.7598 | **−5.61%** | −23.8 | 0.48% |
| `typed` | 0.7819 | **−3.08%** | −20.0 | 0.32% |
| `typed_w_only` | 0.7743 | −4.13% | −20.6 | 0.41% |
| `typed_kappa_only` | 0.7754 | −3.56% | −23.3 | 0.31% |
| `typed_kappa_neutral` | 0.7800 | −3.08% | −16.1 | 0.39% |

**Thirty seeds are still enough.** The effect is eight to twenty times its own resolution under
every table, and the paired spread is *smaller* under a typed population than under v1 (0.84%
against 1.29% on this measure). The dispersion archetypes add to the population does not translate
into dispersion across seeds, because a seed's population is a draw of a thousand households and the
table is a property of the draw, not of the seed.

**The effect halves, and `typed_kappa_neutral` says why it is not an artefact of the table's level.**
§3.5 raised the confound: the typed table's population-mean `kappa` is 0.92–0.965 in every category,
so the town is shifted toward the budget shelves before credit is mentioned, and that alone might
put more households on the shelves an abstainer trades down to. Rescaling every `kappa` column to
mean 1 moves the answer from −3.084% to −3.081%. **It is not the level. It is the heterogeneity.**
The `w`-only and `kappa`-only rows say both channels contribute and roughly multiplicatively.

**The price effect is untouched.** This is the sharper half of the finding, because §10.4's answer to
§1 rests on relative prices rather than on the index:

| | `identity` | `typed` | `typed_kappa_neutral` |
|---|---|---|---|
| `cpi_electronics` | +10.97% | +11.72% | +11.52% |
| `cpi_appliances` | +7.53% | +6.59% | +7.13% |
| `cpi_clothing` | −3.60% | −1.47% | −1.64% |
| `cpi` | +0.55% | +0.14% | +0.15% |

So the two halves of the claim behave differently under a heterogeneous population: **the good B
never borrowed for is exactly as much dearer, and about half as much harder to get.** A write-up
that leads with the price is on firmer ground than one that leads with access.

**What shrank the access effect: there is less borrowing.** `loans_outstanding` averages €70,970
under `identity`, €49,037 under `typed` and €53,564 under `typed_kappa_neutral` — a quarter to a
third less credit outstanding at the same θ. The financing decision is a **threshold**, and
spreading taste around its mean moves households across it in both directions without conserving the
count. Less borrowing is less bidding, and the abstainer feels proportionally less of it. The
mechanism is unchanged; the population that exercises it is smaller.

**One thing that only becomes visible with types.** Under `identity` the abstainer's quality index is
flat and unresolvable (+0.04%, *t* = 0.4). Under every typed table it rises, significantly:
+0.31% at `typed`, *t* = 15.6, while units obtained per tick are flat (+0.008%). The abstainer gets
about the same number of things and they are very slightly better on average. That is *consistent*
with exclusion falling on marginal purchases first — the wants that get dropped are the cheap ones —
but this section has not tested that reading, and it should not be quoted as though it had.

**What this does not say.** It does not say the `typed` table is right. Nothing in this repository
can (§3.5). It says the headline is not an artefact of v1's assumption that one number describes
what a household wants — which was the live risk, and is now measured rather than hoped.

## 11. What this model cannot show

Every one of these must accompany any number that comes out of it.

- **Supply cannot respond.** Fixed supply makes the price effect an **upper bound**; in reality some
  of it would be absorbed by producing more.
- **Income cannot respond.** Nominal income is fixed, so the abstainer's real-income loss is also an
  upper bound. There are no wages, no profits and no firms.
- **Nobody defaults.** The affordability test at origination and the deduction of instalments before
  shopping make arrears impossible by construction. Credit therefore has all of its demand effect
  and none of its losses.
- **There is no second-hand market**, so the cash buyer has no cheaper substitute to escape into.
  This cuts *against* the hypothesis.
- **There is no status, no advertising and no obsolescence**, so no purchase in this model is one
  that would never have happened. The "never-would-have" channel is absent entirely.
- **Waiting is not a decision.** A household that cannot buy simply tries again next tick; it does
  not deliberately save toward a target. Thrift is emergent, not chosen.
- **Under §5.5 the failure hazard is constant, so nothing wears out.** A washing machine is as likely
  to fail in its first month as in its hundredth, and replacement timing is therefore more dispersed
  than in reality, where the hazard rises with age. A Weibull or gamma hazard would be closer and
  costs one shape parameter and a non-memoryless state; it is not in this version. The sharper
  limitation is that a hazard models a thing **breaking**, never a household **choosing** to replace
  something that still works — so `prudent` keeping a phone forty-two months is expressed here as a
  phone that fails less often, which is not what is meant. Making it a decision needs a second
  threshold, and that is a mechanism rather than a parameter.
- **Three qualities per category, fixed.** Producers cannot introduce, drop or reposition a tier,
  so the *supply* of quality is as rigid as the supply of quantity. In reality a shift toward
  financed premium buying pulls production up-market, which would amplify the trade-down effect on
  the abstainer. Its absence cuts **against** the hypothesis.
- **A household buys at most one unit of a category per tick.** Extra income goes into quality, never
  into quantity, so there is no way to model buying *more* rather than *better*. Under §5.5 the cap
  is per *good*, so a household may buy a phone and a laptop in one tick — but never two phones, and
  the limitation stands. §5.4's archetypes do
  **not** lift this: they say how much a household is willing to pay for its one unit and how far up
  the tiers it will go, not how many units it wants. Allowing more would lengthen the candidate
  ladder, break the `capacity = households / life` identity, and give income a new outlet — which
  moves the baseline the credit effect is measured against, so it is a version change and not a
  parameter.

## 12. What comes next, and why not now

In the order the objections will be raised: a supply response, then wages and profit, then default
and repossession, then the second-hand market, then status. Each is a milestone in
[`../draft/MILESTONES-draft.md`](../draft/MILESTONES-draft.md) and each must arrive **behind a
switch whose off setting reproduces this model byte-for-byte**, so that the difference it makes is
measurable rather than merely visible.

Not now, because none of them can be calibrated against anything until this model has produced a
number, and because a mechanism added before its effect can be measured is a mechanism nobody can
argue with.

**§5.5's product groups come before the list too, and for a weaker reason than §5.4's.** They add no
mechanism — a group is a goods-table row — but the recalibration they force *is* a new baseline, and
the replacement-cycle multiplier *is* a new channel. The justification is that the split makes the
calibration checkable against a shop window where the six-category table was unfalsifiable, and an
unfalsifiable calibration undermines every milestone below it. Build it after §5.4, never with it.

**§5.4's archetypes are not on that list and come before it, for a reason worth stating.** Every
milestone above answers an objection by adding a *channel* the model does not have. Archetypes add
no channel: they remove an accidental restriction on a parameter that already exists, replacing one
taste multiplier with six and one quality ladder with one per type. That is why the identity table
reproduces v1 byte for byte without a switch, and why it can be done before the model has been
argued with rather than after. The genuine next steps out of it — `finance_affinity`, a per-household
`buffer_months`, a per-household `life` — do add channels, and they wait their turn.
