# Specification — the model we are building

> **This is the implementation target.** [`../draft/`](../draft/) is a first idea, kept for
> reference and not scheduled.

The smallest model that can actually test the hypothesis: a thousand households, six goods in fixed
supply — each in three quality tiers, so that income and credit have somewhere to go — one adaptive
price per tier, and consumer credit that has to be repaid. A few hundred lines.

| File | What it is |
|---|---|
| [`01-SIMULATION.md`](01-SIMULATION.md) | The model: entities, the decision rule, the seven-step tick, money, scenarios, the measurement, and what it cannot show |
| [`02-PARAMETERS.md`](02-PARAMETERS.md) | Every parameter, its default and why. A parameter not in this file does not exist |
| [`03-VERIFICATION.md`](03-VERIFICATION.md) | The six devices that stand in for a reference implementation, and a table of what a plausible-but-wrong run looks like |
| [`stories/`](stories/) | The implementation backlog: 33 stories across 9 epics, each with acceptance criteria and an objective pass/fail check — plus E10 and E11, specified 2026-09-04 and not built |

## The question

> If A buys a good on credit because they cannot pay for it now, does the same good become more
> expensive — or harder to get — for B, who never borrows?

A fifth of households never borrow, in any scenario, and are the same households across scenarios
for a given seed. **What happens to them is the finding.** Four numbers, `credit_high` against
`credit_off`, paired by seed: the price index they face, the share of what they wanted that they
actually got, how long they waited for it, and **which quality tier they ended up on**.

The last of those is the sharpest claim the model can make. Credit does not stop the abstainer
owning a phone; the question is whether it moves them to a worse one.

The result may be null. A null result is publishable and is the reason the model is built to be
refutable rather than built to demonstrate.

## Why this and not the draft

The draft specified a whole town — eighty firms, twelve sectors, a housing market, CRR3 risk
weights, a status treadmill — across 4,600 lines and 71 stories, with no running simulation until
nearly all of them were done. Most of its content is right. Its *sequencing* was not: it front-loaded
every piece of rigour before any evidence existed about which parts mattered.

This spec inverts that. It produces a number first, and the draft becomes the list of objections to
answer next, in roughly the order they will be raised. Each of them arrives behind a switch whose
*off* setting reproduces this model byte-for-byte, so the difference it makes is measurable rather
than merely visible.

## Implementation constraints

Settled, and not open while implementing.

- **C# on .NET 10.** The benchmark and the reasoning are in
  [`../docs/LANGUAGE-CHOICE.md`](../docs/LANGUAGE-CHOICE.md); sources in [`../bench/`](../bench/).
- **`TreatWarningsAsErrors`, `CheckForOverflowUnderflow`, `Nullable enable`.** The safety settings
  are the reason the language was chosen; a project that does not build with them is not this project.
- **Money is a `readonly record struct` over integer cents**, with no conversion from `double`.
  Every monetary field is that type. There are no floating-point euros anywhere.
- **Rates are a value type** whose only exit applies the correct divisor. No literal `12` or `1200`
  at a call site.
- **No exceptions.** A method that can fail returns a `Result` (FluentResults). At the boundaries —
  config, loading, I/O, the consistency check — not per-candidate inside the shopping walk, where a
  failure is a bug rather than a domain outcome.
- **Households are indices into parallel arrays**, not objects. The walk is the hot loop and it must
  not allocate.
- **The engine writes CSV and nothing else.** No statistics, no plotting, no analysis inside it.

## Rules while building this

Scope, not difficulty, is this project's failure mode — it already grew to 4,600 lines of
specification once.

- **Nothing from `../draft/` gets implemented because it is easy.** If a mechanism is not in
  `01-SIMULATION.md`, it is not in v1, however small it looks while you are in the code.
- **V1 and V6 are assertions inside the tick**, live from the first commit that moves money.
- **V4 before credit.** A drifting baseline makes every later number meaningless, and the drift is
  much easier to find with no credit in the model.
- **Parameters default to the setting that disables the behaviour**, so the default configuration is
  the baseline and a scenario names only what it changes.
- **A result that contradicts the specification stops the work.** The spec is corrected first, with
  the contradiction written down. A parameter tuned until a check passes, with no recorded reason, is
  how a model stops being evidence and becomes an illustration.
