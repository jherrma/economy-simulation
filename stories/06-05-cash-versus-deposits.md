# The cash-versus-deposit choice, κ

**Epic:** E6 — Status, stress, saving
**Depends on:** 06-04, 05-06
**New ground:** The self-correcting loop that makes §7 a slope rather than a wall

## Story

As the model author, I want households choosing the form of their buffer, with `κ` responding to the deposit rate, so that the bank has its only lever over its own reserves, and lending capacity self-corrects.

## Acceptance criteria

- [ ] `κ` is the share of financial assets held as **cash**, `C/(C+D)`, and it falls as `r_d` rises.
- [ ] `κ` rises with distrust after bank losses.
- [ ] The loop is demonstrable: lending grows → headroom falls → `r_d` rises → `κ` falls → reserves rise → headroom recovers.
- [ ] Rebalancing happens at step 14 of the tick, after all spending has settled.
- [ ] **At `reserve_ratio = 1.0` the loop gains exactly nothing** — converting cash to a deposit raises reserves and deposits by the same amount, so headroom gains `X · (1 − reserve_ratio) = 0`. A test asserts this, because it is why time deposits exist (§7.1.1).
- [ ] `c̄ = C/D` is reported for §9 — **not** `κ`, which is a different quantity with a different formula.

## Where to start

The degeneracy at full reserve is the most important thing in this story and it is entirely
counter-intuitive: the loop the specification presents as making §7 self-correcting is *exactly
inoperative* at the setting §8 calls decisive. Writing the test that proves it is what motivates the next
story, and it is the kind of property that gets quietly assumed to work everywhere.

Mind the two ratios. `κ = C/(C+D)` is the household parameter; `c̄ = C/D` is what the money multiplier
needs. Substituting an average of household `κ` into the multiplier formula gives the wrong answer at
every setting except `reserve_ratio = 1.0`. Convert explicitly and report the right one.

Step 14 placement is deliberate: the bank discovers its reserve position only once the tick's
transactions have settled, which is why rate-setting at step 1 uses the previous tick's headroom.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CashPreferenceTests
```

The zero-gain assertion at `reserve_ratio = 1.0`, and the self-correcting loop visibly closing at
0.20.
