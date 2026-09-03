# **V4**: the null run, and tier-mix stationarity

**Epic:** E8 — Validation gates
**Depends on:** 08-02, 05-02
**New ground:** Proving the baseline sits still before anything is compared to it

## Story

As the person who will defend these results, I want the creditless run proved stationary after warm-up, so that a credit effect measured later is not drift plus an unknown amount of signal.

## Acceptance criteria

- [ ] With `credit_enabled = false`, the CPI over ticks 121–360 has **no significant trend**, and each of the eighteen tier prices is flat to within a stated tolerance.
- [ ] **The realised tier mix is also stationary** over the same window. A mix still drifting at tick 120 means the measured window is contaminated, and the CPI can look flat while the mix underneath it moves.
- [ ] **The pool has stopped falling** by the end of warm-up. It drains during the transient by design; a pool still declining at tick 120 is the same failure seen from the money side and is the cheaper of the two to check.
- [ ] The gate reports the tick at which each of the three settled, so `warmup_ticks` can be raised on evidence rather than guessed at.
- [ ] Run across the full seed set, not one seed.
- [ ] A test asserts the gate fails on a configuration with `k` set high enough to oscillate.

## Where to start

Run this **before credit exists**. A drifting baseline makes every later number meaningless, and the
drift is enormously easier to locate in a model with no credit in it than in one where a plausible
alternative explanation is available.

The tier-mix criterion is the one that is easy to leave out and expensive to leave out. The opening
mix is deliberately not an equilibrium — premium starts 60–95% unsold — so the warm-up exists mainly
so relative tier prices can find one. If that has not finished by tick 120, the measured window
contains the tail of a transient that will be read as a credit effect.

Reporting settling ticks rather than a pass/fail turns `warmup_ticks` into an observation. If the
mix settles at tick 200, the parameter is wrong and the gate says so instead of quietly failing.

## How to verify

```sh
dotnet run --project tools/Gates -- nullrun
```

No trend in the CPI, a stationary mix, a flat pool, and the three settling ticks all comfortably
below `warmup_ticks`.
