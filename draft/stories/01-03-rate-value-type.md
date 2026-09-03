# `Rate`, whose only exit applies the correct divisor

**Epic:** E1 — Foundations you cannot retrofit
**Milestone:** M0
**Depends on:** 01-01
**New ground:** Encoding a *convention* in a type, so it cannot be misapplied at a call site

## Story

As the model author, I want interest rates to be a type whose only accessor converts per cent per annum to a per-tick fraction, so that the §13.0 convention cannot be applied inconsistently between the loan schedule, the deposit credit and the DSTI test.

## Acceptance criteria

- [ ] `Rate` carries an annual nominal rate **in per cent** — `3.0` means 3% p.a. — matching every default in §13.7.
- [ ] The only way to a usable number is a per-tick accessor that divides by **1200**, not 12.
- [ ] The underlying per-cent figure is not publicly readable as a bare number that could be used in arithmetic by mistake; only formatting and the per-tick accessor are exposed.
- [ ] Adding two rates (base + risk premium + scarcity premium, per §7.3) compiles and stays in per cent.
- [ ] A test pins `Rate(3.0).PerTick` to exactly `0.0025`.
- [ ] A test pins a 300-month annuity on €144,000 at 3.0% to €682.86 ± 1 cent, which is the §13.11 worked example and fails loudly if the divisor is 12.

## Where to start

This type exists because of a specific near-miss: §7.3 said rates were annual and §13.7 quoted
them in per cent, so read literally the code would have charged 25% a month. That is not a typo class
of error — it produces a run that completes, reports numbers, and is wrong.

The design rule is that the *convention* has exactly one implementation and no call site restates it.
If you find yourself writing `/ 1200` anywhere outside this type, the type has failed.

The annuity check in the criteria is worth more than the unit test above it, because it exercises the
divisor through a realistic formula rather than in isolation, and because it is a number a human can
sanity-check against a mortgage calculator.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~RateTests
```

The annuity figure. €682.86 is right; roughly €5,600 means the divisor is 12; roughly €360 means
someone has halved something.
