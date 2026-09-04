# The archetype table, and the taste identity

**Epic:** E10 — The population has types
**Depends on:** 02-02, 02-03
**New ground:** A table of household types in the configuration, normalised at load

## Story

As the model author, I want a population of named archetypes with a per-category taste weight and quality steepness, so that a household can want *different things* rather than merely more or less of everything.

## Acceptance criteria

- [ ] `[archetypes.<name>]` sections load with `share`, `w` and `kappa` (`02-PARAMETERS.md` §3.5). **An absent category means 1.0; an unknown category name is an error** — the 02-02 asymmetry, inherited rather than re-implemented.
- [ ] The **default is the identity table**: one type, share 1.0, every `w` and every `kappa` at 1.0. A configuration that names no archetype gets it, and there is no `archetypes_enabled` boolean anywhere.
- [ ] Shares must sum to 1.0 or loading fails, naming the sum it got.
- [ ] **The loader normalises each `w` column** by its share-weighted mean, so `Σ_A share_A · ŵ_g,A = 1` holds by construction. A test asserts the identity on the `typed` table of §3.5 and on a random table.
- [ ] §3.5 calls that number the **score multiplier `m`**, because under E11 the taste weight `ŵ = m / d` is derived from it. **In E10 `d ≡ 1`, so `m` and `ŵ` are the same number** and this story implements the plain reading. Nothing here anticipates E11; the name is there so the two epics do not disagree later.
- [ ] The **normalised** weights are what `ToToml()` and the effective configuration emit, so the campaign manifest hashes what actually ran (09-02) rather than what was written.
- [ ] `kappa` is **not** normalised, and a test asserts that a table with mean `kappa` ≠ 1 loads unchanged.
- [ ] `kappa` is rejected outside `0 < kappa < kappa_max`, where `kappa_max` is **computed from the tier table** and not written as a literal. A test changes a tier multiplier and asserts the accepted range moves with it.
- [ ] `sigma_idio` defaults to 0.0.
- [ ] A `config/scenarios/` file can carry a table, and one is committed for the `typed` population.

## Where to start

The normalisation is the story. Writing a table whose columns already average to one is arithmetic busywork that hides what the author meant, so the table is authored as *relative* weights and the loader does the division. That decision has a consequence worth being deliberate about: what the author wrote and what the model ran are then different numbers, and the effective configuration must show the second. This is the same reasoning that makes `capacity` derived in 02-03 — derive what is derivable, and record the derived value where a reader will find it.

Absent-means-1.0 is what keeps the table readable. A type that is average in leisure should not have to say so, and the file then reads as a statement of what makes the type different rather than as a dense grid. It costs nothing because 02-02 already distinguishes an absent key from an unknown one.

The `kappa` bound is where this story can go quietly wrong. The bound exists because `01-SIMULATION.md` §5.4's candidate ladder inverts when `value_mult_budget^kappa` falls below `price_mult_budget`, and at the v1 tiers that is 1.3245. Writing 1.3245 into the code makes the guard correct today and silently wrong the first time somebody edits the tier table — which is exactly the class of error V6's monotonicity assertion exists to catch, arriving from the one direction V6 cannot see, namely configuration.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ArchetypeTests
```

The identity holds on both tables; a misspelt category fails to load while an omitted one defaults; the accepted `kappa` range follows a changed tier multiplier.
