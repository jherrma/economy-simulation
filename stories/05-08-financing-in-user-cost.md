# The financing term in `user_cost`

**Epic:** E5 — The purchase decision
**Milestone:** M2
**Depends on:** 05-02, 07-01
**New ground:** The interest rate inside the durables-versus-services comparison

## Story

As the model author, I want financing to enter the denominator as one more per-tick addend, so that credit's effect on the goods mix appears through the ranking itself rather than through a separate code path.

## Acceptance criteria

- [ ] `user_cost = depreciation + running_cost + financing`, with financing as a term **added** to 05-02's expression — not a second version of it (S5).
- [ ] Financing is `r_l/1200 · outstanding` if the unit is financed, or `r_d/1200 · price` — interest forgone — if it is bought outright.
- [ ] With `credit_enabled = false` the financing term is the forgone-interest form only, and the milestone reproduces M1 byte for byte (**V7**).
- [ ] Since `r_l > r_d` always, financing strictly **worsens** a unit's score. A test asserts this for every one of the twelve sectors.
- [ ] A test shows the mix channel: lowering `r_l` raises the share of durables in realised spending relative to services, with nothing else changed.
- [ ] The `Rate` type supplies the divisor; no literal `1200` and no literal `12` appears at this call site (01-03).
- [ ] Nominal invariance survives: doubling every nominal quantity leaves `score` unchanged (V3).
- [ ] No allocation on this path.

## Where to start

The 'financing always worsens the score' test is the one that keeps the model honest, and it is
worth understanding why before implementing around it. Credit never makes anything look cheaper
here. What it does is **widen the choice set** — it makes reachable a unit whose score was already
above λ but whose price was not affordable this tick. If a bug ever makes financing attractive on
price, the thesis is being assumed rather than tested, and the resulting number would be a
tautology dressed as a finding.

The reason this term lives in the denominator at all, rather than in an affordability check
alongside it, is that it puts the interest rate **inside** the comparison between durables and
services. When credit is cheap, durables get cheaper per tick relative to services and the mix
shifts; when it is dear, the reverse. That channel is central to what the model is testing and a
total-cost formulation could not express it.

Resist the shortcut of computing `user_cost` twice — once financed, once not — and taking the
better. Both forms are the same expression with a different addend, and forking §6.2 is the one
fork this project cannot afford: it is 95% of the tick, it is under a performance gate, and a fork
would mean the V7 comparison runs different code on each side.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~FinancingTermTests
dotnet run --project tools/Regress -- compare --milestone M1
```

The financing-worsens-score assertion across all twelve sectors, and the M1 comparison still
byte-identical with credit disabled.
