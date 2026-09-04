# economy-simulation

A closed-economy agent-based simulation of a single small town, built to test one claim:

> If A buys a good on credit because they cannot pay for it now, does the same good become more
> expensive — or harder to get — for B, who never borrows?

The town is closed: no trade with the outside world, no migration. Every euro spent is someone's
income and every loan is someone's asset, which is what makes credit effects visible rather than
assumed.

## Status

**All nine epics of v1 complete**, with 463 tests. A tenth epic — a population of household
archetypes with per-category taste and quality steepness — is **specified and not built**
([§5.4](spec/01-SIMULATION.md), [§3.5](spec/02-PARAMETERS.md),
[E10](spec/stories/README.md)). The model runs: a population, a ledger of integer
cents, a shopping walk over quality tiers, consumer credit that creates money and has to be repaid,
and two CSV files of output. The campaign runs it five scenarios wide and thirty seeds deep, one
process per run, and collects one dataset:

```sh
dotnet run --project tools/Campaign -- --all
```

150 runs in about seventy seconds, into `campaign/` — the measured window of every run, with the
scenario and the seed on every row, beside a manifest naming the engine commit and the hash of the
effective configuration each scenario actually ran. Nothing is differenced, averaged or plotted:
the campaign produces a dataset, not a result.

**All six verification devices are green.** V1 and V6 run inside every tick; V2 to V5 are a program:

```sh
dotnet build -warnaserror && dotnet test
dotnet run --project tools/Gates -- determinism   # V2 — same seed, twice and across threads
dotnet run --project tools/Gates -- neutrality    # V3 — scale every nominal quantity, nothing real moves
dotnet run --project tools/Gates -- nullrun       # V4 — the creditless baseline sits still
dotnet run --project tools/Gates -- creditoff     # V5 — credit off reproduces the baseline, byte for byte
```

Three of those gates changed the specification rather than the other way round, which is the point
of having them: [§7.3](spec/01-SIMULATION.md) (the money-creation channel is zero by construction),
[§10.2](spec/01-SIMULATION.md) (one cent decorrelates a run, so no result may be read off a single
seed), and [§10.3](spec/01-SIMULATION.md) (relative prices converge eight times slower than the
price level — which is why `warmup_ticks` is 240 and not 120).

A fourth finding came out of E9's own dry run rather than a gate: [§10.4](spec/01-SIMULATION.md)
measures the headline at ten to sixty times the spread across thirty seeds, and corrects two ways of
reading it that would have reported the wrong sign or the wrong size.

What remains is the analysis and the write-up, neither of which lives in this repository. The
[Notes](Notes.md) are the working model of what has been found so far.

The language is settled (C# on .NET 10).

The repository holds two specifications, and the difference between them matters:

### [`spec/`](spec/) — **the implementation target**

The smallest model that can test the claim: a thousand households with a fixed monthly income, six
categories of goods produced in fixed quantity each tick — each in a budget, standard and premium
tier with its own price — and consumer credit that creates money and has to be repaid. A fifth of
households never borrow, and what happens to *them* is the finding: what they pay, what share of
what they wanted they got, how long they waited, and **which tier they ended up on**.

Start at [`spec/README.md`](spec/README.md). The backlog is
[`spec/stories/`](spec/stories/) — 33 stories across 9 epics.

### [`draft/`](draft/) — **a first idea, kept for reference**

The project's original attempt: a full stock-flow-consistent model of the whole town — eighty firms
across twelve sectors, a bank with CRR3 risk weights, a housing market with auction chains, a social
status treadmill — specified across ~4,600 lines and 71 implementation stories before any code
existed. Two review rounds found around 45 defects in it. Its backlog is
[`draft/stories/`](draft/stories/) and is **not** the one to build from.

Most of its content is right, and it is why the project can answer objections rather than only
produce a number. Its *sequencing* was not: no milestone in it produced a running simulation until
nearly everything was done. It is now the list of objections to answer next, in roughly the order
they will be raised. **Nothing in it is scheduled. Do not implement from it.**

### Also here

- [`src/`](src/) and [`tests/`](tests/) — the engine, the runner, the analysers and the tests.
- [`tools/Gates/`](tools/Gates/) — the validation gates as a runnable program, and the committed
  baselines V5 compares against.
- [`docs/REVIEW-CHECKLIST.md`](docs/REVIEW-CHECKLIST.md) — the review items no test can check.
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
