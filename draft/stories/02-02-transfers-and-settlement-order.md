# Transfers, and settlement cash-first

**Epic:** E2 — The ledger
**Milestone:** M0
**Depends on:** 02-01
**New ground:** The only operations permitted to move money, and what each does to reserves

## Story

As the model author, I want a small set of transfer operations that are the *only* way money moves, so that base money cannot be created or destroyed by an ordinary payment.

## Acceptance criteria

- [ ] Paying an amount draws **cash first**, then demand deposits for the remainder (§6.2).
- [ ] A cash payment moves base money between two holders and changes neither the total nor bank reserves.
- [ ] A deposit-to-deposit payment moves deposit claims and leaves reserves **unchanged** — there is one bank, so nothing settles externally.
- [ ] Depositing cash raises reserves and demand deposits by the same amount; withdrawing does the reverse.
- [ ] A payment that cannot be funded returns a failed `Result` and moves nothing. Partial settlement is not possible.
- [ ] Every operation is total: it either applies fully or leaves the ledger untouched.
- [ ] A test asserts that no sequence of transfers changes `M0`.

## Where to start

The cash-first rule is not cosmetic and the specification says so: it determines where base money
sits, which determines the bank's reserves, which determines lending capacity. A town that transacts in
cash tightens its own credit supply through nothing but settlement habits.

Make these operations the only public way to change a balance. If a later story can assign to a balance
field directly, V1 becomes a lottery — you will be hunting for which of forty call sites created the
cent. Keeping the mutation surface tiny is what makes the invariant cheap to trust.

'Either fully or not at all' matters more than it looks: a payment that half-applies and then fails
leaves the ledger inconsistent at the exact moment an error path is being taken, which is the hardest
state to reproduce.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TransferTests
```

The reserves assertions. A deposit-to-deposit payment changing reserves is the classic error and
it makes fractional-reserve lending capacity drift for no visible reason.
