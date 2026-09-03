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
all incomes, `price_floor`, and all outstanding loan principals. Leave `loan_rate`, `λ`, `v_g`,
`k` and every share alone — they are pure numbers.

**Nothing real may change.** Units sold per good per tick, `share_of_wanted_obtained`, `wait`, and
every cohort comparison must be identical; every price and every balance must be exactly `c` times
what it was.

This holds by construction, and it is worth understanding why rather than trusting it:
`flow_value` scales with income, `flow_cost` scales with price, so `score` is homogeneous of degree
zero and λ never needs indexing. Affordability compares two quantities that both scale. This is the
defect that took the longest to find in the draft model — a value in utility units divided by a cost
in euros silently pinned the real price level to the utility scale, so doubling all prices halved
every score while the threshold stood still, and demand collapsed for no economic reason.

Run V3 with `c = 2` and `c = 0.5`, both scenarios.

## V4 — The null run

With `credit_enabled = false`, prices must be **stationary after the warm-up**: the CPI over ticks
121–360 has no significant trend, and each of the eighteen tier prices is flat to within a tolerance.

**The realised tier mix must also be stationary.** The opening unit shares (40/40/20) are
deliberately not an equilibrium, and the warm-up exists mainly so relative tier prices can find one.
A mix still drifting at tick 120 means the measured window is contaminated by the transient, and the
symptom is easy to miss because the CPI can look flat while the mix underneath it is still moving.

**The pool must have stopped falling.** It drains during the transient by design and carries twelve
months of income for that reason. A pool still declining at the end of warm-up is the same failure
seen from the money side, and it is the cheaper of the two to check.

> **Status 2026-09-03:** this criterion fails under the specification as written, structurally —
> the pool falls by about a fifth of income per tick after convergence. See `01-SIMULATION.md` §7.2.
> The gate cannot be built until that is resolved.

Posted prices under V3 agree to within one cent after scaling, not exactly: cent rounding cannot
commute with scaling by `c`. The engine carries each price as a factor on its opening price so the
discrepancy is bounded at half a cent per posting rather than compounding. A knife-edge decision
flipped by that cent is possible in principle and must be reported by the gate if it occurs.

Supply is fixed, income is fixed, and the money stock is constant, so there is nothing in this model
that should make the price level move. If it drifts, the price rule is not converging, and any
credit effect measured later would be that drift plus an unknown amount of signal.

Also check the warm-up actually decayed: the first 120 ticks are written and flagged, not discarded,
so that the transient can be inspected rather than assumed.

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
| Tier shares are constant across every scenario | Tier prices repriced on a category-wide signal instead of per tier, so relative prices cannot move and the mix cannot clear |

## Running order

1. V1 and V6 from the first commit that moves money. They are assertions inside the tick, not a
   separate suite.
2. V2 as soon as there is output to compare.
3. V4 as soon as the price rule exists — before credit, because a drifting baseline makes every
   later number meaningless.
4. V3 before the first result is quoted anywhere.
5. V5 the moment credit is added, and for every mechanism added after it.
