# Deterministic RNG, seeded per stream

**Epic:** E1 — Foundations
**Depends on:** 01-01
**New ground:** **V2.** Reproducibility that survives parallelism, reordering and future mechanisms

## Story

As the person who will defend these results, I want every random draw to come from a stream seeded by what it is for, so that a run is bit-identical at any thread count and two versions of the model can be compared on the same seed.

## Acceptance criteria

- [ ] A stream is derived as `hash(run_seed, household_id, purpose)`, where `purpose` is a **string constant in one registry**. There is no single shared generator anywhere.
- [ ] The registry holds the v1 purposes: `income`, `willingness`, `theta`, `abstainer`, `initial_age`, `finance`, plus the per-tick `order` stream keyed by tick.
- [ ] No stream is derived from an index, a counter, or a slice of another stream.
- [ ] Two runs with the same seed produce byte-identical output files.
- [ ] A run executed serially and across threads produces byte-identical output files.
- [ ] **A test registers a new purpose, draws nothing from it, and asserts the output is byte-identical.**
- [ ] The generator is pinned explicitly in the code and documented — not the framework default.

## Where to start

The unused-stream test is the one that pays for itself, and it is worth being clear about what it
buys. Every mechanism added after v1 — a supply response, wages, default, a second-hand market —
will want randomness. If adding it shifts the draws the current model makes, then the new version
and the old are running on different random worlds, and the difference between their results is
noise plus mechanism with no way to separate the two. Named streams make that impossible by
construction; index-based or slice-based streams make it inevitable.

Derive by hashing the tuple into the seed. Do not advance one generator and hand out slices — slices
reintroduce exactly the ordering dependence you are removing.

Note the one place the model legitimately wants order dependence: the per-tick shopping order is a
seeded shuffle, because under rationing the order decides who gets the last unit. That is a shuffle
driven by its own stream, which is a different thing from letting processing order leak into every
draw in the model.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DeterminismTests
```

The serial-versus-parallel comparison, byte-identical rather than close, and the unused-stream test.
