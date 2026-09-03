# CPI, and the realised tier mix

**Epic:** E7 — Output
**Depends on:** 07-01, 05-02
**New ground:** The price level, and the mix that the price level hides

## Story

As the model author, I want a fixed-basket price index and the realised tier mix recorded every tick, so that a town that keeps its prices flat by buying worse goods is not recorded as unchanged.

## Acceptance criteria

- [ ] `cpi_t = Σ price_(g,t) · units_(g,t) / Σ price_(g,t,0) · units_(g,t)` — a Laspeyres index on the **fixed unit supply** basket, which is the only basket in this model that cannot be argued with.
- [ ] `cpi_0 = 1` exactly.
- [ ] A **category price index** per category, on the same basis.
- [ ] The **realised tier mix**: share of each category's sales at each tier, every tick, town-wide.
- [ ] Both are **outputs, never inputs**. Nothing in the decision rule reads them.
- [ ] A test asserts the CPI is invariant to the tier mix when prices are unchanged — it is a price index, not a spending index, and confusing the two would hide the trade-down effect inside the headline number.
- [ ] A test scales all prices by `c` and asserts the CPI scales by exactly `c`.

## Where to start

The reason the tier mix is recorded next to the CPI, in the same story, is that either one alone
misleads. A run where credit pushes abstainers from standard to budget can show a *flat* CPI —
prices did not move much, the composition did — and reporting only the index would say nothing
happened. The mix is where the effect appears first and most legibly.

Fixing the basket at the supply units rather than at realised purchases is what keeps the index a
price index. A basket that tracks what people actually bought would fall when they trade down, which
is exactly the movement the index is supposed to be neutral about.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~PriceIndexTests
```

The mix-invariance test, and the scaling test with `c = 2`.
