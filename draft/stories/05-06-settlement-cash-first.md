# Settlement into the ledger

**Epic:** E5 — The purchase decision
**Milestone:** M1
**Depends on:** 05-05, 02-02
**New ground:** Purchases becoming money movements, and reserves responding

## Story

As the model author, I want each purchase settled through the 02-02 transfer operations, so that the circulation of base money — and therefore lending capacity — follows from how the town pays.

## Acceptance criteria

- [ ] Every purchase pays **cash first**, then demand deposits (§6.2).
- [ ] Households pay firms; firms keep `firm_cash_preference` of their buffer as cash and deposit the rest, returning those reserves to the bank.
- [ ] A financed purchase creates a loan and a deposit simultaneously, and the deposit is spent in the same tick.
- [ ] V1 holds after every purchase, not merely at the end of the tick.
- [ ] A test shows that a town transacting heavily in cash has lower reserves and tighter credit than an otherwise identical town using deposits — with no change in anyone's saving behaviour.
- [ ] Purchases settle at step 10 of the tick, after credit applications at step 9.

## Where to start

This story is where the settlement habits of §6.2 stop being a description and start having
consequences. The test in the criteria is the one to write first, because it demonstrates the mechanism
the specification claims: `κ` and `firm_cash_preference` together govern how much base money sits outside
the vault, and that alone moves lending capacity.

Assert V1 after every purchase during tests rather than only at step 17. When it fails at the end of a
tick you have 96,000 purchases to search; when it fails on the purchase that caused it you have one.

Note the ordering: credit is applied for at step 9 and purchases settle at step 10, so a household that
was granted a loan spends it in the same tick. That is deliberate and it is what makes credit expand
demand within the tick rather than the next one.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~SettlementTests
```

The cash-versus-deposit town comparison. Reserves should differ measurably with identical saving
behaviour — if they do not, cash payments are being routed through deposits somewhere.
