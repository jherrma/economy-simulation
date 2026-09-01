# Household state

**Epic:** E4 — The world, and an empty tick
**Milestone:** M0
**Depends on:** 03-03, 02-01
**New ground:** The behavioural parameters and holdings, joined to the balance sheet

## Story

As the model author, I want a household to carry its drawn parameters, its holdings, its tenure and its employment in one place, so that every behavioural story has a defined place to read from and write to.

## Acceptance criteria

- [ ] Drawn parameters: `θ`, `φ`, `κ`, `σ`, `π`, `ε`, `τ`, plus derived `λ` (§13.2).
- [ ] Holdings per sector, with age and generation for goods subject to obsolescence (§4.2).
- [ ] Tenure, dwelling(s) held, and whether a landlord.
- [ ] Employment: which firm, which wage tier, current wage.
- [ ] Membership of the abstainer cohort, as a flag set at initialisation and never changed.
- [ ] `stress`, and the credit record (denial window, risk premium).
- [ ] **`λ` is derived, never stored as an independent field** — §13.2 says derived, and a stored copy will drift from `φ` and the buffer.
- [ ] State lives in flat arrays indexed by household id, not as objects (`LANGUAGE-CHOICE.md` §7).

## Where to start

The temptation is to make this a class with properties and let each story add one. That is exactly what
the flat-array commitment forbids, and the reason is measured rather than aesthetic: the hot loop in E5
walks every household every tick, and cache locality is most of the gap between the compiled languages
in the benchmark.

Be strict about `λ`. It is a function of `φ` and the current buffer, and the moment it is stored someone
will update it in one place and not another. The same applies to debt-service ratios and loan-to-value:
computed, not kept.

The abstainer flag being immutable is worth enforcing rather than documenting. Its whole purpose is that
the cohort does not drift with the treatment.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~HouseholdStateTests
```

That λ has no setter, and that the abstainer flag cannot be reassigned after initialisation.
