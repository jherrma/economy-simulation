# Base-money conservation — **V1**

**Epic:** E2 — The ledger
**Milestone:** M0
**Depends on:** 02-02
**New ground:** The invariant that everything afterwards is developed against

## Story

As the person who will defend these results, I want `M0 = public cash + firm cash + bank reserves` asserted to the cent after every operation, so that an accounting bug is caught on the tick it happens rather than inferred from a strange result 200 ticks later.

## Acceptance criteria

- [ ] The identity includes **firm cash**. Firms have held cash since D8 and an earlier draft of the invariant omitted them.
- [ ] The check is exact — integer cents, no tolerance. A tolerance here would hide precisely the rounding bugs it exists to catch.
- [ ] It runs at the end of every tick, and in tests after every individual operation.
- [ ] A test deliberately creates a cent and asserts the check **fails**. An invariant never seen to fail has not been shown to work.
- [ ] At `reserve_ratio = 1.0` the assertion on broad money is `M ≤ M0`, **not** `M` constant — the reserve rule is an inequality and a bank holding excess reserves may still lend (§10 item 2).
- [ ] The failure message names the tick, the amount of the discrepancy and its sign.
- [ ] It is cheap enough to leave on in every run, not only in tests.

## Where to start

This is the project's white-furnace test — one invariant, trivially cheap, that any bug creating or
destroying money violates immediately. Its value comes from being on **always**, so resist any
temptation to make it a debug-only check.

The deliberate-failure test in the criteria is the part people skip and the part that matters. An
assertion that has never been observed to fire might be asserting nothing at all; the classic version
of this bug is comparing a value with itself.

Note what this invariant does *not* cover, so you do not over-trust it: it says money was neither
created nor destroyed, not that it went to the right agent. Inside claims are 02-04, and correctness of
the economics is not a matter for assertions at all.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~BaseMoneyTests
```

Both directions: the honest ledger passes and the sabotaged one fails, naming the cent.
