# Reporting by type, and re-measuring the power

**Epic:** E10 — The population has types
**Depends on:** 10-03, 07-03, 09-02
**New ground:** The cohort cut becomes two-dimensional, and the effect is re-resolved against noise

## Story

As the model author, I want cohort metrics cut by archetype as well as by abstainer status, and the power probe re-run under the typed table, so that I learn both who the harm falls on and whether thirty seeds can still see it.

## Acceptance criteria

- [ ] 07-03's cohort metrics gain an archetype dimension: the same columns, per (abstainer, archetype) cell, with the archetype name on every row.
- [ ] Category shares are reported **within category**, never pooled across categories — `01-SIMULATION.md` §10.4, and the archetype cut makes the trap worse rather than better, because the types differ in exactly the group weights that the pooled figure averages over.
- [ ] The `typed` scenarios are added to `config/scenarios/`: at minimum `typed_credit_off` and `typed_credit_high`, each carrying the **same** table, so the pair is comparable.
- [ ] `tools/Gates pilot` runs under the typed table and reports each measure's paired difference against what 30 seeds resolve, exactly as it does untyped.
- [ ] The result of that probe is written into `01-SIMULATION.md` §10 as a dated finding, **whatever it says**. A typed table under which the headline stops resolving is a result about the population and is reported as one.
- [ ] `Notes.md` gets a dated block for what the typed run showed, per its own convention.

## Where to start

The probe is the point of this story, not the reporting. §10.4's margin — the abstainer share obtained resolving at roughly ten times its own floor — was measured on a population with one taste multiplier. Archetypes add dispersion in exactly the place that decides who is marginal in the financeable categories, and pairing does not rescue that: the same household being the same type in both arms cancels the *draw*, not the trajectory the draw sets off. So the honest order is to measure the power first and read the finding second, because a difference read off an underpowered comparison looks exactly like a difference read off a powered one.

On the two-dimensional cut, resist widening it further. Four types times two abstainer states is eight cells and each cell is a quarter of the households it used to be; the per-cell noise grows accordingly. If a cell gets thin the answer is more seeds, not a finer cut.

The within-category rule earns restating because this story is where it will be broken. Pooling a share across categories reverses its sign when exclusion moves units out of the denominator, and the types were *designed* to differ in their category weights — so a pooled figure cut by archetype averages two things that this epic deliberately made different.

## How to verify

```sh
dotnet run --project tools/Gates -- pilot
dotnet run --project tools/Campaign -- --all
```

Every measure's paired difference sits beside its detectable floor under the typed table; the dataset carries an archetype column on every cohort row.
