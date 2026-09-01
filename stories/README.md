# Backlog — economy-simulation engine

Implementation backlog for the model specified in [`../docs/MODEL.md`](../docs/MODEL.md).
Language decision and its reasoning: [`../docs/LANGUAGE-CHOICE.md`](../docs/LANGUAGE-CHOICE.md).

**71 stories across 12 epics, built in 9 milestones.** One story per file,
`<epic>-<story>-<slug>.md`. Every story carries an objective pass/fail check — nothing is "done"
because it looks done, and every story carries the milestone it belongs to.

**Read [`MILESTONES.md`](MILESTONES.md) first.** The epics are a thematic grouping; the milestones
are the build order, and they are what makes the project deliver a running simulation early and one
economic dimension at a time. [`../docs/FOUNDATION.md`](../docs/FOUNDATION.md) is the other half of
that: the nine structural seams that make adding a dimension additive rather than a rewrite.

`§` always refers to a section of `MODEL.md`. `D<n>` refers to a decision in
[`../docs/DECISIONS.md`](../docs/DECISIONS.md); `B<n>` to an item in
[`../docs/REVIEW-BACKLOG.md`](../docs/REVIEW-BACKLOG.md).

---

## How this project is verified

**There is no reference implementation to diff against.** A wrong economy produces plausible
numbers, not a crash, and no test can assert "this economy is incorrect". Verification therefore
rests on six devices, each of which must keep passing for the life of the project. They are
load-bearing: do not weaken one to make a story pass.

| # | Device | What it catches |
|---|---|---|
| **V1** | **Base-money conservation** — `M0 = public cash + firm cash + bank reserves`, to the cent, every tick, at every `reserve_ratio` (§10 item 2) | Almost every accounting bug. Cheap, universal, and it fires on the tick the mistake is made rather than a hundred ticks later |
| **V2** | **Bit-identical determinism** — the same seed produces the same run, byte for byte, at any degree of parallelism | Order-dependence, uninitialised state, and accidental shared mutable state |
| **V3** | **Money neutrality** (§10 item 4) — scale `M0`, cash, deposits, principals, equity, wages and posted prices *simultaneously*; nothing real may change | Nominal illusion hidden in a decision rule. This is the exact defect D26 fixed in the spec, and it must not return in code |
| **V4** | **The null run** (§10 item 3) — with credit stationary, prices must be constant | Secular drift that would otherwise be misread as a result |
| **V5** | **Compile-fail tests** — the safety settings are only real if wrong code *fails to build* | Silent erosion of the guarantees D27 was chosen for |
| **V6** | **The scoring benchmark** ([`../bench/`](../bench/)) as a performance gate | Accidental allocation or an O(n log n) → O(n²) regression in the one loop that matters |
| **V7** | **Milestone regression** (01-06) — with a dimension switched off, the engine reproduces the previous milestone byte-for-byte on the same seeds | Anything a new dimension disturbed that it had no business disturbing. It is also the *measurement*: the on-versus-off difference is that dimension's attributed effect |

**V1 is this project's white-furnace test.** It is the analogue of the raytracer's albedo-1.0
check: a single invariant, trivially cheap, that any energy-creating or energy-destroying bug
violates immediately. Every story from E2 onward must leave it green.

**V2 is this project's per-pixel seeding.** Streams are seeded from `(run_seed, agent_id, purpose)`
and never from one shared generator, so adding a story, reordering agents, or changing the thread
count cannot perturb output. §13.1 requires the *same 30 seeds across scenarios, compared paired* —
that protocol is worthless if a run is not reproducible.

**V7 is what V2 is for.** Byte-identical reproducibility is only interesting because it lets the
difference between two runs be attributed to exactly one cause. Without V2 there is no V7, and
without V7 the milestone strategy degenerates into "the model got bigger and the numbers changed".

---

## Rules for this project

Scope, not difficulty, is this project's failure mode. The specification already says no to these;
the backlog says no again, because they are the rabbit holes that will present themselves as
"obvious next steps" while implementing.

- **No unemployment.** Every household holds a position at all times (D20). Income shocks run
  through demotion only.
- **No firm failure.** Wage cut → capital call → loan, and the ladder has no last rung (D22).
- **No intermediate goods.** Firms buy labour only; wages are the whole of marginal cost (D22).
- **No state, no taxes, no transfers.** §5.4 is phase 5 and absent from v1.
- **No endogenous share price.** Valuation is `trailing earnings × earnings_multiple` (D16).
- **No demography, no inheritance, no ageing** (B21).
- **No house-price expectations** and no home-equity withdrawal (both open deliberately).
- **The engine writes CSV or Parquet and nothing else.** No analysis, no statistics, no plotting
  inside the C# project (D27).
- **No exceptions.** Methods that can fail return a `Result`. At the boundaries — config, loader,
  I/O, the consistency check — not per-candidate inside the §6.2 hot loop, where a failure is a
  bug rather than a domain outcome, and where allocation is forbidden.
- **Do not optimise before E5's gate tells you to.** The benchmark exists so that performance work
  is triggered by a measurement rather than a hunch.
- **Do not build ahead of the milestone.** A dimension implemented early cannot be measured, because
  the milestone it would have been measured against no longer exists. If a story from M5 looks easy
  while you are in M2, that is not a reason to do it.
- **A milestone that contradicts the specification stops.** The specification is corrected in
  `DECISIONS.md`, with the contradiction stated, before the milestone continues. A parameter tuned
  until a gate passes, with no recorded reason, is how a model stops being evidence and becomes an
  illustration.

---

## Build order

The build order is the **milestone** order in [`MILESTONES.md`](MILESTONES.md), not the epic order.
Nine milestones, each of which runs and passes its own gate, each adding one economic dimension
behind a switch that reproduces its predecessor when turned off.

| | Milestone | Stories | Cumulative |
|---|---|---|---|
| **M0** | A town that conserves money | 24 | 24 |
| **M1** | A town that trades | 15 | 39 |
| **M2** | **Credit** — the first answer to the question the project exists to ask | 10 | **49** |
| **M3** | Saving and the portfolio | 3 | 52 |
| **M4** | The treadmill | 3 | 55 |
| **M5** | Credit under stress | 4 | 59 |
| **M6** | Housing | 4 | 63 |
| **M7** | The supply side answers back | 5 | 68 |
| **M8** | The campaign | 3 | 71 |

Within that, one ordering rule from the specification still holds absolutely: **the ledger and its
assertions are written before any agent behaviour**, so that every later story is developed against a
running invariant rather than having consistency retrofitted. That is why M0 is as large as it is.

## Epics — themes, not build order

| Epic | Theme | Stories | Why grouped |
|---|---|---|---|
| **E1** | Foundations you cannot retrofit | 6 | The value types, the RNG, and the regression harness. Everything downstream inherits their guarantees |
| **E2** | The ledger | 6 | V1 goes green here and stays green. No behaviour yet |
| **E3** | Configuration and opening state | 7 | The loader is where "a parameter not in §13 does not exist" becomes enforceable |
| **E4** | The world, and an empty tick | 5 | Agents and the 17-step skeleton, doing nothing, in the right order |
| **E5** | The purchase decision | 8 | §6.2 — the core, ~95% of the tick, and the only part that resists optimisation |
| **E6** | Status, stress, saving | 6 | The feedbacks that make demand move |
| **E7** | Credit | 9 | The subject of the experiment |
| **E8** | Housing | 5 | The largest price effect and the trickiest clearing |
| **E9** | Firms | 6 | Supply, wages, and where profit goes |
| **E10** | Metrics and output | 5 | Including the headline decomposition, which is not a raw number |
| **E11** | Validation gates | 4 | V3 and V4 become runnable programs |
| **E12** | Scenarios and the campaign | 4 | The sweep runner; one process per seed |

---

## Concept coverage map

Where each part of the specification is implemented:

| `MODEL.md` | Stories |
|---|---|
| §3.1 Money and initial balances | 02-03, 03-04, 03-05 |
| §4 Goods, §4.1 transport, §4.2 obsolescence | 04-01, 05-01, 06-02 |
| §5.1 Household, §5.1.1–5.1.3 | 04-02, 03-03, 03-06, 09-03 |
| §5.2 Firm, §5.2.1–5.2.3 | 04-03, 09-01, 09-02, 09-04, 09-05, 09-06 |
| §5.3 Bank, §5.3.1 second-hand, §5.3.2 runs | 04-04, 07-08, 07-09 |
| §6.1 Tick sequence | 04-05 |
| §6.2 The purchase decision | 05-01 … 05-08 |
| §6.3 Status, §6.4 stress | 06-01, 06-02, 06-03 |
| §6.5 Housing | 08-01 … 08-05 |
| §6.6 Saving, cash-vs-deposit, time deposits | 06-04, 06-05, 06-06 |
| §6.7 Budget shares as output, §6.8 overextension | 10-02, 07-06 |
| §7.1–7.3 Lending capacity and pricing | 07-03, 07-04, 06-06 |
| §8 Scenarios | 12-01, 12-04 |
| §9 Metrics | 10-01 … 10-05 |
| §10 Validation | 02-05, 11-01 … 11-04 |
| Seams (`FOUNDATION.md` S1–S9) | 01-05, 01-06, 02-01, 03-07, 04-01, 04-05, 05-01, 10-01 |
| §13 Parameter appendix | 03-01, 03-02, 03-07 |

---

## Story index

### E1 — Foundations you cannot retrofit
- [01-01](01-01-project-skeleton-and-safety-settings.md) — Project skeleton and the safety settings
- [01-02](01-02-money-value-type.md) — `Money`, in integer cents, with no way in from `double`
- [01-03](01-03-rate-value-type.md) — `Rate`, whose only exit applies the correct divisor
- [01-04](01-04-result-based-error-handling.md) — Result-based failure, and an ignored Result is a build error
- [01-05](01-05-deterministic-rng-streams.md) — Deterministic RNG, seeded per stream
- [01-06](01-06-milestone-regression-harness.md) — **V7**: the milestone regression harness

### E2 — The ledger
- [02-01](02-01-accounts-and-balance-sheets.md) — Accounts and the agent balance sheet
- [02-02](02-02-transfers-and-settlement-order.md) — Transfers, and settlement cash-first
- [02-03](02-03-base-money-conservation.md) — **V1**: base-money conservation
- [02-04](02-04-inside-claims-net-to-zero.md) — Inside claims net to zero
- [02-05](02-05-consistency-check-halts-the-run.md) — The consistency check, and halting on violation
- [02-06](02-06-ledger-property-tests.md) — Property tests: random transfer sequences

### E3 — Configuration and opening state
- [03-01](03-01-parameter-schema.md) — The §13 parameter schema
- [03-02](03-02-config-loading-and-validation.md) — Loading and validating a configuration
- [03-03](03-03-seeded-agent-draws.md) — Seeded per-agent draws, including correlated θ and φ
- [03-04](03-04-opening-balance-sheet.md) — The opening balance sheet
- [03-05](03-05-loader-assertions.md) — Loader assertions on reserves and bank equity
- [03-06](03-06-initialisation-ordering.md) — Initialisation ordering, and breaking the tenure circularity
- [03-07](03-07-additive-configuration.md) — Additive configuration: new parameters default to the old behaviour

### E4 — The world, and an empty tick
- [04-01](04-01-goods-table.md) — The goods table
- [04-02](04-02-household-state.md) — Household state
- [04-03](04-03-firm-state-and-wage-hierarchy.md) — Firm state and the wage hierarchy
- [04-04](04-04-bank-state.md) — Bank state
- [04-05](04-05-tick-skeleton.md) — The 17-step tick, doing nothing, in order

### E5 — The purchase decision
- [05-01](05-01-value-function.md) — `value`: joy, mobility, and CPI indexation
- [05-02](05-02-user-cost-function.md) — `user_cost`: depreciation and running cost
- [05-03](05-03-score-and-lambda.md) — `score`, and λ as a reservation ratio
- [05-04](05-04-candidate-generation.md) — Candidate units, and indivisible durables
- [05-05](05-05-filter-then-sort-and-walk.md) — Filter before sorting, then the descending walk
- [05-06](05-06-settlement-cash-first.md) — Settlement into the ledger
- [05-07](05-07-scoring-performance-gate.md) — **V6**: the performance gate
- [05-08](05-08-financing-in-user-cost.md) — The financing term in `user_cost` *(M2)*

### E6 — Status, stress, saving
- [06-01](06-01-relative-status.md) — Relative status, and why the marginal form is required
- [06-02](06-02-obsolescence-and-generations.md) — Generations and status decay
- [06-03](06-03-stress.md) — Stress, and its switch
- [06-04](06-04-buffer-target-and-lambda-feedback.md) — The buffer target φ and its feedback into λ
- [06-05](06-05-cash-versus-deposits.md) — The cash-versus-deposit choice, κ
- [06-06](06-06-time-deposits.md) — Time deposits, and what they mean for the reserve test

### E7 — Credit
- [07-01](07-01-loan-schedule.md) — The loan schedule, and not double-counting interest
- [07-02](07-02-credit-standards.md) — Credit standards, for households and for firms
- [07-03](07-03-lending-capacity.md) — Lending capacity: the three-way minimum
- [07-04](07-04-scarcity-pricing.md) — The price of credit under scarcity
- [07-05](07-05-credit-queue.md) — One queue, in seeded random order
- [07-06](07-06-arrears-and-default.md) — Arrears, forbearance, and the choice to default
- [07-07](07-07-repossession-and-marking.md) — Repossession, marked in two steps
- [07-08](07-08-second-hand-market.md) — The second-hand market, with a clearing price
- [07-09](07-09-terminating-conditions.md) — Bank runs and negative equity

### E8 — Housing
- [08-01](08-01-rental-market.md) — The rental market, and sitting tenants
- [08-02](08-02-housing-valuation-and-credit-limit.md) — Valuation, and defining `credit_limit`
- [08-03](08-03-auction-and-reserve-prices.md) — The auction, and the option to do nothing
- [08-04](08-04-chain-resolution.md) — Simultaneous settlement and the chain problem
- [08-05](08-05-construction.md) — Construction

### E9 — Firms
- [09-01](09-01-pricing-and-production.md) — Pricing and production
- [09-02](09-02-annual-wage-review.md) — The annual wage review
- [09-03](09-03-profit-distribution-and-shares.md) — Profit distribution and the share register
- [09-04](09-04-capital-calls.md) — Capital calls
- [09-05](09-05-investment-and-construction-counterparty.md) — Investment, and who receives the money
- [09-06](09-06-labour-reallocation.md) — Labour reallocation, and the empty pool

### E10 — Metrics and output
- [10-01](10-01-metrics-writer.md) — The metrics writer
- [10-02](10-02-price-indices.md) — CPI, sector indices, and realised budget shares
- [10-03](10-03-abstainer-cohort-decomposition.md) — The headline, decomposed
- [10-04](10-04-distribution-and-wealth.md) — Distribution and wealth
- [10-05](10-05-required-diagnostics.md) — The diagnostics the reviews made mandatory

### E11 — Validation gates
- [11-01](11-01-balance-sheet-gate.md) — The balance-sheet gate
- [11-02](11-02-warmup-transient-gate.md) — The warm-up transient gate
- [11-03](11-03-null-run-gate.md) — **V4**: the null run
- [11-04](11-04-neutrality-gate.md) — **V3**: money neutrality

### E12 — Scenarios and the campaign
- [12-01](12-01-scenario-definition.md) — Defining a scenario
- [12-02](12-02-campaign-runner.md) — The runner, one process per seed
- [12-03](12-03-paired-seed-comparison.md) — Paired-seed comparison
- [12-04](12-04-sweeps.md) — The sweeps, and the per-good financeability switch

---

## Story file convention

Each file carries: a title; `Epic` / `Milestone` / `Depends on` / `New ground` header lines; the
story in as-a/I-want/so-that form; acceptance criteria as checkboxes; a **prose** implementation
hint with no code; and a **How to verify** block giving the command and what to look for.

No story depends on a story from a later milestone. That property is mechanically checkable from the
headers and should stay true — a violation means either the dependency is not real or the milestone
boundary is in the wrong place.

Hints are deliberately prose. A story should survive the implementation changing under it, and
a story that contains the answer stops being a specification of *behaviour* and becomes a diff
waiting to rot.
