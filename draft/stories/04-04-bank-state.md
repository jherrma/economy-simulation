# Bank state

**Epic:** E4 — The world, and an empty tick
**Milestone:** M0
**Depends on:** 02-01, 04-03
**New ground:** The single bank, as both intermediary and employer

## Story

As the model author, I want the bank carrying reserves, its loan book, both deposit classes, equity and its own staff, so that §7's constraints and §5.3's payroll have a defined subject.

## Acceptance criteria

- [ ] Reserves, loans by type, demand deposits, time deposits, equity.
- [ ] Loans carry type, principal, rate, remaining term, collateral and borrower — enough for risk weighting (§13.7) and for repossession (§7.2).
- [ ] Risk weights per CRR3: mortgages LTV-graduated 0.20–0.70, consumer 0.75, firm 1.0, repossessed 1.0.
- [ ] The bank employs `bank_headcount` staff on the same tier structure and distributes profit to shareholders in the same register as any firm — there is **no** separate `bank_shares` field (§5.1.3).
- [ ] Its payout is **annual**, at its own staggered fiscal year end, on the §5.2.2 rule (D28). Interest accrues every tick; the dividend does not.
- [ ] Bank profit is interest received − interest paid − write-offs − **its own wage bill**.
- [ ] A test asserts the bank appears in the share register and in the §9 net-worth definition, since omitting it would understate the wealth Gini, which is C4.

## Where to start

The bank being an employer was added to the specification later than the rest of §7, and it broke an
invariant nobody re-checked: §7.1 exempts exactly two non-lending operations from the reserve inequality,
and bank wages are a third. That exemption is implemented in E7, but the payroll has to exist here.

Keep the loan record rich enough that E7 never needs to reconstruct anything. Risk weight depends on
current LTV, which depends on the collateral's current value — so the loan must know what it is secured
on, not merely how much is outstanding.

One share register, not two. The separate `bank_shares` field in an earlier draft left the town's most
concentrated asset outside every wealth statistic.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~BankStateTests
```

That bank shares are in the same register as firm shares and reach the net-worth calculation.
