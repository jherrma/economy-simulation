# Defining a scenario

**Epic:** E9 — Scenarios and the campaign
**Depends on:** 02-02
**New ground:** A named, versioned override of the defaults

## Story

As the person running the experiment, I want a scenario to be a small named file that overrides defaults, so that the difference between two runs is visible on one screen.

## Acceptance criteria

- [ ] A scenario is a name plus a set of overrides. **The defaults are `credit_off`**, so the baseline scenario file is empty.
- [ ] The five scenarios of `01-SIMULATION.md` §9: `credit_off`, `credit_low`, `credit_high`, `credit_high_no_money_creation`, `credit_high_willingness_rationing`.
- [ ] The scenario id is written on every output row.
- [ ] A scenario cannot introduce a key that is not in the schema (02-02), so a typo in a scenario file fails rather than silently running the baseline.
- [ ] A test asserts the five scenarios differ from the default in exactly the keys they claim to, and no others.
- [ ] Scenario files are committed.

## Where to start

Making the empty file the baseline is a small decision with a large payoff. The control arm is then
literally the defaults, so it cannot drift away from the treatment arm's setup through a change that
was only meant to affect one of them — which is the most common way a paired comparison quietly
stops being paired.

The "differs in exactly these keys" test is the guard against the other version of the same problem:
a scenario that accumulates an unrelated override over time and turns the comparison into a
two-variable experiment nobody remembers designing.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ScenarioTests
```

The exact-difference test for all five, and a typo'd key failing to load.
