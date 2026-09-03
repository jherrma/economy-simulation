# Money creation, destruction, and the interest dividend

**Epic:** E6 — Credit
**Depends on:** 06-01, 03-02
**New ground:** The only two operations that change the money stock

## Story

As the model author, I want lending to create money and principal repayment to destroy it, with interest returned to households, so that the money-creation channel exists and V1 still holds to the cent.

## Acceptance criteria

- [x] Origination: `cash_h += principal` and `loans_outstanding += principal`. It is a **creation**, not a transfer from the pool.
- [x] Repayment: `cash_h −= instalment`; `loans_outstanding −= principal_part`, and that principal is **destroyed**.
- [x] The **interest part is a transfer, not a destruction**. It is pooled across all loans in the tick and paid out to all households pro rata by `income_h`, as bank profit returning as household income.
- [x] The dividend distribution uses 01-02's remainder-distributing split, so it sums to the interest collected to the cent.
- [x] Abstainers receive the dividend too — they hold bank shares like anyone else. A test asserts this, since it biases mildly **against** the hypothesis and must not be quietly removed.
- [x] V1 holds every tick through origination, repayment and distribution.
- [x] A test destroys the interest as well as the principal and asserts V1 **fails**, confirming the check catches it.

## Where to start

The interest treatment is the subtle one and it changes the answer rather than breaking the run.
Destroy interest along with principal and the money stock leaks downward at exactly the rate
households are borrowing — so the more credit there is, the more money is drained, and the price
effect being measured is damped by an artefact. Returning it as income closes the circuit, which is
what a closed economy requires: the bank's income is somebody's income.

That the dividend also reaches abstainers is not an oversight. It is the honest treatment, and it
makes the measured effect slightly smaller, which is the direction an honest model should err in.

The deliberate-failure test earns its place here rather than in 03-02, because this is the story
where the mistake is easiest to make.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~MoneyCreationTests
```

V1 green across a full credit cycle, and the deliberate interest-destruction variant halting.

## Implementation note (2026-09-03)

The interest is pooled in a third money holder, the bank's till (`Account.Bank`), which the check
requires to be empty at the end of every tick. Origination in these tests is done by hand through
the ledger and the loan book; the walk's own origination is 06-02.
