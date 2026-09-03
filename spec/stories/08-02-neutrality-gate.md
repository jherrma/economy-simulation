# **V3**: the nominal neutrality gate

**Epic:** E8 — Validation gates
**Depends on:** 08-01
**New ground:** Proving there is no money illusion in the decision rule

## Story

As the person who will defend these results, I want a gate that scales every nominal quantity and asserts nothing real changes, so that the price level is anchored by the money stock rather than by a decision rule.

## Acceptance criteria

- [ ] Multiply **simultaneously** by `c`: `M0`, all cash, the pool, all opening tier prices, all incomes, `price_floor`, `a_g`, and all outstanding loan principals.
- [ ] Leave alone: `loan_rate`, `λ`, `b_g`, `v_g`, `necessity_g`, `k`, all shares and all multipliers. They are pure numbers.
- [ ] **Nothing real may change**: units sold per tier per tick, `share_of_wanted_obtained`, `wait`, `tier_mix` and `quality_index` must be identical; every price and balance must be exactly `c` times what it was.
- [ ] Run at `c = 2` and `c = 0.5`, in `credit_off` and `credit_high`.
- [ ] The gate fails loudly if `price_floor` was left unscaled, and a comment says why that is the likely first failure.

## Where to start

Note that `a_g` scales and `b_g` does not. `a_g` is a floor in euros per tick, so it is nominal;
`b_g` is a coefficient on income, so it is a pure number and scaling it would double-count. Getting
that backwards is the second most likely failure here and it produces a *near*-neutral result, which
is worse than an obviously broken one.

This gate exists because of the defect that took longest to find in the draft model: a value in
utility units divided by a cost in euros silently pinned the real price level to the utility scale,
so doubling all prices halved every score while the threshold stood still and demand collapsed for
no economic reason. The current design is homogeneous of degree zero by construction — `Δvalue`
scales with income, `Δcost` scales with price — which means this gate should pass exactly, not
approximately. If it passes only to within a tolerance, something is not scaling and the tolerance
is hiding it.

## How to verify

```sh
dotnet run --project tools/Gates -- neutrality
```

Exact equality on every real series at both scale factors, in both scenarios.
