# The loan schedule, and not double-counting interest

**Epic:** E7 — Credit
**Milestone:** M2
**Depends on:** 01-03, 02-02, 04-04
**New ground:** Amortisation, and the separation of payment from accrual

## Story

As the model author, I want loans that amortise on an annuity schedule, with accrual and payment as distinct operations, so that interest is charged once rather than twice, which is the standard way this goes wrong.

## Acceptance criteria

- [ ] A loan carries principal, rate, term, remaining term, collateral and borrower; the instalment is the monthly annuity at origination.
- [ ] Payment happens at step 8; **accrual happens at step 15, on balances *after* step 8's payments** (§6.1).
- [ ] A test asserts that payment and accrual together charge exactly one month's interest, not two.
- [ ] Repayment destroys the deposit it came from and releases the reserve requirement (§7.1).
- [ ] The §13.11 worked example is pinned: €144,000 over 300 months at 3.0% gives an instalment of €682.86.
- [ ] A loan run to term arrives at exactly zero principal — no residual cent.
- [ ] Amortisation is computed in `Money`, with rounding that cannot accumulate drift over 300 payments.

## Where to start

The double-counting error is worth guarding explicitly because it produces a run that completes and
reports plausible numbers, with debt burdens roughly twice what the parameters imply. §6.1 states the
ordering precisely for that reason: they are separate operations on the same loan and must not both be
treated as interest.

The residual-cent criterion is where 01-02's rounding rule gets a hard test. An annuity computed in
floating point and rounded per payment will leave a few cents outstanding after 300 months, and those
cents will show up as a V1 violation rather than as an obviously wrong loan. Decide where the rounding
difference goes — conventionally into the final payment — and test the full schedule.

Keep the collateral reference on the loan rather than looking it up. E7's risk weights and E8's
repossession both need to know what a loan is secured on.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LoanScheduleTests
```

The €682.86 figure, the single-charge assertion, and a 300-month schedule terminating at exactly
zero.
