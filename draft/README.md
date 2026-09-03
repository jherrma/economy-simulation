# Draft — the first idea, kept for reference

> **Status: DRAFT. This is not the implementation target.**
> The specification we are actually building is [`../spec/`](../spec/).
> Nothing in this directory is scheduled. Nothing in it is settled. Do not implement from it.

This is where the project started: an attempt to specify, in one pass and before writing any code,
a stock-flow-consistent agent-based model of a whole small town — roughly a thousand households,
eighty firms across twelve sectors, a bank with CRR3 risk weights, a housing market with auction
chains, and a social status treadmill.

It grew to about 4,600 lines of specification, 71 implementation stories and 443 acceptance
criteria without a single line of engine code. Two review rounds found around 45 defects in it,
several of them fatal in the quiet way — a units error that would have silently pinned the price
level, and a headline metric that confounded the two effects it was supposed to separate.

The problem was never that the content was wrong. Most of it is right, and it is the reason the
project can answer objections rather than just produce a number. The problem was **sequencing**: it
front-loaded every piece of rigour before any evidence existed about which parts mattered, and no
milestone in it produced a running simulation until nearly all 71 stories were done.

So the order was inverted. [`../spec/`](../spec/) specifies the smallest model that can actually
test the hypothesis — a few hundred lines, runnable in a day — and this draft becomes what it should
have been from the start: **the list of objections to answer next**, in roughly the order they will
be raised.

## What is here

| File | What it is |
|---|---|
| [`MODEL-draft.md`](MODEL-draft.md) | The full model specification, ~2,700 lines. Agents, goods, behaviour, bank lending capacity, scenarios, metrics, and the limits of what it could show |
| [`DECISIONS-draft.md`](DECISIONS-draft.md) | D1–D32, the design decisions and the reasoning behind them. **Still worth reading** — most of these decisions survive into the real spec |
| [`REVIEW-BACKLOG-draft.md`](REVIEW-BACKLOG-draft.md) | Specification defects found in review and not yet fixed |
| [`DIAGRAMS-draft.md`](DIAGRAMS-draft.md) | Four views: the tick pipeline, the value types, the data layout, every entity |
| [`FOUNDATION-draft.md`](FOUNDATION-draft.md) | Nine structural seams for adding dimensions without rewriting. **Two of them (S2 named RNG streams, S3 the off-switch) are carried into the real spec**; the rest wait |
| [`MILESTONES-draft.md`](MILESTONES-draft.md) | The nine-milestone build order for the full model |
| [`stories/`](stories/) | 71 implementation stories across 12 epics, each with acceptance criteria and a pass/fail check |

## What survives into the real spec

Not the structure, but a fair amount of the thinking:

- **The question**, unchanged: does A's credit purchase raise the price B pays?
- **The abstainer cohort** as the measurement — the households that never borrow are the ones whose
  outcome is the finding (`MODEL-draft.md` §1.2).
- **The four-channel decomposition** T/M/N/R — timing, money creation, never-would-have,
  replacement cycle (§9). The real spec can only separate T and M; N and R need mechanisms it does
  not have yet.
- **User cost as a per-tick flow**, so a car and a restaurant meal are comparable, and so the
  interest rate sits *inside* the durables-versus-services comparison (§6.2, D31).
- **Money conservation as the primary invariant**, and byte-identical determinism as the thing that
  makes any comparison mean anything (`stories/README.md`, V1 and V2).
- **The standing limitations** that must accompany any published result (§12): no unemployment and
  no firm failure make the model structurally conservative about credit's harms; the single bank
  inflates the distributional channel; the model is calibrated to a world that already has credit,
  so the low-credit run is an extrapolation away from the calibration point.

## What was wrong with it

Worth writing down, because it is the reason this directory exists.

The model grew to be **more complex than the claim it was testing**. §13.10 lists seventeen
parameters with no empirical anchor. Every one of those is a researcher degree of freedom, and past
a certain count the result stops being a finding about credit and becomes a finding about the
parameters. That is not a flaw in any individual decision; it is what happens when a minimal model
drifts toward a descriptive one without anyone deciding to make the switch.

The real spec is deliberately on the other side of that line: few parameters, all of them stateable
in a sentence, and a mechanism simple enough that a reader can check the result in their head before
believing the code.
