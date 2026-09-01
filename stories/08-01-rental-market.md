# The rental market, and sitting tenants

**Epic:** E8 — Housing
**Depends on:** 04-05, 05-03
**New ground:** A tick step that the specification had no place for

## Story

As the model author, I want rents set by a clearing market at step 4, with sitting tenants re-priced slowly, so that 40% of the town has a defined housing cost, and whether a price rise reaches them is a result.

## Acceptance criteria

- [ ] The rental market clears at **step 4**, before obligations are computed at step 5.
- [ ] Landlords list **vacant** dwellings at `rent_target_yield` against assessed value, adjusted down toward vacancy and up when nothing is vacant.
- [ ] Tenants bid what their §6.2 valuation supports; the quality tier clears.
- [ ] **Rent is not a fixed yield on the sale price.** A fixed yield would pass every credit-driven price rise straight into rents, making renters worse off by definition and largely determining the distributional result before any agent acted.
- [ ] **Sitting tenants** move toward market rent by at most `sitting_tenant_lag` per year — the *Kappungsgrenze* in model form.
- [ ] Realised yield, vacancy rate, and the gap between sitting and market rents are all reported.
- [ ] A test shows realised yield departing from `rent_target_yield` during a boom, since the realised yield is an output.

## Where to start

§6.5 replaced the fixed-yield rule and §6.1 was never given a step to run the replacement in — the two
sections each assumed the other handled rent formation, with 40% of the town renting. That is why this
story exists as its own unit rather than as part of the auction.

The sitting-tenant lag is not decoration. With this many renters, whether a credit-driven price rise
reaches existing tenants this year or over a decade is one of the larger distributional questions the
model can answer, and making all rents re-clear instantly would answer it by assumption. It also feeds
the headline: abstainers who never buy face rent, so the primary result's housing channel runs through
this parameter.

Note what is deliberately missing: roughly a tenth of German rental stock is cooperative or municipal and
priced on cost rather than market. The model has no such landlord, which makes rents more price-responsive
than reality.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~RentalMarketTests
```

Realised yield diverging from target under a boom, and sitting rents lagging market rents by no more
than the configured rate.
