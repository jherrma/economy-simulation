# The minimal simulation — v1

> **This is the implementation target.** Everything in [`../draft/`](../draft/) is a first idea and
> is not scheduled.

## 1. The question

> If A buys a good on credit because they cannot pay for it now, does the same good become more
> expensive — or harder to get — for B, who never borrows?

Everything in this specification exists to make that question answerable and, crucially, **refutable**.

## 2. The model in one paragraph

A thousand households receive a fixed monthly income. Six categories of goods are produced in
**fixed quantity** every tick and sold at a single posted price per category. Households decide what
they want, rank it by value for money, and buy what they are willing to pay for and able to afford.
A household that wants something it cannot pay for in cash may — with probability `θ` — finance it,
which creates new money and obliges it to pay instalments out of future income. At the end of the
tick, any category that sold out raises its price and any category with stock left lowers it. A
fifth of households have `θ = 0` and never borrow. **What happens to them is the finding.**

## 3. Scope

**In.** Households, six goods, one posted price per good, fixed supply, adaptive pricing, consumer
credit with repayment, money creation and destruction, rationing when demand exceeds supply.

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

`G` categories, each with a fixed `life_g` (ticks a unit lasts), `supply_g` (units produced per
tick), a posted `price_g` (the only thing that moves), a value weight `v_g`, a `financeable_g` flag
and a loan `term_g`. Values in [`02-PARAMETERS.md`](02-PARAMETERS.md).

Goods with `life_g = 1` are consumed within the tick and wanted again immediately. Goods with
`life_g > 1` are durable and wanted only when the household's unit has reached its life.

Supply is **fixed and does not respond to price**. That is the sharpest possible version of the
experiment: it isolates the bidding effect with nothing else able to absorb it. Adding a supply
response is the first thing that would soften the result, and it is a later milestone.

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
flow_value(h, g) = v_g · income_h · w_h              what a unit is worth to h, per tick
flow_cost(g)     = price_g / life_g                  what a unit costs, per tick, paid in cash
flow_cost_fin(g) = (price_g + interest_g) / life_g   … paid on credit
interest_g       = price_g · loan_rate/100 · term_g/12
score(h, g)      = flow_value(h, g) / flow_cost(g)   dimensionless
```

A unit is **worth having** when `score ≥ λ`. λ is a pure number and is never indexed: because
`flow_value` scales with income and `flow_cost` scales with price, doubling every nominal quantity
in the model leaves every score unchanged. That is what makes the model nominally neutral, and it
is checked (`03-VERIFICATION.md`, V3).

Since `interest_g > 0` always, **financing strictly worsens a unit's score**. Credit never makes
anything look cheaper here. What it does is make reachable a unit the household already wanted.
If an implementation ever makes financing attractive on price, the hypothesis is being assumed
rather than tested.

## 6. The tick

Seven steps, in this order. The order matters and is asserted.

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

For each good, the household wants one unit if `life_g = 1`, or if `age_h,g ≥ life_g`. Otherwise it
wants nothing. Wants are quantities, not budgets.

### Step 4 — The shopping walk

Households are visited in a **seeded random order, redrawn every tick**. Within a household, wanted
goods are ranked by `score` descending, and walked:

1. If `score < λ`, **stop**. The list is sorted, so nothing further can clear the threshold.
2. If `stock_g = 0`, record a **blocked** unit and continue to the next good.
3. If `price_g ≤ cash_h`, buy for cash. `cash_h −= price_g`, `pool += price_g`, `stock_g −= 1`,
   `age_h,g = 0`.
4. Otherwise, if `financeable_g`, **and** a draw from the household's finance stream is `< θ_h`,
   **and** the financed score `flow_value / flow_cost_fin ≥ λ`, **and** the instalment fits:

   ```
   residual_h = income_h − debt_service_running_h − subsistence_share · income_h
   instalment_g ≤ residual_h
   ```

   then originate the loan and buy. New money appears as `cash_h += price_g` and
   `loans_outstanding += price_g`, and it is spent into the pool in the same step.
5. Otherwise record an **unaffordable** unit and continue.

The budget depletes **sequentially** — each purchase changes what is affordable next. Do not
precompute affordability for the whole list.

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

For each good, demand is what would have sold with unlimited stock:

```
D_g = sold_g + blocked_g
```

**`unaffordable_g` is not demand.** Counting it would raise prices on goods nobody can buy. Getting
this wrong is silent: the run works and the price series is meaningless.

```
price_g ← price_g · (1 + k · clamp((D_g − supply_g) / supply_g, −1, +1))
price_g ← max(price_g, price_floor)
```

`k` is the adjustment speed. The rule is symmetric: sold out raises the price in proportion to the
shortage, stock left lowers it in proportion to the surplus.

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

## 8. Randomness

Every draw comes from a stream derived as `hash(run_seed, household_id, purpose)`, where `purpose`
is a string constant — `"income"`, `"willingness"`, `"theta"`, `"abstainer"`, `"initial_age"`,
`"finance"` — plus one per-tick stream `hash(run_seed, 0, "order", tick)` for the shopping order.

There is **no single shared generator**. Adding a new consumer of randomness must not shift any
existing draw, and a test asserts exactly that by registering an unused purpose and requiring
byte-identical output. This costs about thirty lines now and is what makes every later comparison
between two runs mean something.

Durable ages are drawn **uniformly over each good's life** at initialisation. Without this every
household replaces its appliances in the same month and the model produces a sawtooth that looks
like a business cycle and is an artefact of initialisation.

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
- **`real_units`**: total units obtained, per good and in total.
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
- **One price per category**, so quality, brand and second-hand condition do not exist.

## 12. What comes next, and why not now

In the order the objections will be raised: a supply response, then wages and profit, then default
and repossession, then the second-hand market, then status. Each is a milestone in
[`../draft/MILESTONES-draft.md`](../draft/MILESTONES-draft.md) and each must arrive **behind a
switch whose off setting reproduces this model byte-for-byte**, so that the difference it makes is
measurable rather than merely visible.

Not now, because none of them can be calibrated against anything until this model has produced a
number, and because a mechanism added before its effect can be measured is a mechanism nobody can
argue with.
