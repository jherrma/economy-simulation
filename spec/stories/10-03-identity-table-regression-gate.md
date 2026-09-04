# **V5a**: the identity table reproduces v1 byte for byte

**Epic:** E10 — The population has types
**Depends on:** 10-02, 08-04
**New ground:** A mechanism switched off by data rather than by a flag

## Story

As the model author, I want the identity archetype table to reproduce a pre-archetype run byte for byte, so that the difference a typed population makes is attributable to the table and to nothing else.

## Acceptance criteria

- [ ] A gate `archetypes` runs the current engine under the identity table and compares its output to the recorded pre-E10 baseline for the same seeds. **Byte-identical, or the gate fails naming the first differing line.**
- [ ] The comparison is over a **stated projection**, not the raw files: `run.csv` and `tiers.csv` whole, and the cohort file with the columns E10 adds (10-04's archetype column) dropped. Without this the gate is permanently red the moment 10-04 lands, because a new column changes every line under the identity table too — and the same applies again to 11-01's per-good columns. State the projection in the gate, never special-case the writer to emit the v1 shape when there is one archetype: that hides exactly the bug the gate is for.
- [ ] The baseline is the `credit_off` and `credit_high` output at the commit E10 branched from, committed as a fixture with that commit recorded beside it.
- [ ] The unused-stream test of 01-05 is extended to `"archetype"` and `"taste_idio"`: registering them and not consuming them changes no other stream's output.
- [ ] The gate is in the CI run alongside determinism, neutrality, nullrun and creditoff.
- [ ] A test asserts the identity table is what a configuration naming no archetype receives — the gate is worthless if the default has drifted into something else.
- [ ] `03-VERIFICATION.md` V5a is the specification for this story; a divergence is a spec correction first, never a fixture regenerated to match.

## Where to start

The last criterion is the one that will be under pressure. When this gate goes red the cheap move is to regenerate the fixture, and it is available at any moment, and it destroys the only evidence that the mechanism is neutral when off. A red V5a means one of two things: the identity table is not the identity, or the archetype code perturbs a run it should not touch. Both are findings. Neither is fixed by taking a new photograph of the wrong thing.

Recording the baseline commit next to the fixture is what makes the failure diagnosable a year from now. Without it the fixture is a wall of numbers that once matched something.

Note what this gate cannot do, so nobody expects it to: it says the identity table is neutral, not that the `typed` table is correct. Nothing in this repository can say the second — the table is a hypothesis about a population and is defended by sweeping it (`02-PARAMETERS.md` §3.5), not by a check.

## How to verify

```sh
dotnet run --project tools/Gates -- archetypes
```

`PASS`, naming both arms and the projection it compared over.

**Corrected while building, 2026-09-04.** This story originally asked for "a deliberate edit to any
`w` in the identity table makes it fail on the first differing line". That edit cannot be made: with
a single type at share 1.0 the column scale is that type's own weight, so the loader normalises any
one-type table straight back to the identity (`02-PARAMETERS.md` §3.5). The property is stronger
than the check that was asked for, and the gate is exercised instead by a table with two types who
want different things — which fails both the identity check and the parameter comparison, naming
`archetypes.` as what moved.
