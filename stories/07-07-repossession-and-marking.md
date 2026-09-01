# Repossession, marked in two steps

**Epic:** E7 — Credit
**Depends on:** 07-06, 07-03
**New ground:** Where the loss hits equity, and when

## Story

As the model author, I want collateral taken at repossession and marked immediately, with the residual taken on sale, so that bank equity is not overstated during the holding period, when the capital constraint most needs to bite.

## Acceptance criteria

- [ ] At **repossession**: the loan of principal `P` leaves, an asset valued `V` arrives, and equity moves by `min(P, V) − P` immediately.
- [ ] At **sale**: the residual between carrying value and realised price is taken.
- [ ] An earlier draft took the whole loss 'on sale', leaving equity overstated for up to `liquidation_ticks` — a test asserts the two-step behaviour explicitly.
- [ ] Repossessed assets carry risk weight 1.0 while held.
- [ ] Write-offs reduce equity, which under an active capital constraint reduces lending capacity — the mechanism that turns a wave of defaults into a credit crunch.
- [ ] Dwellings re-enter the housing auction as supply with the bank as a forced seller (§5.3.1, E8).
- [ ] Cars and durables enter the second-hand market (07-08).

## Where to start

The timing is the whole story. Equity drives `h_capital`, so overstating it for six ticks lets the bank
keep lending through exactly the episode the capital constraint exists to catch. The specification's own
correction of this is worth reading before implementing.

Resist the temptation to net the two steps into one. They happen at different ticks and the intermediate
state is observable in the metrics — that is the point.

Note the direction of the feedback and make sure it is present rather than assumed: defaults reduce
equity, reduced equity reduces `h_capital`, reduced headroom raises `r_l` and tightens lending, which
raises defaults. That loop should be visible in a stressed run, and if it is not, something is not
wired.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~RepossessionTests
```

Equity moving at repossession rather than only at sale, and the crunch feedback visible in a
high-default scenario.
