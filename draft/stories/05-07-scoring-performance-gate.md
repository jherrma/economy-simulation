# The performance gate — **V6**

**Epic:** E5 — The purchase decision
**Milestone:** M1
**Depends on:** 05-06
**New ground:** A measured budget for the loop that is 95% of the tick

## Story

As a maintainer, I want the scoring loop measured against the benchmark and held to a budget, so that an accidental allocation or a complexity regression is caught by a number rather than by a slow campaign.

## Acceptance criteria

- [ ] A benchmark harness runs the real scoring loop, not a synthetic copy, on a fixed seed and configuration.
- [ ] The measured throughput is compared against the C# figure in `LANGUAGE-CHOICE.md` — about 0.86 s for 800 households × 100 ticks × 120 candidates — with a stated tolerance.
- [ ] **Zero allocation** in the loop is asserted, not assumed: the harness fails if any managed allocation occurs per candidate.
- [ ] The realised candidate count per household-tick is reported, since the 120 figure is an estimate the specification does not state and everything scales with it.
- [ ] A regression beyond tolerance fails the build.
- [ ] The gate can be run alone and is excluded from the ordinary unit-test run, being far slower.

## Where to start

The allocation assertion is the more valuable half. A performance number drifts for many reasons on a
laptop, but an allocation in the hot loop is a binary fact and it is the specific way the C# choice can be
squandered — `LANGUAGE-CHOICE.md` §7 committed to no allocation, and the benchmark figures assume it. Use
the runtime's allocated-bytes counter around the loop rather than eyeballing a profiler.

Report the realised candidate count every time. It is the largest uncertainty in the whole extrapolation:
at 30 candidates the campaign is four times faster than predicted, at 300 it is two and a half times
slower, and nobody will notice which until this number is printed.

Do not tune anything yet. The rule in the backlog README is that optimisation is triggered by this gate,
not by a hunch — and a first implementation that passes needs no attention.

## How to verify

```sh
dotnet run -c Release --project bench/EngineBench
```

The allocation count at zero, the realised candidate count, and the throughput within tolerance of
the figure in `LANGUAGE-CHOICE.md`.
