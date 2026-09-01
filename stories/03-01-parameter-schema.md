# The §13 parameter schema

**Epic:** E3 — Configuration and opening state
**Depends on:** 01-02, 01-03
**New ground:** Every configurable quantity, typed, with §13's own rule enforced

## Story

As the model author, I want a schema mirroring §13 in which every parameter has a type, a unit and a default, so that 'a parameter that is not in the table does not exist' is enforced by the loader instead of by discipline.

## Acceptance criteria

- [ ] Every parameter in §13.1–§13.11 appears exactly once, with the type its unit implies: `Money` for euros, `Rate` for interest, a fraction type for shares, integers for counts.
- [ ] An **unknown key in the configuration file is a hard failure**, not a warning. This is §13's stated rule and it is the whole value of the appendix.
- [ ] A `{{min, max}}` range is its own type and cannot be read as a scalar by accident.
- [ ] Each parameter records whether it is swept, and whether it is one of the ~17 with no empirical anchor (§13.10), so §10's sensitivity programme can enumerate them rather than being hand-maintained.
- [ ] The ⚠ list is **derived from the table**, never hand-written — the hand-maintained version in the spec had already drifted before anyone checked.
- [ ] Rates are per cent per annum, entering `Rate` directly, so no call site sees a bare number.

## Where to start

Model the schema so the §13.10 list falls out of it. In the specification that list was maintained
by hand and was wrong by the time it was reviewed; the fix is to make it a query over the table rather
than a second copy of it. The same applies to which parameters are swept — E12's sensitivity runner
should be able to ask.

Rejecting unknown keys is more useful than it sounds. It catches the renamed parameter, the typo, and
the parameter someone added to a config file expecting the engine to honour it. All three otherwise
present as a run that silently used a default.

Do not attach behaviour to this type. It is a description of what may be configured; validation is
03-02 and the drawing of per-agent values is 03-03.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~SchemaTests
```

The unknown-key rejection, and a test asserting the ⚠ list matches §13.10's contents — if the two
ever diverge, the spec or the schema is stale and both should be looked at.
