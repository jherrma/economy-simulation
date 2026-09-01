# Loading and validating a configuration

**Epic:** E3 — Configuration and opening state
**Milestone:** M0
**Depends on:** 03-01, 01-04
**New ground:** The first real use of Result at a boundary, with all errors reported at once

## Story

As a maintainer, I want a configuration file loaded into the schema, with every problem reported in one pass, so that a bad config costs one edit rather than a dozen run-fix-rerun cycles.

## Acceptance criteria

- [ ] TOML in, schema out, as a `Result`. Malformed input never throws.
- [ ] **All** validation failures are collected and reported together — not the first one.
- [ ] Each failure names the key, what was wrong and what was expected.
- [ ] Range checks: fractions within [0,1], `reserve_ratio` in (0,1], counts positive, `{{min, max}}` with min ≤ max.
- [ ] Cross-parameter checks that a single-key check cannot catch: tenure fractions summing to 1, wage tier shares summing to 1, `dsti_max` tight below loose.
- [ ] A configuration equal to every §13 default loads successfully and is a committed test fixture.
- [ ] The effective configuration — after defaults are applied — is written alongside the run output, so a result can always be traced to the exact parameters that produced it.

## Where to start

Collecting all errors rather than failing on the first is the difference between a usable loader and
an irritating one, and FluentResults supports it directly: accumulate into one failed Result rather than
returning early.

The 'effective configuration alongside the output' criterion is the one that will save an afternoon
six months from now. A CSV whose parameters cannot be recovered is not a result, and defaults change.
Write the resolved values, not the input file.

The all-defaults fixture doubles as a regression test on §13 itself: if someone adds a parameter to the
schema without a default, this test tells you immediately.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ConfigLoadTests
```

That a config with several distinct errors reports all of them. Reporting one at a time is the
default behaviour and has to be deliberately avoided.
