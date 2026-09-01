# Deterministic RNG, seeded per stream

**Epic:** E1 — Foundations you cannot retrofit
**Milestone:** M0
**Depends on:** 01-01
**New ground:** **V2.** Reproducibility that survives parallelism and reordering

## Story

As the person who will defend these results, I want every random draw to come from a stream seeded by what it is for, not from one shared generator, so that a run is bit-identical at any thread count and the paired-seed protocol of §13.1 means something.

## Acceptance criteria

- [ ] A stream is derived from `(run_seed, agent_id, purpose)`. There is **no** single shared generator anywhere in the engine.
- [ ] Two runs with the same seed produce byte-identical output files.
- [ ] A run executed with agents processed in a different order produces the same draws **for each agent**, because an agent's stream does not depend on when it was reached.
- [ ] Adding a new consumer of randomness in one part of the model does not shift the draws in any other part.
- [ ] Stream purposes are **string constants in one registry**. No stream is derived from an index, a counter, or a slice of another stream — those reintroduce the ordering dependence this story removes (S2).
- [ ] A test **registers a new purpose, never draws from it, and asserts the run is byte-identical**. This is the property that makes every later milestone comparable to every earlier one on the same seeds.
- [ ] Per-agent attributes are drawn at initialisation from named streams **even when nothing reads them yet** — θ is unused until M2 and φ until M3, and drawing them late would shift the opening state (03-03).
- [ ] The generator is explicit and fixed in the code, not the framework default, and is documented — a runtime that changes its PRNG between versions would silently break every stored result.
- [ ] A test runs the same seed twice, once serially and once across threads, and compares output byte for byte.

## Where to start

This is borrowed wholesale from the raytracer's per-pixel seeding, and for the same reason: the
moment draws come from one shared generator, the *order* in which agents are visited becomes part of
the answer. That makes parallelism unsafe, makes adding a story silently change unrelated results, and
makes 'the same 30 seeds compared paired' a claim you cannot honour.

Derive a stream by hashing the tuple into the generator's seed rather than by advancing one generator
and handing out slices. Slices reintroduce exactly the ordering dependence you are trying to remove.

Note the one place the model legitimately wants order-dependence: §6.1's credit queue is processed in
a per-tick seeded **random** order because under a binding constraint the order decides who gets
credit. That is a shuffle driven by its own stream, which is a different thing from letting the
processing order leak into every draw in the model.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DeterminismTests
```

The serial-versus-parallel comparison. It must be byte-identical, not 'close' — a floating-point
difference here means an accumulation is order-dependent and will drift over 480 ticks.
