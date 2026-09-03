# The finance decision inside the walk

**Epic:** E6 — Credit
**Depends on:** 06-01, 04-05
**New ground:** θ, the myopic residual test, and what credit actually changes

## Story

As the model author, I want a household that cannot pay cash for a candidate to be able to finance it, subject to θ and to the instalment fitting this month, so that credit widens the choice set without ever making anything look cheaper.

## Acceptance criteria

- [x] Reached only when the cash branch failed, the category is `financeable`, and `credit_enabled`.
- [x] A draw from the household's `finance` stream must be `< θ_h`. Abstainers have `θ = 0` and never reach the next condition.
- [x] The **financed score** — cash score divided by `finance_mult(g)` — must still clear λ.
- [x] The instalment must fit **this tick**: `instalment ≤ income_h − debt_service_h − subsistence_share · income_h`.
- [x] **The horizon is one tick, not the loan term.** `affordability_horizon = full_term` is the control, and a test asserts loan stacking all but disappears under it.
- [x] The loan is originated for `Δcost`, not for the whole tier price — the increments and their loans line up (04-03).
- [x] A test shows the mechanism in miniature: two identical households, one with cash and one without, end on **different tiers**, and the difference is position rather than preference.
- [x] A test asserts a household may carry loans on several categories at once, bounded by the residual.

## Where to start

The myopic horizon is a behaviour under test, not an assumption to hide. It is the model's version
of the observation that a household short of cash looks at the monthly payment while a household
with cash looks at the price, and the control setting exists precisely so the result can be
attributed to it rather than assumed from it.

The two-identical-households test is the sentence the eventual write-up will make, so it is worth
having as a named test rather than as a chart someone reads off later: a household with cash buys
the better tier outright; an identical household without cash either finances it or drops a tier,
and which of those happens is what θ decides.

Financing the *increment* rather than the unit is exact, not approximate. Because `finance_mult` is
uniform for a category, the total interest is identical either way, and increments keep the
bookkeeping aligned with the candidate model.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~FinanceDecisionTests
```

The identical-households test landing on different tiers, and stacking collapsing under
`full_term`.

## Implementation note (2026-09-03)

`full_term` had no definition beyond "the control"; it is now the whole repayable amount against
the residual (`01-SIMULATION.md` §6 step 4). The identical-households test runs at €1000 with the
reservation price on money (§5.3) switched off: at €650 no standard step of a financeable durable
clears λ even in cash, so there is nothing for credit to reach, and with §5.3 on the cash-rich
household's λ falls and it takes premium, which muddles a test about position. The finance branch
is checked against the household's λ_h, not λ, so the two thresholds a household applies are the same.
