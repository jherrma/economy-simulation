# **V2**: the determinism gate

**Epic:** E8 — Validation gates
**Depends on:** 07-01, 01-05
**New ground:** A runnable program that proves the comparison is meaningful

## Story

As a maintainer, I want determinism checked as a gate rather than a unit test, so that a regression in it is caught against real output rather than a toy.

## Acceptance criteria

- [ ] A command runs one scenario twice on one seed and compares output byte for byte, reporting the first differing file, row and column.
- [ ] A command runs the same seed serially and across threads and compares the same way. **Byte-identical, not close** — a floating-point difference means an accumulation is order-dependent and it will drift over 360 ticks.
- [ ] A command registers an unused random purpose and asserts output is unchanged.
- [ ] All three run in CI and in under a minute, on a short run (48 ticks, 4 seeds) rather than a full campaign.
- [ ] The comparison can itself fail: a test perturbs one parameter by one cent and asserts it is reported.

## Where to start

Keep the gate runs deliberately small. The temptation is to check determinism on a realistic run;
the value is in being able to run it after every change, and 48 ticks on four seeds catches
essentially everything 360 ticks would.

The unused-purpose check is the one that will matter in six months. Every mechanism the draft
backlog describes will want randomness, and this gate is what says adding it did not silently move
the model onto a different random world.

Proving the harness can fail is not ceremony. A comparison that passes because it is reading the
wrong directory is worse than none, since it will be trusted.

## How to verify

```sh
dotnet run --project tools/Gates -- determinism
```

Exit zero on all three checks, and the one-cent perturbation reported with a file and a column
rather than a bare non-zero exit code.
