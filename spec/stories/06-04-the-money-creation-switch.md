# The `money_creation` switch

**Epic:** E6 — Credit
**Depends on:** 06-03
**New ground:** The cheapest interesting experiment in the model

## Story

As the model author, I want loans fundable from the pool instead of created, so that the money-creation channel can be measured directly rather than inferred.

## Acceptance criteria

- [ ] With `money_creation = false`, origination **transfers** the principal from the pool; repayment of principal returns it there.
- [ ] The money stock is then constant: `Σ cash + pool == M0` at every tick, and the V1 form with `loans_outstanding` still holds because the loan is a claim rather than new money.
- [ ] Everything else — θ, the residual test, the instalment, the interest dividend — is unchanged. Only the funding differs.
- [ ] Origination **fails** if the pool cannot cover it, and the failure is recorded as a credit-rationing event rather than halting the run.
- [ ] A test runs `credit_high` at both settings on the same seeds and asserts the results differ.
- [ ] The difference between the two settings is written to output as its own series and labelled as the money-creation channel.

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
