# CPI, and the realised tier mix

**Epic:** E7 — Output
**Depends on:** 07-01, 05-02
**New ground:** The price level, and the mix that the price level hides

## Story

As the model author, I want a fixed-basket price index and the realised tier mix recorded every tick, so that a town that keeps its prices flat by buying worse goods is not recorded as unchanged.

## Acceptance criteria

- [x] `cpi_t = Σ price_(g,t) · units_(g,t) / Σ price_(g,t,0) · units_(g,t)` — a Laspeyres index on the **fixed unit supply** basket, which is the only basket in this model that cannot be argued with.
- [x] `cpi_0 = 1` exactly.
- [x] A **category price index** per category, on the same basis.
- [x] The **realised tier mix**: share of each category's sales at each tier, every tick, town-wide.
- [x] Both are **outputs, never inputs**. Nothing in the decision rule reads them.
- [x] A test asserts the CPI is invariant to the tier mix when prices are unchanged — it is a price index, not a spending index, and confusing the two would hide the trade-down effect inside the headline number.
- [x] A test scales all prices by `c` and asserts the CPI scales by exactly `c`.

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

## Implementation note (2026-09-03)

The index is computed from a `ReadOnlySpan<Money>` of the tick's traded prices rather than from a
lookup function. A `Func<int, int, Money>` handed to it is a delegate allocation, and the index is
computed inside the tick, so the walk's zero-allocation guarantee (04-05) failed the moment the
first version ran. `Market.Prices` exposes the same span for the tests.

"Outputs, never inputs" is asserted by scanning the engine's own sources: outside `Engine/Output`
nothing may mention the index at all.
