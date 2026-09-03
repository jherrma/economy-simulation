# economy-simulation

A closed-economy agent-based simulation of a single small town, built to test one claim:

> If A buys a good on credit because they cannot pay for it now, does the same good become more
> expensive — or harder to get — for B, who never borrows?

The town is closed: no trade with the outside world, no migration. Every euro spent is someone's
income and every loan is someone's asset, which is what makes credit effects visible rather than
assumed.

## Status

**Specification stage, about to become implementation.** No engine code yet. The language is settled
(C# on .NET 10).

The repository holds two specifications, and the difference between them matters:

### [`spec/`](spec/) — **the implementation target**

The smallest model that can test the claim: a thousand households with a fixed monthly income, six
categories of goods produced in fixed quantity each tick — each in a budget, standard and premium
tier with its own price — and consumer credit that creates money and has to be repaid. A fifth of
households never borrow, and what happens to *them* is the finding: what they pay, what share of
what they wanted they got, how long they waited, and **which tier they ended up on**.

Start at [`spec/README.md`](spec/README.md).

### [`draft/`](draft/) — **a first idea, kept for reference**

The project's original attempt: a full stock-flow-consistent model of the whole town — eighty firms
across twelve sectors, a bank with CRR3 risk weights, a housing market with auction chains, a social
status treadmill — specified across ~4,600 lines and 71 implementation stories before any code
existed. Two review rounds found around 45 defects in it.

Most of its content is right, and it is why the project can answer objections rather than only
produce a number. Its *sequencing* was not: no milestone in it produced a running simulation until
nearly everything was done. It is now the list of objections to answer next, in roughly the order
they will be raised. **Nothing in it is scheduled. Do not implement from it.**

### Also here

- [`docs/LANGUAGE-CHOICE.md`](docs/LANGUAGE-CHOICE.md) — how the implementation language was chosen:
  the benchmark, the measurements, and the argument for C#. Settled, and applies to `spec/`.
- [`bench/`](bench/) — the benchmark sources, in six languages.

## Approach

The model is built so that it can refute the claim. Any assumption strong enough to produce the
expected result on its own is exposed as a switch, and the opposing setting is always run as a
control. Results are reported with sensitivity ranges, and runs that contradict the hypothesis are
published alongside those that support it.

Every mechanism added after v1 arrives behind a switch whose *off* setting reproduces the previous
version byte-for-byte on the same seeds. The difference it makes is therefore a measurement of that
one mechanism, not the difference between two versions of a program.

## Licence

GPL-3.0
