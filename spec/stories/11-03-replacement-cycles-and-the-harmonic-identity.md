# Replacement cycles by archetype, and the harmonic identity

**Epic:** E11 — Product groups and replacement cycles
**Depends on:** 11-02, 10-02
**New ground:** A per-household life, and the one normalisation that keeps capacity honest

## Story

As the model author, I want each archetype to replace each good on its own cycle, so that a type differs in how often it turns up at the shelf and not only in what it will pay when it does.

## Acceptance criteria

- [ ] `archetypes.<name>.d.<good>` multiplies that good's life. Absent means 1.0, the rule `w` and `kappa` follow (`02-PARAMETERS.md` §3.5).
- [ ] **The loader normalises harmonically**: `Σ_A share_A / d[A][g] = 1`, asserted on the normalised table for every good. A test builds a table whose *arithmetic* mean is 1 and asserts it is rejected or renormalised — because that table is the bug.
- [ ] A test asserts the consequence directly: over a long run with a varied `d`, realised replacement demand per tick for each good matches `capacity_g` within sampling error. This is what a wrong normalisation breaks and nothing else catches.
- [ ] `capacity = round(households / life_g)` is unchanged, and is **not** derived from the realised archetype assignment. A test asserts two seeds of one scenario produce identical effective configurations.
- [ ] `age_h,g` is drawn uniform over `{1 … life_h,g}` — the household's own life, not the good's (02-04's ageing criterion, inherited).
- [ ] `d` is meaningful only where `life > 1`; a `d` on a life-1 good fails to load rather than being silently ignored.
- [ ] `d ≡ 1` reproduces the same calibration run without `d`, byte for byte (V5b).
- [ ] The finite-sample residue is reported once in the opening state, per §3.7.

## Where to start

Harmonic, not arithmetic, and this is the whole story. Demand per tick is `1 / life`, so it is the reciprocal that has to average to one. `E[1/d] > 1/E[d]` for any `d` that varies at all — Jensen — so normalising `d` itself hands the population strictly more replacement demand than capacity was sized for. It is permanent, it is invisible, the price rule absorbs it into the level, and no invariant in this repository fires. Write the test that would catch it before writing the normalisation.

The second trap is the tempting correction for it. Once you know the identity only holds in expectation, deriving `capacity` from the realised population looks like the rigorous fix. It makes the goods table a function of the seed, the effective configuration then differs between seeds of one scenario, and the campaign collector refuses to collect — correctly, because a seed that changes a parameter has become a parameter. Leave the residue: it is well under a per cent, and it is identical in both arms because the assignment is drawn per household and per seed rather than per scenario, so it cancels in the paired difference that is the finding.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ReplacementCycleTests
dotnet run --project tools/Gates -- archetypes
```

The harmonic identity holds per good; realised replacement demand matches capacity; two seeds agree on the effective configuration.
