# Candidates are upgrades, not tiers

**Epic:** E4 — The decision
**Depends on:** 04-01, 04-02
**New ground:** One rule that decides both *whether* to buy and *which quality*

## Story

As the model author, I want each wanted category to offer up to three incremental candidates rather than three alternative tiers, so that a single ranking rule produces the quality ladder instead of a second decision procedure.

## Acceptance criteria

- [ ] Three candidates per wanted category, per `01-SIMULATION.md` §5.1: buy budget (`Δvalue = V·0.68`, `Δcost = P·0.60/life`), budget → standard (`V·0.32`, `P·0.40/life`), standard → premium (`V·0.40`, `P·0.80/life`).
- [ ] `score = Δvalue / Δcost`, dimensionless. A candidate is taken when `score ≥ λ`.
- [ ] An upgrade candidate is **unavailable unless the step below it was taken**.
- [ ] **Upgrade scores are monotone decreasing** within a category — budget > budget→standard > standard→premium — for every household and every reference price, while tier prices stand in their opening ratios. A property test asserts it over randomised prices and incomes. (Once tiers have repriced independently the ordering can invert — budget above 0.68 of standard does it — so the walk must not assume a sorted ladder; a test documents the inversion.)
- [ ] The uniform multipliers on the base score are **1.133 / 0.800 / 0.500** at default tier parameters, and a test derives them from the multipliers rather than hard-coding them.
- [ ] **Increments sum exactly to the tier price**: `0.60·P + 0.40·P = 1.00·P`, and `+ 0.80·P = 1.80·P`. A test asserts this to the cent. (Implementation note: increments are differences of the posted tier prices, `price_t − price_(t−1)`, so they telescope to the tier price by construction and no split is needed; at opening prices they equal the multiples above.)
- [ ] A test reproduces the median household's tier choices from `02-PARAMETERS.md` §3.4 exactly.
- [ ] No allocation: candidates are a fixed-size stack buffer, at most eighteen per household per tick.

## Where to start

This is the story where the obvious implementation is wrong, and wrong in a way that produces a
working run.

Rank the three **tiers** by total score and the cheapest always wins: `value_mult / price_mult` is
0.68/0.60 = 1.133 for budget against 1.00 for standard and 0.778 for premium, so every household
buys budget everything, premium never sells at any price, and the model quietly loses the entire
mechanism the tiers were added for. Only the **increment** can justify an upgrade, because only the
increment answers the question the household is actually asking: is the next step up worth what it
costs?

The monotonicity property is the same fact seen from the other side, and it holds only because
`value_mult` rises more slowly than `price_mult`. It is worth a property test rather than an example
test: if a future parameter change inverts it, the ladder inverts, everyone jumps to premium, and
the output still looks like output. 02-02 rejects that configuration at load; this test catches it
if the check is ever weakened.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CandidateTests
```

The monotonicity property over randomised inputs, the increments summing to the tier price, and the
median household's ladder matching the specification table row for row.
