# Additive configuration: new parameters default to the old behaviour

**Epic:** E3 — Configuration and opening state
**Milestone:** M0
**Depends on:** 03-02
**New ground:** Forward compatibility as a rule the loader enforces, not a convention people remember

## Story

As the person who will compare M2's result against M7's, I want a scenario file written at any milestone to load unchanged at every later one and reproduce its original behaviour, so that old results stay reproducible without hand-reconstructing an old configuration.

## Acceptance criteria

- [ ] An **absent** key is filled from its default and the run proceeds. An **unknown** key is an error — a parameter not in §13 does not exist (03-02).
- [ ] Every parameter that gates a dimension has a default that **disables** that dimension: `credit_enabled = false`, `sigma = 0`, `status_relative = false`, `lambda_gap = 0`, `default_enabled = false`, and so on for each milestone's switch.
- [ ] The committed §13-defaults fixture therefore describes **M1**, not the full model. A test asserts that loading it with the M8 engine yields M1 behaviour.
- [ ] Each parameter carries the milestone at which it was introduced, and the schema can print the parameter set for a given milestone.
- [ ] The effective configuration written beside the output records every resolved value, including defaults that were never named in the input file (03-02).
- [ ] A test loads a committed M1-era scenario file with the current schema and asserts the run is byte-identical to the stored M1 baseline (01-06).
- [ ] Removing or renaming a parameter is a schema change with an explicit alias, not a silent break. A test asserts an aliased old name still loads.

## Where to start

This is seam S4 in `../docs/FOUNDATION.md`, and it exists to make V7 usable rather than merely
possible. Reproducing M2 while sitting on the M7 engine must not require reconstructing an M2-era
config by hand — that is the kind of task that gets skipped, after which the milestone comparisons
quietly stop being run.

The asymmetry between absent and unknown keys is the whole design and it is worth being precise
about. Absent means "an older file, or a caller who does not care" and is filled from defaults.
Unknown means "a typo, or a parameter someone invented" and must fail, because a misspelled key
that is silently ignored produces a run at default settings that looks exactly like the run that
was asked for.

The consequence for §13 is that the defaults column stops describing "the model" and starts
describing **the model with every dimension off**. That reads oddly at first, and it is the correct
choice: it means the file that has been in the repository longest is also the one that always
still works.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ConfigCompatibilityTests
```

The M1-era fixture loading under the current schema and matching the M1 baseline byte for byte.
That test is the one that will actually catch a regression here, because it fails on the day
someone adds a parameter with an enabling default.
