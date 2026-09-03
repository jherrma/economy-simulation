# `flow_cost`, and prices per tier

**Epic:** E4 — The decision
**Depends on:** 02-03, 01-03
**New ground:** The denominator as a per-tick flow, with financing as a multiplier

## Story

As the model author, I want the cost of a unit expressed as a per-tick flow rather than a purchase price, so that a phone and a restaurant meal are comparable and credit's effect on the goods mix can appear at all.

## Acceptance criteria

- [ ] `flow_cost(g, t) = price_(g,t) / life_g`, per tick. Not the purchase price.
- [ ] `finance_mult(g) = 1 + loan_rate · term_g / 1200`, from `Rate` (01-03), and a financed candidate's cost is the cash cost times it.
- [ ] **Financing strictly worsens a candidate's score**, for every category and every tier. A test asserts this exhaustively — it is the assertion that keeps the model honest.
- [ ] For a category with `life = 1` there is no financing and `flow_cost` collapses to the price.
- [ ] A test compares a phone and a month of food and asserts neither is favoured by the *form* of the expression: both sides are flows.
- [ ] No allocation on this path.

## Where to start

The per-tick form is load-bearing. Dividing a per-tick value by a *total* price loads an arbitrary
bias between durables and non-durables into the ranking, and since the financeable categories are
all durables, that bias would land squarely on the primary result.

The construction earns its place for a second reason: it puts the interest rate **inside** the
comparison between durables and non-durables. When credit is cheap, durables get cheaper per tick
relative to food and leisure and the mix shifts; when it is dear, the reverse. A total-cost
formulation could not express that channel at all.

The financing-worsens-score test is the one that keeps the hypothesis testable. Credit never makes
anything look cheaper here; what it does is put a tier within reach that cash could not pay for. If
a bug ever makes financing attractive on price, the thesis is being assumed rather than tested, and
the number that comes out is a tautology.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~FlowCostTests
```

The financing-worsens-score assertion across all six categories and all three tiers.
