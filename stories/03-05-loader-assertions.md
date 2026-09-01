# Loader assertions on reserves and bank equity

**Epic:** E3 — Configuration and opening state
**Depends on:** 03-04
**New ground:** Catching a starting state that would silently predetermine the result

## Story

As the person who will defend these results, I want the loader to reject an opening state in which a swept constraint could never bind, so that a sweep of the main dial is not a sweep of something that was never connected.

## Acceptance criteria

- [ ] `demand_deposits ≤ reserves / reserve_ratio` at t = 0, per scenario.
- [ ] Where it fails, the loader shifts household claims from demand into time deposits until it holds — and **reports how much it had to shift**, because that shift is a scenario-dependent difference in the starting state.
- [ ] `bank_equity / RWA` at t = 0 must lie within a stated band of `capital_ratio_min`. A bank starting with several times the equity it needs has no capital constraint for the length of the run.
- [ ] These replace the old test that equity merely be non-negative — a test almost nothing fails.
- [ ] `initial_loan_book_ratio` (loans ÷ `M0`) is reported and is swept, since §7.1.1 shows the full-reserve run is otherwise determined by it.
- [ ] Each failure explains which scenario it fired in and what would fix it, because these will fire during calibration and a bare assertion will simply be disabled.

## Where to start

§3.1 anticipated this exactly: an opening mortgage book several times `M0` hands the bank an enormous
residual equity and leaves the capital constraint non-binding in every scenario 'without anyone
noticing'. The whole point of these assertions is to make the noticing automatic.

Be careful with the automatic shift into time deposits. It is the right remedy — a full-reserve town
genuinely must hold its claims differently — but it means the scenarios no longer start from identical
states, which is a real confound and must appear in the output rather than in a log line nobody reads.

The equity band needs judgement rather than a hard rule: too tight and the loader rejects every
reasonable configuration during calibration, too loose and it never fires. Start wide, report the
realised ratio every run, and tighten once there is evidence of what is normal.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LoaderAssertionTests
```

That the assertions fire on a deliberately bad configuration, and that the reported shift is
non-zero for `reserve_ratio = 1.0` — if it is zero there, the check is not doing anything.
