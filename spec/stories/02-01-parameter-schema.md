# The parameter schema

**Epic:** E2 — Configuration and the world
**Depends on:** 01-02, 01-03
**New ground:** "A parameter not in `02-PARAMETERS.md` does not exist", made enforceable

## Story

As a maintainer, I want every parameter in the specification represented once, in a typed schema with its default, so that nothing in the engine can depend on a number nobody wrote down.

## Acceptance criteria

- [ ] One record per section of `02-PARAMETERS.md`: run, income, categories, tiers, decision, credit, prices, money.
- [ ] Every parameter carries its **default from the specification**, and the defaults are the values in that file — a test compares the two so drift is impossible.
- [ ] **Every parameter that gates a behaviour defaults to the setting that disables it**: `credit_enabled = false` above all.
- [ ] Monetary parameters are `Money`; `loan_rate` is a `Rate`; shares are constrained doubles.
- [ ] The schema can print itself, so a run's parameters can be read without reading the code.
- [ ] A committed fixture holds the full default configuration and is used by every other test that needs one.

## Where to start

The rule this story enforces is the one the draft model lost: its appendix grew to seventeen
parameters with no empirical anchor, largely because parameters could be introduced at a call site
and documented later, or not at all. Making the schema the single source and testing it against the
specification file closes that route.

The disable-by-default rule is what makes the baseline free. `credit_off` is then not a scenario
that has to be configured; it is what you get by running the defaults, which means the control arm
can never quietly drift away from the treatment arm's setup.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~SchemaTests
```

The schema-versus-specification comparison, and the default configuration having `credit_enabled`
false and `loans_outstanding` zero after a run.
