# Accounts, the pool, and transfers

**Epic:** E3 — The ledger and the opening state
**Depends on:** 01-02, 01-04
**New ground:** The four places money can be, and the only ways it moves

## Story

As the model author, I want every euro in a named account and every movement in one transfer operation, so that later stories move money between places rather than inventing fields.

## Acceptance criteria

- [ ] Accounts by **kind**, not by agent type: household cash, the pool, and `loans_outstanding` as a claim. The conservation sum is over kinds, so a new kind of holder later does not require editing the check.
- [ ] Every monetary field is `Money`. No field is a `double`.
- [ ] Exactly one transfer operation, and it is the only thing that changes a balance. It takes a source, a destination, an amount and a **reason** from a closed enum: `income`, `purchase`, `interest_dividend`, `loan_origination`, `repayment_principal`.
- [ ] Money creation and destruction are their own named operations, distinct from transfer, and each records the amount for the tick's diagnostics.
- [ ] A transfer that would take an account negative **fails**; it does not clamp and it does not warn.
- [ ] Balances are index-addressed arrays. Nothing in the engine holds a reference to a household.
- [ ] Derived quantities — net worth, debt service, residual — are **computed, never stored**. A stored copy is a second source of truth that will drift.

## Where to start

Naming the reason on every transfer costs nothing and buys the tick's money diagnostics for free:
the sum of purchases must equal the pool's increase from goods, the sum of income must equal the
pool's decrease, and the two creation operations must reconcile with the change in
`loans_outstanding`. Those are the sub-invariants that tell you *where* V1 broke, not just that it did.

Keeping creation and destruction separate from transfer is the distinction the whole money story
rests on. A loan origination is not a transfer from anywhere — that is the point of it — and an
implementation that models it as a transfer from the pool has quietly built the
`money_creation = false` variant and called it the default.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LedgerTests
```

A random sequence of a few thousand transfers leaving the conservation sum unchanged, and a transfer
that would overdraw returning a failed `Result`.
