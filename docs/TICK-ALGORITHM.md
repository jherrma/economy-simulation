# How one tick is computed

Pseudocode for everything that happens in a single month of simulated time, in the order it
happens, with the quantities each step reads and writes. It is written to be read alongside the
code: every block names the file it lives in, so a reader can go from "what happens" to "where"
without searching.

The authority for *why* the model does any of this is `spec/01-SIMULATION.md`. This document is
the *what*, and where the two disagree the specification wins and this file is wrong.

**Notation.** `h` is a household, `g` a good (a row of the goods table), `t` a tier
(0 = budget, 1 = standard, 2 = premium), `A(h)` the household's archetype. A *shelf* is one
`(g, t)` pair; there are `|goods| x 3` of them. Money is integer cents throughout and is written
as an amount; a **flow** is euros per tick and is the only quantity allowed to be a float, because
nothing conserves it and the only thing ever done with two flows is to divide one by the other.

---

## 0. Before the first tick

Built once, in `Simulation`'s constructor, and never re-drawn.

```
GoodsTable(parameters):                                     # World/GoodsTable.cs
    for each good g:
        capacity_g = round(households / life_g)
        units[g][*] = LargestRemainder(capacity_g, tier_unit_shares)    # e.g. 19 -> 8 / 7 / 4
        for each tier t:
            opening_price[g][t] = price_ref_g * price_mult_t

Households.Draw(parameters, goods, run_seed):               # World/Households.cs
    for each household h:
        income[h]      = mean_income * LogNormal(1, sigma_income)   # stream (h, Income)
        taste_level[h] = LogNormal(1, sigma_w)                      # stream (h, Willingness)
        archetype[h]   = pick_by_share(types, U())                  # stream (h, Archetype)
        for each good g:
            eps                = LogNormal(1, sigma_idio)           # stream (h, TasteIdiosyncratic)
            taste[h][g]        = taste_level[h] * w_hat[A(h)][g] * eps
        is_abstainer[h] = U() < abstainer_share                     # stream (h, Abstainer)
        theta_raw       = U()                                       # stream (h, Theta) -- ALWAYS drawn
        theta[h]        = 0 if is_abstainer[h] else theta_min + (theta_max - theta_min) * theta_raw
        for each good g:
            life[A(h)][g]   = life_g * d[A(h)][g]                   # the household's own life
            hazard[A(h)][g] = 1 / life[A(h)][g]

Ledger.Open(opening_cash, opening_pool):                    # Ledger/Ledger.cs
    cash[h] = income[h] * opening_cash_share
    pool    = (sum of all income) * opening_pool_months
```

Three details here are load-bearing rather than incidental:

- **`theta` is drawn even when credit is off.** If the draw were skipped in the baseline, the two
  arms of a paired comparison would sit on different random worlds from that point on and the
  pairing would be worthless.
- **Each per-household quantity has its own stream**, keyed by `(run_seed, h, purpose)`. Adding a
  household therefore disturbs nobody else's draws, and the abstainer draw is independent of the
  archetype draw — otherwise "does not borrow" could confound with "wants less".
- **`capacity_g` is sized in expectation** and the population actually drawn realises something
  slightly different. The gap is reported in `run.done` as `replacement_residue` and is *not*
  corrected; deriving capacity from the realised draw would make the goods table a function of the
  seed.

---

## 1. The tick, at the top

```
RunTick(tick):                                              # Simulation.cs
    Market.Restock()                                        # not a step: nothing decides, nothing moves
    Ledger.OpenTick()
    TickRecord.Open(tick, market, warmup_ticks)             # prices + CPI, before anything moves them
    CohortMetrics.OpenTick()                                # clears this tick's accumulators

    for step in [Income, DebtService, Wants, Walk, Ageing, Repricing, Check]:
        run(step);  if it failed: stop the run here

    TickRecord.Close(ledger, loans, rationed)
    CohortMetrics.Close(population, ledger, loans)
```

The step list is data, not control flow: `Simulation.StepOrder` is compared by a test against the
committed fixture `spec/tick-order.txt`, so reordering the tick is an edit to a file with a reason
attached rather than a line moved in a method.

```
Restock():                                                  # World/Market.cs
    for each shelf (g, t):
        stock[g][t] = units[g][t]                           # fixed supply: unsold units do NOT carry over
    clear sold[], blocked[], unaffordable[]
```

---

## 2. Step 1 — income

```
Income():                                                   # Simulation.cs
    for each household h:
        Transfer(Pool -> Household(h), income[h], reason = Income)
        if the pool cannot pay: fail the run as a CALIBRATION result, not a bug
```

Incomes are fixed for the run. The pool is a finite counterparty, so an income calibration the
economy cannot sustain halts the run with a message saying which kind of failure it is.

---

## 3. Step 2 — debt service

Before any shopping, which is what makes the affordability test at origination sufficient: the
instalment is guaranteed to fit because it left income before anything else could claim it.

```
Service():                                                  # Credit/LoanBook.cs
    for each household h, for each live loan l of h:
        k             = l.paid                              # which instalment is due
        principal_k   = Share(l.principal,     l.term, k)   # largest-remainder split
        interest_k    = Share(l.interest_total, l.term, k)  # so the parts sum EXACTLY

        Transfer(Household(h) -> Bank, interest_k, reason = InterestDividend)
        if money_creation:  DestroyMoney(Household(h), principal_k)
        else:               Transfer(Household(h) -> Pool, principal_k)
        ReleaseClaim(principal_k)

        l.paid += 1
        if l.paid == l.term: retire l

    Distribute():                                           # the bank keeps nothing
        parts = LargestRemainder(bank_balance, weights = income[])   # pro rata by fixed income
        for each household h: Transfer(Bank -> Household(h), parts[h])
```

Interest is **simple**: `interest_total = principal * (rate * term / 1200)`, computed once at
origination in `Rate` — the only file in the engine permitted to know the divisor `1200`.

---

## 4. Step 3 — wants

A want is a boolean, never a count. The model cannot represent buying *more* of something, only
buying *better* or *worse*; that is what the tier ladder is for.

```
Wants():                                                    # Simulation.cs, World/Households.cs
    for each household h, for each good g:
        if replacement == "hazard":
            wants = not holds[h][g]
        else:                                    # "deterministic"
            wants = (life_g == 1) or (age[h][g] >= life_g)     # note: the GOOD's life here

        wait[h][g] = wait[h][g] + 1  if (wants and wanted[h][g])  else 0
        wanted[h][g] = wants

        if wanted[h][g]: CohortMetrics.RecordWant(cell(h), g)
```

An unmet want persists and its `wait` accumulates: the household priced out of a phone this tick is
still in the market next tick, and how long it stays there is the cleanest statement the model can
make about the timing channel.

---

## 5. Step 4 — the shopping walk

About 95 % of the tick's runtime, and allocation-free: every buffer is owned by `Walker` and sized
once at construction.

### 5.1 Who shops first

```
Order(tick):                                                # Decision/Walker.cs
    order = [0 .. households-1]
    Shuffle(order)                                          # stream (run_seed, tick, Order), redrawn every tick
    if rationing == "willingness":
        stable-sort order by taste_level[h] descending      # keener households first; the shuffle breaks ties
```

`random` is the default and the conservative choice: nobody is favoured by wealth or willingness,
so any crowding-out the model produces is caused by *ability to bid at all* — the mechanism under
test. `willingness` is expected to strengthen the result and must never be the default.

### 5.2 The candidate ladder

Built per household, per wanted good. **A candidate is an increment, never a tier.** Ranking whole
tiers by total score is the obvious implementation and it silently produces a run in which every
household buys budget everything and premium never sells at any price.

```
Ladder.Build(h, g):                                         # Decision/Ladder.cs
    base_value = (floor_g + income_slope_g * income[h]) * taste[h][g]     # euros per tick
    life       = life[A(h)][g]                              # the HOUSEHOLD's life, not the good's

    prev_mult = 0;  prev_price = 0
    for each tier t:
        mult        = value_mult_t ^ kappa[A(h)][label(g)]   # the household's view of the tier
        price       = posted_price[g][t]

        delta_value = base_value * (mult - prev_mult)        # flow, euros per tick
        delta_price = price - prev_price                     # money, cents
        delta_cost  = delta_price / life                     # flow, euros per tick

        score[t]    = delta_value / delta_cost               # a pure number, compared against lambda
                      (0 if delta_value <= 0; +inf if delta_value > 0 and delta_cost <= 0)

        prev_mult = mult;  prev_price = price
```

The increments telescope: whatever path a household walks, what it has paid in total is exactly the
posted price of the tier it ends on.

### 5.3 The household's threshold

```
LambdaFor(h):                                               # Decision/Walker.cs
    b_h = cash[h] / income[h]                               # months of its own income, after steps 1-2
    return lambda * min(1, buffer_months / b_h)
```

Cash below `buffer_months` leaves `lambda` alone; cash above it lowers `lambda`, so a household
that has been banking part of its income starts taking upgrades it would not otherwise take.
Dimensionless throughout, which is what nominal neutrality (V3) rests on.

### 5.4 The walk itself

```
WalkOne(h):                                                 # Decision/Walker.cs
    candidates = concat(Ladder.Build(h, g) for each g with wanted[h][g])
    chosen_tier[g] = -1 for all g
    lambda_h = LambdaFor(h)

    loop:
        best = the unvisited candidate with the highest score among those AVAILABLE,
               where available(c) = (c.tier == 0) or (chosen_tier[c.good] == c.tier - 1)

        if best does not exist or best.score < lambda_h:
            break                                           # nothing further can clear the threshold

        mark best visited
        finance = false

        # --- ability before stock, deliberately -------------------------------------------
        if best.delta_price > cash[h]:
            if not CanFinance(h, best, lambda_h):
                unaffordable[best.good][best.tier] += 1      # NOT demand
                continue
            finance = true

        if stock[best.good][best.tier] == 0:
            blocked[best.good][best.tier] += 1               # IS demand: willing AND able
            continue

        if finance:
            Originate(h, best)  -> funded?
            if not funded:                                   # money_creation off and the pool is empty
                unaffordable[best.good][best.tier] += 1
                rationed += 1
                continue

        Pay(h, best.delta_price)                             # household -> pool (or the reverse if negative)
        chosen_tier[best.good] = best.tier

    # --- settlement: one unit per good, at the tier finally landed on ----------------------
    for each good g with chosen_tier[g] >= 0:
        t = chosen_tier[g]
        Market.Sell(g, t)                                    # stock--, sold++
        CohortMetrics.RecordPurchase(cell(h), g, t, posted_price[g][t], wait[h][g])
        Acquire(h, g)                                        # age = 0, holds = true, wanted = false, wait = 0
```

Four properties of this loop are choices, and all four are conservative:

- **The budget is sequential.** Each candidate taken changes what is affordable next, so the tier a
  household lands on depends on what it bought first. Precomputing affordability for the whole list
  would let a household take an upgrade it can no longer pay for.
- **Ability is checked before stock.** "Blocked" means willing *and able* with nowhere to go,
  because that and only that is demand the price should see. A household that could not have paid
  would not have bought from a full shelf either.
- **A household walking budget -> standard consumes one standard unit**, not one of each. Stock
  moves once per good, at settlement.
- **The walk does not assume the ladder arrives sorted.** At opening prices it is, because
  `value_mult` rises more slowly than `price_mult`; once tiers reprice independently a higher tier
  can become the better ratio, and the walk re-scans for the best remaining candidate every
  iteration.

### 5.5 Whether a household may finance

Four conditions, in the specification's order, each reached only if the one before held.

```
CanFinance(h, candidate, lambda_h):                         # Decision/Walker.cs
    1.  credit_enabled and financeable(candidate.good) and candidate.delta_price > 0
    2.  U() < theta[h]                                      # stream (h, Finance); abstainers have theta = 0
    3.  candidate.score / (1 + rate * term_g / 1200) >= lambda_h
    4.  burden <= income[h] - DebtService(h) - income[h] * subsistence_share

        where burden = next_instalment                      if horizon == "myopic"   (default)
                     = principal + interest_total           if horizon == "full_term" (the control)
```

Condition 3 is the one worth pausing on. The finance multiplier exceeds 1 whenever the rate and the
term do, so **financing strictly worsens a candidate's score**. Credit never makes anything look
cheaper in this model. What it does is put within reach a tier that cash could not pay for — and
`FlowCostTests` fails the build for every good and every tier if that ever stops being true.

Condition 4's residual counts loans originated earlier in this same walk, so a household cannot
stack its way past its own income within one tick.

```
Originate(h, candidate):                                    # Decision/Walker.cs
    principal = candidate.delta_price                       # the INCREMENT, never the whole tier price
    if money_creation:
        CreateMoney(Household(h), principal)                # new money against a claim of equal size
    else:
        if pool < principal: return funded = false          # credit rationing: recorded, never halted
        Transfer(Pool -> Household(h), principal)
    AddClaim(principal)
    loans.Add(Loan(h, good, principal, interest = principal * (rate*term/1200), term))
    return funded = true
```

---

## 6. Step 5 — ageing

After the walk, so a unit bought this tick starts at age 0 and is not immediately wanted again.

```
if replacement == "deterministic":       AgeDurables():     # World/Households.cs
    for each good g with life_g > 1, for each household h:
        age[h][g] += 1

if replacement == "hazard":              FailDurables():
    for each household h, for each good g:
        drawn = U()                                         # stream (h, Failure) -- drawn UNCONDITIONALLY,
        age[h][g] += 1                                      #   before anything is asked about holdings
        if holds[h][g] and drawn < hazard[A(h)][g]:
            holds[h][g] = false
```

The unconditional draw is the discipline that keeps the two arms on the same random world: a stream
that advances only when a household happens to hold a unit would desynchronise the moment the arms
diverged. The hazard is memoryless — `p = 1 / life[A(h)][g]` every tick, regardless of age.

---

## 7. Step 6 — repricing

Every shelf moves on **its own** excess demand, and only its own. New prices apply from the next
tick; prices are constant throughout a walk, so the household visited first faces the same price as
the one visited last.

```
Reprice(k, price_floor):                                    # World/Market.cs
    for each shelf (g, t) with units[g][t] > 0:
        demand = sold[g][t] + blocked[g][t]                 # unaffordable is NOT demand
        excess = clamp((demand - units[g][t]) / units[g][t], -1, +1)

        factor[g][t] *= 1 + k * excess
        posted        = opening_price[g][t] * factor[g][t]  # scaled from the OPENING price, never in place
        if posted < price_floor:
            posted       = price_floor
            factor[g][t] = price_floor / opening_price[g][t]
        posted_price[g][t] = posted
```

Two decisions are embedded here:

- **`unaffordable` is excluded from demand.** Counting it would raise prices on goods nobody can
  buy, which makes more households unable to buy them. Excluding `blocked` instead would mean
  demand can never exceed supply and prices only ever fall. Both failures produce a clean series
  that looks like a finding.
- **The price is carried as a dimensionless factor on the opening price** and rounded to the cent
  from there each tick, rather than rounded and re-rounded in place. Cent rounding cannot commute
  with scaling every nominal quantity by `c` (V3); this bounds the discrepancy at half a cent per
  posting instead of compounding it over 600 ticks.
- **Each tier moves on its own signal**, which is what lets *relative* tier prices move — and that
  movement is the trade-down channel. A category-wide signal would freeze the tier mix at the unit
  shares and answer the question by assumption.

---

## 8. Step 7 — the check

V1, run at the end of every tick. A failure stops the run at the tick that caused it.

```
Check(tick, money_creation):                                # Ledger/Ledger.cs
    money_held == M0 + net_money_created                    ... or fail with the cent discrepancy
    net_money_created == (loans_outstanding if money_creation else 0)
    pool >= 0                                               ... reported as a CALIBRATION failure
    cash[h] >= 0 for every h
    loans_outstanding >= 0
    bank == 0                                               ... the bank keeps nothing overnight
```

The last one is not bookkeeping pedantry: interest sitting in the bank's till at the end of a tick
is money that left the households and went nowhere, which would show up later as a slow demand leak
that looks economic.

---

## 9. How the metrics are computed

Nothing in the model reads any of this. An output that feeds back into a decision stops being a
measurement.

### 9.1 At the top of the tick — prices and the index

Two quantities cannot be read off the model afterwards: step 6 changes the prices before the tick
ends, and the next restock clears the demand counters. Both are captured at the moment they are
still true.

```
TickRecord.Open(tick, market, warmup_ticks):                # Output/TickRecord.cs
    is_warmup = (tick <= warmup_ticks)                      # warm-up is flagged, never discarded
    prices_traded[g][t] = posted_price[g][t]                # what THIS tick will trade at

    cpi = sum(prices_traded[g][t] * units[g][t]) / sum(opening_price[g][t] * units[g][t])
    for each good g:   index[g]     = the same ratio restricted to g's three shelves
    for each label l:  index[l]     = the same ratio restricted to every row carrying label l
```

A **Laspeyres index on the fixed supply basket**. Since supply is fixed by construction, that
basket is the only one in this model that cannot be argued with. Fixing it at supply units rather
than at realised purchases is what keeps it a *price* index: a basket tracking what households
actually bought would fall when they trade down, and trading down is precisely the movement the
index has to stay neutral about, because it is the finding. The roll-up to a label is
**unit-weighted**, so a EUR 1,440 washing machine replaced every twelve years counts for its seven
units rather than for its price.

Read the level as a property of the town, not of credit: `1.00` means a good is trading at exactly
the price it was first put on the shelf for, and a shelf can settle well above or below that in
*both* arms. Only the difference between arms is attributable to credit.

### 9.2 During the walk — the cohort series

Every household is in exactly one cohort (`abstainer` if `theta == 0` in every scenario, `borrower`
otherwise) and one archetype; the cut is two-dimensional and the per-cohort figure is the sum over
archetypes. Both dimensions are fixed at initialisation and identical across scenarios for a given
seed, which is what makes the comparison paired in both of them.

```
RecordWant(cell, g):        wanted[cell][g] += 1            # Output/CohortMetrics.cs, from step 3

RecordPurchase(cell, g, t, paid, wait):                     # from the walk's settlement
    obtained[cell][g]      += 1
    spend_by_good[cell][g] += paid
    spend[cell]            += paid
    units[cell][g][t]      += 1                             # the tier mix
    quality[cell]          += value_mult_t                  # the TIER TABLE's multiplier, not the household's
    if durable(g):
        push wait into waits[cell] and waits_met[cell]      # histograms, not lists
```

`quality` deliberately uses the tier table's `value_mult` rather than the household's
`value_mult ^ kappa`: it is a reported quantity and has to mean the same thing for every household,
or a cohort's quality index would move when the population's taste changed rather than when what it
bought did.

### 9.3 At the end of the tick — stocks and the wait medians

```
CohortMetrics.Close(population, ledger, loans):             # Output/CohortMetrics.cs
    for each household h:
        cash[cell]               += ledger.cash[h]
        loans_outstanding[cell]  += loans.OutstandingPrincipal(h)
        debt_service[cell]       += loans.DebtService(h)
        for each durable good g still wanted by h:
            push wait[h][g] into waits[cell]                # the OPEN wants, at their current age

    wait_median[cell]     = Median(waits[cell])             # met wants AND open wants
    wait_median_met[cell] = Median(waits_met[cell])         # met wants only

TickRecord.Close(ledger, loans, rationed):                  # Output/TickRecord.cs
    money_stock, loans_outstanding, pool, money_created, money_destroyed, loans_live, rationed
```

Both wait medians exist, and reading the wrong one is a trap the model walks into on its own:

- `wait_median` includes wants that are still open, and **cannot be compared across ticks**. A
  stable population at the poor end never gets served at all; their wants stay open and age by one
  every tick, so this median climbs with the tick number in a perfectly stationary economy.
- `wait_median_met` covers only the wants actually met this tick, and is the one to read.

Food and leisure are excluded from both, because they are wanted every tick and met or not the same
tick, so their zeros would swamp the number that matters.

### 9.4 What the analysis is allowed to conclude

Two derived quantities are routinely confused, and they differ by roughly a factor of three:

- **`share_of_wanted_obtained` = obtained / wanted.** `wanted` is a *stock* — an unmet want stays
  on the list until it is met — so the denominator grows exactly when a household falls behind.
  This measures how badly a cohort is kept from a good.
- **`obtained` alone** is the count of units that went home. This measures what the cohort
  consumes.

Both are sound; neither is the other. A write-up must not describe a share figure as a count.

Differencing between scenarios happens **outside** the engine. The engine emits raw per-tick series
only, so the comparison can be revised without re-running the campaign.

---

## 10. Determinism

```
stream(run_seed, h, purpose)          # per household, for the life of the run
stream(run_seed, tick, purpose)       # per tick, reseeded in place so the tick allocates nothing
```

Every draw is keyed by an explicit `Purpose`, and the same seed produces a bit-identical run. Three
rules keep it that way, and each of them exists because breaking it produces a run that works and
reports the wrong number:

1. **Draw unconditionally.** A stream that advances only under some condition desynchronises the
   two arms the moment they diverge. `theta` in the baseline and the hazard draw in step 5 are both
   drawn and discarded rather than skipped.
2. **One purpose per stream.** Two quantities sharing a stream means changing one silently changes
   the other.
3. **Per household, not per population.** Adding a household must not disturb anybody else's draws.

A single cent of difference sends two runs completely apart within a few dozen ticks, which is why
no result may be read off one run — only from the mean over thirty seeds, compared in pairs.

---

## 11. Why the order is what it is

Three of the seven orderings have a failure mode that produces a working run with wrong numbers.
They are pinned in `spec/tick-order.txt` and asserted by a test.

| ordering | what breaks if it moves |
|---|---|
| **debt service before the walk** | The affordability test at origination stops being sufficient, and households spend their way into arrears — which v1 has no machinery to handle. |
| **ageing after the walk** | A unit bought this tick is immediately wanted again next tick. |
| **repricing after the walk** | A stockout would move a price mid-walk, so the household visited first faces a different price from the one visited last, and the rationing order silently becomes a price advantage. |

Restocking sits before step 1 and is not a step at all: nothing decides and nothing moves. It is
the definition of the shelf the seven steps then act on.
