# Loading and validating a configuration

**Epic:** E2 — Configuration and the world
**Depends on:** 02-01, 01-04
**New ground:** The first real use of `Result` at a boundary, and forward compatibility

## Story

As a maintainer, I want a configuration file loaded into the schema with every problem reported in one pass, so that a bad config costs one edit and an old config still runs.

## Acceptance criteria

- [ ] TOML in, schema out, as a `Result`. Malformed input never throws.
- [ ] **All** validation failures are collected and reported together, each naming the key, what was wrong and what was expected.
- [ ] An **absent** key is filled from its default and the run proceeds. An **unknown** key is an error — a misspelled key that is silently ignored produces a run at defaults that looks exactly like the run that was asked for.
- [ ] Range checks: shares in `[0,1]`, counts positive, `life ≥ 1`, `term ≥ 1`, `σ > 0`.
- [ ] Cross-parameter checks a single-key check cannot catch: tier unit shares summing to 1;
      **the incremental value-for-money ratio strictly decreasing up the ladder**, since quality
      must have diminishing returns; `warmup_ticks < ticks`.

      *Corrected during implementation.* This criterion first read "`value_mult` strictly below
      `price_mult` for every tier above budget", which the specification's own defaults fail:
      standard is the reference tier, where both multipliers are 1.00 by construction. The
      property actually being protected is the one `02-PARAMETERS.md` §3.2 states —
      `value_mult` rises more slowly than `price_mult` — and its testable form is that
      `Δvalue / Δprice` falls at every step: 1.133, then 0.800, then 0.500. That is stronger
      than the original wording, and it is the thing the inverted-ladder failure would break.
- [ ] The **effective configuration**, after defaults are applied, is written beside the run output.

## Where to start

The absent-versus-unknown asymmetry is the whole design. Absent means "an older file, or a caller
who does not care" and is filled from defaults, which is what lets a scenario written today still
run against a model that has grown three mechanisms. Unknown means "a typo, or a parameter someone
invented" and must fail.

The `value_mult < price_mult` cross-check deserves its place. If someone sets a premium tier that is
better value for money than standard, the upgrade ladder inverts, every household jumps straight to
premium, and the run produces numbers that look fine. It is a one-line check against a failure that
would take a day to find.

Writing the *resolved* configuration rather than the input file is what will save an afternoon six
months from now. A CSV whose parameters cannot be recovered is not a result, and defaults change.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ConfigTests
```

A config with three problems reporting all three; an old fixture missing later keys still loading;
an inverted tier multiplier being rejected.
