# `Rate`, whose only exit applies the correct divisor

**Epic:** E1 — Foundations
**Depends on:** 01-01
**New ground:** Making the per-annum / per-tick confusion unrepresentable

## Story

As the model author, I want interest rates in a type that carries its own units, so that a rate quoted per annum in per cent can never be applied to a month by accident.

## Acceptance criteria

- [ ] `readonly record struct Rate(double PercentPerAnnum)`, with the only accessors being named conversions.
- [ ] The **finance multiplier** for a term is `1 + PercentPerAnnum · term / 1200`, exposed as one method, and it is the only place that constant appears.
- [ ] **No literal `12`, `100` or `1200` appears at any call site** in the engine. A test greps the source for those literals outside this type and fails if it finds one in an interest context.
- [ ] A test pins the arithmetic: 8%/a over 24 months is a multiplier of exactly 1.16; over 12 months, 1.08.
- [ ] `Rate` has no arithmetic operators with `Money`. Applying a rate to money is a named method that returns `Money` through 01-02's rounding.

## Where to start

The specification quotes `loan_rate` as **8.0 per cent per annum**, and the model needs it as a
multiplier over a term in months. Written out at a call site that is `rate / 100 * term / 12`, and
the failure mode of getting it wrong is not a crash: it is a plausible number. In the draft model an
earlier note had `r_annual / 12` where the column was already in per cent, which charges 25 per cent
*per month* and produces a credit effect that looks spectacular and is arithmetic.

One exit, one test pinning two known values, and the literals banned everywhere else. That is the
whole story, and it takes an hour.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~RateTests
```

The 1.16 and 1.08 assertions, and the source scan finding no bare divisors.
