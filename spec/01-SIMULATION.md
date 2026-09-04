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
| `w_h` | initialisation, drawn | never — a taste multiplier, mean 1 |
| `θ_h` | initialisation, by scenario | never |
| `abstainer_h` | initialisation, drawn | never — `θ = 0` in **every** scenario |
| `cash_h` | `opening_cash_share · income_h` | every tick |
| `age_h,g` | drawn uniform over the good's life | +1 per tick |
| `loans_h` | empty | on origination and repayment |

A household is an index into parallel arrays, not an object.

### 4.2 Goods

Six **categories**, each with a fixed `life_g` (ticks a unit lasts), a `capacity_g` (units produced
per tick), a value weight `v_g`, a `necessity_g` share, a `financeable_g` flag and a loan `term_g`.

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
flow_cost(g, t)     = price_(g,t) / life_g
finance_mult(g)     = 1 + loan_rate · term_g / 1200
```

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
Otherwise it wants nothing. Wants are quantities, not budgets, and never more than one unit of a
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

### Step 5 — Ageing

Every held durable's `age_h,g` increases by one.

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
`"finance"` — plus one per-tick stream `hash(run_seed, 0, "order", tick)` for the shopping order.

There is **no single shared generator**. Adding a new consumer of randomness must not shift any
existing draw, and a test asserts exactly that by registering an unused purpose and requiring
byte-identical output. This costs about thirty lines now and is what makes every later comparison
between two runs mean something.

Durable ages are drawn **uniformly over each good's life** at initialisation — over `{1 … life}`,
so that with wants asked before ageing the first replacement cohort falls in tick 1 rather than
tick 2. Without the spread every household replaces its appliances in the same month and the model
produces a sawtooth that looks like a business cycle and is an artefact of initialisation.

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
  one. Reported as a cohort median. This is the cleanest available statement of the timing channel —
  the borrower gets it now, the abstainer gets it later or not at all.
- **`tier_mix`**: the share of each cohort's purchases at each tier, **per category, never pooled
  across them** (§10.4 — pooled, it reports the trade-down with the wrong sign). **This is the
  trade-down finding.** If credit moves borrowers up the ladder and abstainers down it, that shows
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
- **Three qualities per category, fixed.** Producers cannot introduce, drop or reposition a tier,
  so the *supply* of quality is as rigid as the supply of quantity. In reality a shift toward
  financed premium buying pulls production up-market, which would amplify the trade-down effect on
  the abstainer. Its absence cuts **against** the hypothesis.
- **A household buys at most one unit of a category per tick.** Extra income goes into quality, never
  into quantity, so there is no way to model buying *more* rather than *better*.

## 12. What comes next, and why not now

In the order the objections will be raised: a supply response, then wages and profit, then default
and repossession, then the second-hand market, then status. Each is a milestone in
[`../draft/MILESTONES-draft.md`](../draft/MILESTONES-draft.md) and each must arrive **behind a
switch whose off setting reproduces this model byte-for-byte**, so that the difference it makes is
measurable rather than merely visible.

Not now, because none of them can be calibrated against anything until this model has produced a
number, and because a mechanism added before its effect can be measured is a mechanism nobody can
argue with.
