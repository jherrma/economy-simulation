# Valuation, and defining `credit_limit`

**Epic:** E8 — Housing
**Milestone:** M6
**Depends on:** 08-01, 05-03
**New ground:** One valuation rule at two horizons, and a term that appeared nowhere

## Story

As the model author, I want a dwelling valued by capitalising the same per-tick `value` the ranking uses, and `credit_limit` defined, so that housing is not governed by three rival rules and the auction has a bid it can compute.

## Acceptance criteria

- [ ] `valuation = value_housing(d) · capitalisation_factor`, where `value_housing` is **the same §6.2 function**, computed once and used twice.
- [ ] The separate renter's 'instalment plus running costs against rent' comparison is **deleted** — renting and buying are two candidates in the same ranking (05-04).
- [ ] `credit_limit = min(dsti_max · income / instalment_per_euro(term, r_l), ltv_max · assessed_value)`.
- [ ] The LTV circularity is broken by a **lag**: `assessed_value` is the previous tick's clearing price for that quality tier, which is also what a real valuer uses. At t = 0 it is the configured opening price.
- [ ] Three horizons coexist and are **not** the same quantity: `ownership_horizon_housing` 360 (ownership horizon), `term_housing` 300 (mortgage), `capitalisation_factor` 240 (valuation).
- [ ] A test asserts the numerator of the capitalised valuation is byte-identical to the per-tick value used in the ranking.

## Where to start

Three rival housing valuations coexisted in the specification with nothing saying which drove the
ranking, which drove the bid and which drove rent-versus-buy. The resolution is one rule applied at two
horizons — per tick for the decision, capitalised for the bid — and the criterion that the numerator is
literally the same computation is what keeps them from drifting apart again.

`credit_limit` appeared in §6.5 and nowhere else, which meant an implementer had to invent it, including
the resolution of its circularity: the loan is capped at a fraction of a price the auction has not yet
discovered. The one-tick lag is both the simplest fix and the realistic one.

The three horizons are worth a comment in the code. They look like they should be equal, someone will
eventually 'fix' them to match, and nothing requires it.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~HousingValuationTests
```

The identical-numerator assertion, and `credit_limit` at t = 0 using the configured opening price
rather than a zero or an uninitialised value.
