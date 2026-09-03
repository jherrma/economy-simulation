# The `money_creation` switch

**Epic:** E6 — Credit
**Depends on:** 06-03
**New ground:** The cheapest interesting experiment in the model

## Story

As the model author, I want loans fundable from the pool instead of created, so that the money-creation channel can be measured directly rather than inferred.

## Acceptance criteria

- [x] With `money_creation = false`, origination **transfers** the principal from the pool; repayment of principal returns it there.
- [x] The money stock is then constant: `Σ cash + pool == M0` at every tick, and the V1 form with `loans_outstanding` still holds because the loan is a claim rather than new money.
- [x] Everything else — θ, the residual test, the instalment, the interest dividend — is unchanged. Only the funding differs.
- [x] Origination **fails** if the pool cannot cover it, and the failure is recorded as a credit-rationing event rather than halting the run.
- [x] ~~A test runs `credit_high` at both settings on the same seeds and asserts the results differ.~~ **Corrected 2026-09-03:** they cannot differ (`01-SIMULATION.md` §7.3). The test asserts the paired runs are identical in every household-visible quantity and differ only in the pool and the money stock, one for one with the loans.
- [ ] (E7, 07-02) The difference between the two settings is written to output as its own series and labelled as the money-creation channel.

## Where to start

This switch is worth more than its size. `credit_high` against `credit_off` measures the whole
credit effect; `credit_high` against `credit_high` with creation off separates the part that comes
from *new money* from the part that comes from *reallocated timing*. Those are two different claims
about the world and the blog post will want to make them separately.

The rationing failure is deliberate: with a finite pool, credit becomes genuinely scarce, and who
gets it is decided by the random household order. That is the closest v1 comes to a lending
constraint, and it costs one branch.

Keep every other behaviour identical between the settings. If anything else differs, the difference
between the runs is no longer attributable to money creation, and the experiment is spent.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~MoneyCreationSwitchTests
```

`Σ cash + pool == M0` exactly with creation off, and the paired runs producing different prices.

## Finding (2026-09-03)

The paired runs are byte-identical to the household. Nothing on the household side of the walk or
of debt service depends on where the principal came from, income is fixed, and the pool has no
behaviour, so the whole difference is the pool's balance. The money-creation channel is zero by
construction in v1; the price effect of credit is entirely reach and timing. Recorded as §7.3, with
what would have to change for the channel to open. Rationing needs the pool below one principal at
the moment of the loan, because the principal drawn out is spent straight back in — with a default
pool it never happens.
