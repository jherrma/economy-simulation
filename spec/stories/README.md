# Backlog — the v1 engine

Implementation backlog for [`../01-SIMULATION.md`](../01-SIMULATION.md). Parameters in
[`../02-PARAMETERS.md`](../02-PARAMETERS.md); the checks that stand in for a reference
implementation in [`../03-VERIFICATION.md`](../03-VERIFICATION.md).

**33 stories across 9 epics.** One story per file, `<epic>-<story>-<slug>.md`. Every story carries
an objective pass/fail check — nothing is "done" because it looks done. No story depends on a story
later in the build order, and that is mechanically checkable from the headers.

> The 71-story backlog in [`../../draft/stories/`](../../draft/stories/) is for the **full** model
> and is not scheduled. It is the list of objections to answer after v1 produces a number.

---

## Build order

| Epic | Theme | Stories | Why here |
|---|---|---|---|
| **E1** | Foundations | 5 | The value types and the RNG. Everything downstream inherits their guarantees, and none of them can be retrofitted |
| **E2** | Configuration and the world | 4 | Where "a parameter not in `02-PARAMETERS.md` does not exist" becomes enforceable, and where the two calibration identities are asserted |
| **E3** | The ledger and the opening state | 4 | V1 goes green here and stays green. The seven-step tick exists, doing nothing, in order |
| **E4** | The decision | 5 | The core: value, cost, upgrade candidates, and the walk. ~95% of the tick |
| **E5** | Prices | 2 | Demand accounting and the reprice rule. Small, and the place the model is most easily wrong in silence |
| **E6** | Credit | 4 | The subject of the experiment |
| **E7** | Output | 3 | Including the cohort metrics, which are the finding rather than a raw number |
| **E8** | Validation gates | 4 | V2–V5 as runnable programs |
| **E9** | Scenarios and the campaign | 2 | Five scenarios, thirty seeds, paired |

Two ordering rules override convenience:

- **The ledger and V1 come before any behaviour** (E3 before E4), so every later story is developed
  against a running invariant rather than having consistency retrofitted.
- **The null run comes before credit** (08-03 before E6 is finished). A drifting baseline makes
  every later number meaningless, and the drift is far easier to find with no credit in the model.

---

## How this project is verified

There is no reference implementation to diff against, and a wrong economy produces plausible numbers
rather than a crash. Six devices stand in for an oracle, detailed in
[`../03-VERIFICATION.md`](../03-VERIFICATION.md):

| | Device | Story |
|---|---|---|
| **V1** | Money conservation, to the cent, every tick | 03-02 |
| **V2** | Bit-identical determinism, including an unused-stream check | 01-05, 08-01 |
| **V3** | Nominal neutrality | 08-02 |
| **V4** | The null run, including tier-mix stationarity | 08-03 |
| **V5** | Credit-off regression, byte for byte | 08-04 |
| **V6** | Bounds and sanity, as assertions inside the tick | 03-02, 05-01 |

**V1 is this project's white-furnace test**: a single invariant, trivially cheap, that any
money-creating or money-destroying bug violates on the tick it is made. Every story from E3 onward
must leave it green.

**V5 is what V2 is for.** Byte-identical reproducibility is only interesting because it lets the
difference between two runs be attributed to exactly one cause. Every mechanism added after v1
inherits the pattern: it arrives behind a switch, and *off* reproduces the previous version byte for
byte on the same seeds.

---

## Rules for this project

Scope, not difficulty, is this project's failure mode — the specification already grew to 4,600
lines once.

- **Nothing from `../../draft/` gets implemented because it is easy.** If a mechanism is not in
  `01-SIMULATION.md`, it is not in v1, however small it looks while you are in the code.
- **No exceptions.** A method that can fail returns a `Result`. At the boundaries — config, loading,
  I/O, the consistency check — never per-candidate inside the walk, where a failure is a bug rather
  than a domain outcome and where allocation is forbidden.
- **Money is integer cents.** No floating-point euros anywhere.
- **The engine writes CSV and nothing else.** No statistics, no plotting, no analysis.
- **Do not tune to make a check pass.** A result that contradicts the specification stops the work:
  the spec is corrected first, with the contradiction written down. A parameter tuned until a gate
  passes, with no recorded reason, is how a model stops being evidence and becomes an illustration.
- **Do not calibrate the tier mix.** It is the output the experiment turns on.

---

## The four places this model is most easily wrong in silence

Each has a story that names it, because none of them crashes and none is caught by any other check.

| | Where | Story |
|---|---|---|
| `unaffordable` counted as demand — prices rise on goods nobody can buy | 05-01 | Demand accounting |
| Tiers ranked by total score instead of incremental — everyone buys budget, premium never sells | 04-03 | Upgrade candidates |
| Interest destroyed with principal — the money stock leaks and damps the measured effect | 06-03 | Money creation |
| Durable ages initialised to zero — a sawtooth that looks like a business cycle | 02-04 | Household draws |

---

## Story index

### E1 — Foundations
- [01-01](01-01-project-skeleton-and-safety-settings.md) — Project skeleton and the safety settings
- [01-02](01-02-money-value-type.md) — `Money`, in integer cents, with no way in from `double`
- [01-03](01-03-rate-value-type.md) — `Rate`, whose only exit applies the correct divisor
- [01-04](01-04-result-based-error-handling.md) — Result-based failure, and an ignored `Result` is a build error
- [01-05](01-05-deterministic-rng-streams.md) — **V2**: deterministic RNG, seeded per stream

### E2 — Configuration and the world
- [02-01](02-01-parameter-schema.md) — The parameter schema
- [02-02](02-02-config-loading-and-validation.md) — Loading and validating a configuration
- [02-03](02-03-goods-table-and-tiers.md) — The goods table and the three tiers
- [02-04](02-04-household-draws.md) — Household draws: income, taste, θ, abstainers and durable ages

### E3 — The ledger and the opening state
- [03-01](03-01-accounts-and-transfers.md) — Accounts, the pool, and transfers
- [03-02](03-02-money-conservation.md) — **V1**: money conservation, and the halt
- [03-03](03-03-opening-state.md) — The opening state and the money identity
- [03-04](03-04-tick-skeleton.md) — The seven-step tick, doing nothing, in order

### E4 — The decision
- [04-01](04-01-flow-value.md) — `flow_value`: the Stone-Geary floor and the tier multiplier
- [04-02](04-02-flow-cost-and-tiers.md) — `flow_cost`, and prices per tier
- [04-03](04-03-upgrade-candidates.md) — Candidates are upgrades, not tiers
- [04-04](04-04-wants-and-ageing.md) — Wants, and ageing
- [04-05](04-05-the-shopping-walk.md) — The shopping walk

### E5 — Prices
- [05-01](05-01-demand-accounting.md) — Demand accounting: sold, blocked, unaffordable
- [05-02](05-02-the-reprice-rule.md) — The reprice rule

### E6 — Credit
- [06-01](06-01-the-loan-and-its-schedule.md) — The loan, and its schedule
- [06-02](06-02-the-finance-decision.md) — The finance decision inside the walk
- [06-03](06-03-money-creation-and-destruction.md) — Money creation, destruction, and the interest dividend
- [06-04](06-04-the-money-creation-switch.md) — The `money_creation` switch

### E7 — Output
- [07-01](07-01-metrics-writer.md) — The metrics writer
- [07-02](07-02-cpi-and-tier-mix.md) — CPI, and the realised tier mix
- [07-03](07-03-cohort-metrics.md) — Cohort metrics: the finding itself

### E8 — Validation gates
- [08-01](08-01-determinism-gate.md) — **V2**: the determinism gate
- [08-02](08-02-neutrality-gate.md) — **V3**: the nominal neutrality gate
- [08-03](08-03-null-run-gate.md) — **V4**: the null run, and tier-mix stationarity
- [08-04](08-04-credit-off-regression-gate.md) — **V5**: the credit-off regression gate

### E9 — Scenarios and the campaign
- [09-01](09-01-scenario-definition.md) — Defining a scenario
- [09-02](09-02-campaign-runner.md) — The runner, and paired-seed comparison

---

## Story file convention

Each file carries: a title; `Epic` / `Depends on` / `New ground` header lines; the story in
as-a/I-want/so-that form; acceptance criteria as checkboxes; a **prose** implementation hint with no
code; and a **How to verify** block giving the command and what to look for.

Hints are deliberately prose. A story should survive the implementation changing under it, and a
story that contains the answer stops being a specification of *behaviour* and becomes a diff waiting
to rot.
