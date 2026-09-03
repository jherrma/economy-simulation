# The balance-sheet gate

**Epic:** E11 — Validation gates
**Milestone:** M1
**Depends on:** 02-06, 10-01
**New ground:** §10's first two items as a gate on every scenario, not only on tests

## Story

As the person who will defend these results, I want V1 and the inside-claims identity enforced on every production run, so that no scenario is ever interpreted from a run that was already inconsistent.

## Acceptance criteria

- [ ] Both invariants run in production, not only under test, and abort per 02-05.
- [ ] The gate is reported as passed in the run's output, so a reader can confirm it rather than assume it.
- [ ] At `reserve_ratio = 1.0`, `M ≤ M0` is asserted — **not** `M` constant (§10 item 2).
- [ ] The identity includes firm cash.
- [ ] Performance impact is measured and stated; if it is material, the check is optimised rather than disabled.
- [ ] A campaign of scenarios reports the gate result per run, and any failure blocks aggregation.

## Where to start

The temptation at this point is to make the invariant a debug-only check because the campaign is long.
Resist it: the whole argument for writing the ledger first was that every later story is developed against
a running invariant, and that argument extends to production. If it is too slow, that is a measurement
worth having rather than a reason to switch it off.

Reporting the gate as passed matters for how results get read. 'The consistency check ran and passed on
all 480 ticks' is a sentence that belongs next to a chart.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~BalanceSheetGateTests
```

The gate result appearing in output, and a deliberately broken scenario failing to aggregate.
