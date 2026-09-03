# Inside claims net to zero

**Epic:** E2 — The ledger
**Milestone:** M0
**Depends on:** 02-03
**New ground:** The second invariant, and the correction of one that was wrong in the spec

## Story

As the model author, I want deposits and loans asserted to net to zero, separately from the base-money identity, so that the aggregate identity is stated correctly rather than in the form that is false here.

## Acceptance criteria

- [ ] Every *inside* claim nets to zero: a deposit is the bank's liability and someone's asset; a loan is the reverse.
- [ ] The aggregate identity asserted is `Σ net financial assets = M0`, **not** 'all financial assets equal all liabilities' — cash is outside money with no offsetting liability, so the second form is false here and must not be coded (§10 item 1, D4).
- [ ] Time deposits are included on both sides.
- [ ] Bank equity is the residual and is asserted to be consistent with assets minus liabilities.
- [ ] A test constructs a ledger in which the *wrong* identity would pass and the right one fails, so the two cannot be confused later.

## Where to start

D4 originally asserted the wrong invariant, and it survived into a written decision before anyone
noticed. The reason it is seductive is that it is true in most closed models — those without physical
cash. Here the public holds base money that nobody owes them, so financial assets exceed liabilities by
exactly `M0`, which is the whole point of modelling a commodity-money case at all.

The test that distinguishes the two forms is worth writing carefully: a ledger with no cash satisfies
both, so the distinguishing case must have cash in public hands. That is also the case a future
maintainer will reach for when 'simplifying' this check.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~InsideClaimsTests
```

The discriminating test. If it passes under both forms of the identity, it is not discriminating.
