# Replacement cycles by archetype, and the two identities

**Epic:** E11 — Product groups and replacement cycles
**Depends on:** 11-03, 10-02
**New ground:** A per-household life, and two normalisations conserving two different things

## Story

As the model author, I want each archetype to replace each good on its own cycle, so that a type differs in how often it turns up at the shelf and not only in what it will pay when it does.

## Acceptance criteria

- [ ] `archetypes.<name>.d.<good>` multiplies that good's life. Absent means 1.0, the rule `w` and `kappa` follow (`02-PARAMETERS.md` §3.5). Requires `replacement = "hazard"` (11-03); under `deterministic` a `d` is rejected, never rounded.
- [ ] **`flow_cost` divides by the household's own life**, `price_(g,t) / life_h,g`, so the score multiplier is `ŵ · d` and the two tables compose.
- [ ] **`ŵ = m / d` is derived**, where `m` is what §3.5's table states. A test asserts the second identity `Σ_A share_A · m[A][g] = 1` on the authored table and asserts that it does **not** hold on the derived `ŵ` — the share-weighted mean of `ŵ` is not 1 and is not meant to be.
- [ ] A test asserts the composition on the worked case: `gadget`'s phone has `d' = 0.676`, `m = 1.553` and therefore `ŵ = 2.299`, and its realised score multiplier is 1.553 rather than the 1.050 that authoring `ŵ` directly would have produced.
- [ ] **The loader normalises harmonically**: `Σ_A share_A / d[A][g] = 1`, asserted on the normalised table for every good. A test builds a table whose *arithmetic* mean is 1 and asserts it is rejected or renormalised — because that table is the bug.
- [ ] A test asserts the consequence directly: over a long run with a varied `d`, realised replacement demand per tick for each good matches `capacity_g` within sampling error. This is what a wrong normalisation breaks and nothing else catches.
- [ ] `capacity = round(households / life_g)` is unchanged, and is **not** derived from the realised archetype assignment. A test asserts two seeds of one scenario produce identical effective configurations.
- [ ] No age is drawn: 11-03's hazard is memoryless and every household opens owning a working unit.
- [ ] `d` is meaningful only where `life > 1`; a `d` on a life-1 good fails to load rather than being silently ignored.
- [ ] `d ≡ 1` reproduces the same calibration run without `d`, byte for byte (V5b).
- [ ] The finite-sample residue is reported once in the opening state, per §3.7.

## Where to start

Two identities, and they conserve different things: `Σ share · m = 1` fixes the score level so §3.6's
base scores keep meaning what they say at the population mean, and `Σ share / d = 1` fixes the units
so capacity stays sized right. Neither implies the other and a table can satisfy one while breaking
the other.

Harmonic, not arithmetic, on the second one, and this is the older half of the story. Demand per tick is `1 / life`, so it is the reciprocal that has to average to one. `E[1/d] > 1/E[d]` for any `d` that varies at all — Jensen — so normalising `d` itself hands the population strictly more replacement demand than capacity was sized for. It is permanent, it is invisible, the price rule absorbs it into the level, and no invariant in this repository fires. Write the test that would catch it before writing the normalisation.

The trap on the first identity is subtler and it is why `ŵ` is derived rather than authored. Because `flow_cost` divides by the household's own life, a shorter cycle divides the score, so a type that replaces its phone 48% more often has already lost most of the 1.55 taste weight meant to make it the top bidder — and the type that keeps its phone longest comes out bidding highest, which is the opposite of the table's intent. Author the score multiplier, derive the weight, and the intent survives. The derived weight then looks alarming (2.299 for `gadget`'s phone) and is correct: a household that both churns and buys well values the good more than twice as much as average, because those are competing claims on one budget.

The second trap is the tempting correction for the sampling residue. Once you know the identity only holds in expectation, deriving `capacity` from the realised population looks like the rigorous fix. It makes the goods table a function of the seed, the effective configuration then differs between seeds of one scenario, and the campaign collector refuses to collect — correctly, because a seed that changes a parameter has become a parameter. Leave the residue: it is well under a per cent, and it is identical in both arms because the assignment is drawn per household and per seed rather than per scenario, so it cancels in the paired difference that is the finding.

> **Built 2026-09-06.** Two things the story left open, decided here.
>
> **The effective configuration records `m` and `d`, not `ŵ`.** §3.7 said otherwise and has been
> corrected. `ŵ` is the quotient of two columns that are both written down, so recording it as well
> would be seventy-two redundant numbers in a file the campaign manifest hashes, and a third place
> for the derivation to disagree with itself. §3.7's table of derived weights is guarded by a test
> that re-computes it — the answer 11-02 already gave for `base_score` and `v`.
>
> **`w` and `kappa` are keyed by category label, `d` by good.** The story does not say, and the two
> levels have to differ for the worked case to exist at all: `m = 1.553` is `gadget`'s *electronics*
> weight, while `d = 0.676` is its *phone* cycle, and there are three different cycles inside
> electronics. Under §3.1 a label is a row and the distinction is invisible, which is why every file
> written before E11 still means what it meant. The loader rejects a good's name in a `w` and a
> label in a `d`.
>
> **The claim that nothing else catches an arithmetic normalisation was checked, not assumed.** With
> the normalisation turned arithmetic, 587 of 593 tests still pass and all six failures are in
> `ReplacementCycleTests`.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ReplacementCycleTests
dotnet run --project tools/Gates -- archetypes
```

The harmonic identity holds per good; realised replacement demand matches capacity; two seeds agree on the effective configuration.
