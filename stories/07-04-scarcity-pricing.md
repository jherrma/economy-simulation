# The price of credit under scarcity

**Epic:** E7 — Credit
**Depends on:** 07-03
**New ground:** Credit becoming dearer as the limit approaches, with a calibration gate

## Story

As the model author, I want the loan rate to rise as headroom falls, at a calibration that leaves a functioning credit market, so that rationing happens by price as well as by quantity, without the control run failing for unrelated reasons.

## Acceptance criteria

- [ ] `r_l = r_base + risk_premium(borrower) + k_scarcity · (1/max(h, h_floor) − 1)`. **No `spread` term** — it was folded into `r_base`.
- [ ] `r_d = r_d_base + k_deposit · (1 − h)`.
- [ ] `scarcity_form` selects `reciprocal` or `linear`, and is swept — the curve is an assumption and not a small one.
- [ ] `k_scarcity` defaults to **0.25**, not 4.0: at 4.0 the rate exceeds 75% at the headroom floor and `high_credit_gold` spends its life at the cap.
- [ ] `k_scarcity = 0` recovers a fixed-rate bank as the control, isolating quantity rationing from price rationing.
- [ ] **The realised distribution of `r_l` during warm-up is a gate**: a run whose median loan rate leaves a plausible band is rejected before its results are looked at.
- [ ] The scarcity premium is reported as its own series, separate from the base rate.

## Where to start

The shape of `1/h` is violent and the default was set before anyone plotted it. Nothing in this model
restrains how much the bank *wants* to lend — it grants any application passing the standards — so `h`
sits near zero whenever demand is ample, and at `reserve_ratio = 1.0` it collapses as soon as the bank
lends out its slack. The decisive control run would then fail for a reason having nothing to do with the
thesis.

The warm-up gate is the part that makes the calibration honest. It rejects an implausible run *before*
its output is examined, rather than after someone has formed a view about what the output shows. That
ordering matters more than the specific band.

`h_floor` silently sets how sharp a crunch can get, so report it alongside any crunch result. It is ⚠ for
good reason.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ScarcityPricingTests
```

The realised `r_l` distribution in warm-up across the reserve sweep. If the gold run sits at the cap,
the calibration is still wrong regardless of what the tests say.
