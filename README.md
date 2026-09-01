# economy-simulation

A closed-economy agent-based simulation of a single small town, built to test one question:

> How does the availability of consumer credit — and the social willingness to use it — affect
> prices, real consumption, and the distribution of income?

The town has households, about eighty firms across twelve sectors, a bank, and (later) a state. It is closed:
no trade with the outside world, no migration. Every euro spent is someone's income and every
loan is someone's asset, which is what makes credit effects visible rather than assumed.

The simulation is run under contrasting settings — high versus low credit appetite, banks that
create money at a 5:1 ratio versus banks that must hold every deposit in full — and the resulting
price, consumption and distribution paths are compared. A state that taxes, spends and can issue
money is specified but deferred to a later phase.

## Status

Specification stage. No engine code yet; the implementation language is settled (C# on .NET 10 —
see below).

- [`docs/MODEL.md`](docs/MODEL.md) — full model specification: agents, goods, behaviour,
  bank lending capacity, scenarios, metrics, and the limits of what the model can show.
- [`docs/DECISIONS.md`](docs/DECISIONS.md) — design decisions and the reasoning behind them.
- [`docs/REVIEW-BACKLOG.md`](docs/REVIEW-BACKLOG.md) — known specification defects not yet fixed.
- [`docs/LANGUAGE-CHOICE.md`](docs/LANGUAGE-CHOICE.md) — how the implementation language was
  chosen: the benchmark, the measurements, and the argument for C#. Sources in
  [`bench/`](bench/).
- [`stories/`](stories/) — the implementation backlog: 68 stories across 12 epics, each with
  acceptance criteria and an objective pass/fail check. Start with
  [`stories/README.md`](stories/README.md), which sets out the six verification devices this
  project relies on in place of an external oracle.

## Approach

The model is built so that it can refute the hypothesis. Any assumption strong enough to
produce the expected result on its own is exposed as a switch, and the opposing setting is
always run as a control. Results are reported with sensitivity ranges, and runs that
contradict the hypothesis are published alongside those that support it.

## Licence

GPL-3.0
