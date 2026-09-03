# `Money`, in integer cents, with no way in from `double`

**Epic:** E1 — Foundations
**Depends on:** 01-01
**New ground:** The type every balance in the model is expressed in

## Story

As the model author, I want money represented as integer cents in a type that cannot be constructed from a floating-point number, so that the conservation invariant can be exact rather than approximate.

## Acceptance criteria

- [ ] `readonly record struct Money(long Cents)`, with addition, subtraction, negation, comparison and multiplication by an integer.
- [ ] **No implicit or explicit conversion from `double` or `decimal`.** Constructing money from a computed fraction must be a deliberate, named, rounded operation.
- [ ] Splitting a `Money` into `n` parts distributes the remainder cent by cent, so the parts sum exactly to the original. A test asserts this for awkward values such as `€1.00 / 3`.
- [ ] Multiplication by a ratio (a price multiplier, an interest rate) goes through one named rounding method, and every call site in the engine uses it.
- [ ] Formatting for CSV is invariant-culture and fixed to two decimals.
- [ ] Overflow throws rather than wrapping.

## Where to start

The reason for integer cents is V1. `Σ cash + pool == M0 + loans` has to hold *to the cent*, and a
floating-point representation makes that assertion either false or fuzzy — and a fuzzy conservation
check is worth almost nothing, because the bugs it exists to catch are often small.

The remainder-distributing split is what makes tier increments exact. A household that takes budget
and then upgrades pays `0.60·P` and `0.40·P`, and those two must sum to `P` with no residual cent
appearing or vanishing. Get this wrong and V1 fails intermittently, on prices that happen to be odd.

Resist adding a currency field or a decimal type. There is one currency and there is no rounding
policy to choose beyond the one named method.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~MoneyTests
```

The split-remainder test across a range of awkward values, and the compile-fail case from 01-01
showing that `Money m = 1234.56;` does not build.
