# `user_cost`: depreciation and running cost

**Epic:** E5 — The purchase decision
**Milestone:** M1
**Depends on:** 05-01, 01-03
**New ground:** The denominator, as a per-tick flow

## Story

As the model author, I want the cost of a unit expressed as a per-tick flow rather than a purchase price, so that a car and a restaurant meal are comparable, and credit's effect on the durables/services mix can appear at all.

## Acceptance criteria

- [ ] `user_cost = (V(age) − V(age+1)) + financing + running_cost`, per tick (§6.2), **with `financing ≡ 0` until 05-08 adds it at M2** (S5) — the value **actually lost this tick**, from the §4.3 curve. Not `price / life`.
- [ ] **One curve, three uses.** The same `V(age)` function serves user cost, the second-hand opening ask (07-08) and collateral value for LTV and risk weight. A test asserts all three call it rather than reimplementing it.
- [ ] Cars use the **exponential** curve at `car_depreciation_rate` (0.16 p.a.), the same rate for every car regardless of price. Everything else is straight-line; housing has its own rule.
- [ ] A test pins the shape: a car is worth ~59% of new at 3 years, ~42% at 5, ~17% at 10.
- [ ] A test shows depreciation is **front-loaded**: a new car costs ~€440/tick against ~€289 for a five-year-old one — the gap that makes the second-hand market the cash buyer's substitute.
- [ ] For a non-storable service `durability = 1` and there is no running cost, so `user_cost` collapses to the price.
- [ ] **Housing is the exception**: it uses its own low-rate curve, because its 360 ticks are an ownership horizon and not the building's physical life (§6.5).
- [ ] A test compares a car and a meal and asserts neither is favoured by the *form* of the expression — both sides are flows.
- [ ] No allocation on this path.

## Where to start

The per-tick form is the load-bearing part. Dividing a per-tick value by a *total* price loads an
arbitrary bias between durables and services into the ranking, and since the financeable goods are almost
all durables, that bias would land squarely on the primary result.

The user-cost construction earns its place for a second reason, which does not pay off until M2: it puts
the interest rate **inside** the comparison between durables and services rather than in a separate
affordability check. Leave room for that addend now — 05-08 adds it — and do not fork the expression when
it arrives.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~UserCostTests
```

The three-year / five-year / ten-year car shape, the front-loading gap that makes the second-hand market
worth having, and housing not using the generic depreciation rule.
