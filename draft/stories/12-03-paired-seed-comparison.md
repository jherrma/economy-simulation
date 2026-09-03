# Paired-seed comparison

**Epic:** E12 — Scenarios and the campaign
**Milestone:** M2
**Depends on:** 12-02
**New ground:** Comparing scenarios seed by seed rather than in aggregate

## Story

As the person who will defend these results, I want scenarios compared paired, seed by seed, rather than as pooled distributions, so that differences are not swamped by initialisation noise the pairing removes.

## Acceptance criteria

- [ ] The **same 30 seeds** are used across every scenario and comparisons are made **paired**, seed by seed (§13.1).
- [ ] A seed missing from either side excludes that pair rather than being compared against a different seed.
- [ ] Paired differences are reported with their dispersion, so a difference smaller than seed variation is visibly so.
- [ ] Firm draws are seeded identically across scenarios — comparing scenarios with different firm populations would confound the comparison with pure initialisation noise (§5.2.1).
- [ ] A test constructs two scenarios differing in nothing and asserts the paired difference is exactly zero for every seed.

## Where to start

The zero-difference test in the last criterion is the strongest single check in this epic. Two identical
scenarios must differ by nothing at all — not 'by very little'. Any nonzero difference means something in
the run depends on state that is not seeded, and every result the campaign produces would carry that noise.

Pairing exists because between-seed variation in an agent model is large relative to the treatment effect.
Pooling the two distributions and comparing means would need far more seeds to see the same signal, and 30
would not be enough.

Excluding incomplete pairs rather than substituting is the conservative choice and worth stating: a
halted run in one scenario must not silently be compared against a completed run in another.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~PairedComparisonTests
```

Exactly zero difference between two identical scenarios across all 30 seeds. Anything else is an
unseeded dependency and must be found before the campaign is trusted.
