# `Money`, in integer cents, with no way in from `double`

**Epic:** E1 — Foundations you cannot retrofit
**Depends on:** 01-01
**New ground:** The first domain value type, and the rule that money is never floating point

## Story

As the model author, I want every balance in the simulation to be integer cents in a type that a `double` cannot enter, so that §13.0's 'never floating point for balances' is enforced rather than remembered.

## Acceptance criteria

- [ ] `Money` wraps a 64-bit integer count of cents. There is no other representation.
- [ ] There is **no** implicit or explicit conversion from `double`, `float` or `decimal`. Constructing money from a euro figure requires a named factory that states its rounding.
- [ ] Addition and subtraction of two `Money` values compile; adding a `Rate`, a `double` or a bare number does not.
- [ ] Multiplication by a **dimensionless** factor (a share, an LTV, a rate already reduced to a fraction) returns `Money`, with the rounding rule named in the method and documented.
- [ ] **Pro-rata distribution sums back exactly.** Splitting an amount across n holders by arbitrary weights returns parts that sum to the original to the cent, with the remainder allocated by a stated, deterministic rule.
- [ ] Formatting for output is euros with two decimals; the internal representation is never formatted directly.
- [ ] A property test: for random amounts and random weight vectors, the parts always sum to the original.

## Where to start

The pro-rata requirement is the one that earns this story its place. §5.2.2 distributes profit
to shareholders pro rata by holding, and §5.1.3 does the same for share transfers; naive rounding
loses or invents cents on every distribution, and V1 will catch it — as a mysterious base-money
violation hundreds of ticks later rather than as an obviously wrong dividend. Decide the remainder
rule now (largest-remainder is the conventional choice, and it is deterministic, which V2 needs) and
test it directly.

Resist adding conveniences. Every implicit conversion you add is a hole in the guarantee: the reason
this type is worth having is precisely that it is *awkward* to get a `double` into it, because in
the specification's own history that is how errors arrived.

`Money` is a struct and lives in the §6.2 hot loop, so it must not allocate and must not carry
anything beyond the integer.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~MoneyTests
```

Particularly the pro-rata property test. If it passes only for round numbers, the weights being
generated are not adversarial enough.
