# Pricing and production

**Epic:** E9 — Firms
**Depends on:** 04-03, 05-05
**New ground:** Firms observing sales and moving prices without knowing the demand curve

## Story

As the model author, I want prices set by markup over unit cost, adjusted by inventory and utilisation, so that the price level responds to demand through a mechanism no agent can see whole.

## Acceptance criteria

- [ ] Markup over unit cost, adjusted by inventory and capacity utilisation: above `target_util` with falling inventory, raise by up to `price_step`; accumulating inventory, cut.
- [ ] **No firm knows the demand curve.** Pricing uses only what the firm can observe.
- [ ] Output is limited by capacity; unit cost rises above `soft_capacity` as utilisation approaches the limit.
- [ ] Wages are the whole of marginal cost — there are no intermediate goods (D22).
- [ ] `elasticity_g` governs how readily the sector's supply expands, per the numeric values in §13.4.
- [ ] A test shows the elastic and inelastic sectors responding differently to the same demand increase — this is the mechanism the §4 grid rests on.

## Where to start

The markup rule pins nothing on its own: it is homogeneous of degree one in the price level. What
anchors the level is the **quantity of money** — if every price doubled while the money stock did not,
households could not clear the market, inventory would accumulate, and this rule would cut. That is worth
understanding before implementing, because it is why V3 can pass at all and why the price level is not a
free parameter.

The differential response by elasticity is the experiment in miniature. If elastic and inelastic sectors
move together under a demand shock, the §4 grid is not doing anything and every downstream price result
is suspect.

Keep pricing observational. The moment a firm consults anything global — total demand, other firms'
prices — the model acquires a coordination it does not claim to have.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~PricingTests
```

The elastic/inelastic divergence under an identical demand shock, and inventory behaving as the
signal that drives price down.
