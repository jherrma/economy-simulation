# The loan, and its schedule

**Epic:** E6 — Credit
**Depends on:** 01-03, 03-01
**New ground:** An obligation on future income, split into principal and interest

## Story

As the model author, I want a loan to be a principal, a term and a fixed instalment with a stated split, so that repayment reduces future spending and the money side can be got right.

## Acceptance criteria

- [ ] Simple interest: `interest_total = principal · loan_rate/100 · term/12`; `instalment = (principal + interest_total) / term`.
- [ ] The split per instalment is `principal/term` and `interest_total/term`, and the two sum to the instalment **to the cent** using 01-02's remainder-distributing split.
- [ ] A loan is a flat record in an array — principal, remaining term, instalment, principal part, interest part. Households do not own object graphs.
- [ ] `debt_service_h` is the **sum of instalments on live loans, computed** each tick, never stored.
- [ ] A loan with zero remaining term is retired and stops being counted.
- [ ] A test amortises a €900 loan over 24 months at 8% and asserts the principal parts sum exactly to €900 and the interest parts to €144.
- [ ] Accrual and payment are not both treated as interest — there is one interest figure per instalment and it is the one above.

## Where to start

Simple interest is a deliberate simplification and the reason belongs in the code comment as well as
the spec: the difference from an annuity is second-order for this question, and the arithmetic stays
checkable by hand, which matters a great deal while V1 is being brought up. Replace it with an
annuity when arrears exist and the principal/interest split starts to matter behaviourally.

The exact-sum requirement is not pedantry. The principal parts are what get destroyed on repayment;
if they sum to a cent more or less than the principal that was created, V1 fails at some point
between one and twenty-four months later, and the trail is cold.

Computing debt service rather than storing it removes a second source of truth that would otherwise
drift every time a loan retires.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LoanScheduleTests
```

The €900 amortisation summing exactly, and a retired loan disappearing from `debt_service`.
