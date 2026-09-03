# Defining a scenario

**Epic:** E12 — Scenarios and the campaign
**Milestone:** M2
**Depends on:** 03-02, 10-01
**New ground:** A scenario as data, so the grids of §8 are enumerable

## Story

As the model author, I want a scenario expressed as a named set of parameter overrides on a base configuration, so that §8's grids can be generated rather than hand-written.

## Acceptance criteria

- [ ] A scenario is a name plus overrides on the base configuration; the effective configuration is resolved and written per run (03-02).
- [ ] The six named runs of Grid A exist, including `low_credit_light` and `high_credit_gold`.
- [ ] Grids B and C are expressible as products of override sets.
- [ ] A scenario declares which seeds it uses — **the same 30 across all scenarios** (§13.1).
- [ ] Scenario ids are stable and appear on every output row, so files can be concatenated.
- [ ] An invalid scenario — one whose overrides fail 03-02's validation — is rejected before any run starts, not after 29 seeds have completed.

## Where to start

Generating grids rather than listing them is what keeps §8 and the code from diverging, which is how the
2×2 goods grid went wrong in the specification. A product of override sets is enough structure.

Validating every scenario up front is a small thing that saves a great deal: a campaign that fails on
scenario 140 of 150 after four hours has wasted the four hours.

Keep scenario ids stable across edits. They end up in filenames and in charts, and renumbering them
silently invalidates any comparison someone has already drawn.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ScenarioTests
```

The six Grid A runs enumerating correctly, and an invalid override rejected before execution.
