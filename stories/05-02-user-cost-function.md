# `user_cost`: depreciation, financing, running cost

**Epic:** E5 — The purchase decision
**Depends on:** 05-01, 01-03
**New ground:** The denominator, as a per-tick flow, with the interest rate inside it

## Story

As the model author, I want the cost of a unit expressed as a per-tick flow rather than a purchase price, so that a car and a restaurant meal are comparable, and credit's effect on the durables/services mix can appear at all.

## Acceptance criteria

- [ ] `user_cost = (V(age) − V(age+1)) + financing + running_cost`, per tick (§6.2) — the value **actually lost this tick**, from the §4.3 curve. Not `price / life`.
- [ ] **One curve, three uses.** The same `V(age)` function serves user cost, the second-hand opening ask (07-08) and collateral value for LTV and risk weight. A test asserts all three call it rather than reimplementing it.
- [ ] Cars use the **exponential** curve at `car_depreciation_rate` (0.16 p.a.), the same rate for every car regardless of price. Everything else is straight-line; housing has its own rule.
- [ ] A test pins the shape: a car is worth ~59% of new at 3 years, ~42% at 5, ~17% at 10.
- [ ] A test shows depreciation is **front-loaded**: a new car costs ~€440/tick against ~€289 for a five-year-old one — the gap that makes the second-hand market the cash buyer's substitute.
- [ ] Financing is `r_l/1200 · outstanding` if financed, or `r_d/1200 · price` — interest forgone — if bought outright.
- [ ] For a non-storable service `durability = 1` and there is no financing or running cost, so `user_cost` collapses to the price.
- [ ] **Housing is the exception**: it uses its own low-rate curve, because its 360 ticks are an ownership horizon and not the building's physical life (§6.5).
- [ ] Since `r_l > r_d` always, financing strictly **worsens** a unit's score. A test asserts this for every good.
- [ ] A test compares a car and a meal and asserts neither is favoured by the *form* of the expression — both sides are flows.
- [ ] No allocation on this path.

## Where to start

The per-tick form is the load-bearing part. Dividing a per-tick value by a *total* price loads an
arbitrary bias between durables and services into the ranking, and since the financeable goods are almost
all durables, that bias would land squarely on the primary result.

The user-cost construction earns its place for a second reason: it puts the interest rate **inside** the
comparison between durables and services. When credit is cheap, durables become cheaper per tick relative
to services and the mix shifts; when it is dear, the reverse. That channel is central to what the model
is testing and a total-cost formulation could not express it.

The 'financing always worsens the score' test is the one that keeps the model honest. Credit never makes
anything look cheaper here — what it does is widen the choice set. If a bug ever makes financing
attractive on price, the thesis is being assumed rather than tested.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~UserCostTests
```

The financing-worsens-score assertion across all twelve sectors, and housing not using the generic
depreciation rule.
