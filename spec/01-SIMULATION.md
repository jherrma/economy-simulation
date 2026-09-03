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

### Step 3 — Wants

For each category, the household wants one unit if `life_g = 1`, or if `age_h,g ≥ life_g`.
Otherwise it wants nothing. Wants are quantities, not budgets, and never more than one unit of a
category per tick — the tier, not the count, is where extra income goes.

### Step 4 — The shopping walk

Households are visited in a **seeded random order, redrawn every tick**. Within a household, all
available candidates across all wanted categories are ranked by `score` descending and walked:

1. If `score < λ`, **stop**. Nothing further can clear the threshold.
2. If the candidate is an upgrade and the tier below it was not taken, skip it — it is not yet
   available.
3. If the target tier has no stock, record a **blocked** unit against that tier and continue.
4. If `Δcost_cash ≤ cash_h`, take it for cash. `cash_h −= Δcost_cash`, `pool += Δcost_cash`, and the
   category's chosen tier moves up one step.
5. Otherwise, if `financeable_g`, **and** a draw from the household's finance stream is `< θ_h`,
   **and** the financed score clears λ, **and** the instalment fits:

   ```
   residual_h  = income_h − debt_service_running_h − subsistence_share · income_h
   instalment  = Δcost_cash · finance_mult(g) / term_g
   instalment ≤ residual_h
   ```

   then originate a loan for `Δcost_cash` and take the candidate. New money appears as
   `cash_h += Δcost_cash` and `loans_outstanding += Δcost_cash`, and it is spent into the pool in
   the same step.
6. Otherwise record an **unaffordable** unit against that tier and continue.

At the end of the walk each category the household bought in has exactly one unit at its final tier:
stock is consumed once, at that tier, and `age_h,g = 0`.

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
it to the full term as a control.

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
- **`tier_mix`**: the share of each cohort's purchases at each tier, per category. **This is the
  trade-down finding.** If credit moves borrowers up the ladder and abstainers down it, that shows
  here before it shows anywhere else, and it is a more concrete claim than a price index.
- **`real_units`**: total units obtained, per category and in total.
- **`quality_index`**: units obtained weighted by `value_mult`, so a cohort that keeps its unit count
  by buying worse goods is not recorded as unaffected.
- **nominal spend**, and the price index faced.

The headline is the difference in the abstainer cohort's `cpi`, `share_of_wanted_obtained` and
`wait` between `credit_high` and `credit_off`, paired by seed, over the measured window.

**The result may be null, and a null result is publishable.** If abstainers are no worse off, the
hypothesis is not supported by this mechanism, and that is a finding about the mechanism.

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
