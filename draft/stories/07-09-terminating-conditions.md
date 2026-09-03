# Bank runs and negative equity

**Epic:** E7 — Credit
**Milestone:** M5
**Depends on:** 07-03, 07-07
**New ground:** Two ways the run ends, and the halt tick as a result

## Story

As the person who will defend these results, I want the run to halt when the bank cannot serve withdrawals or its equity goes negative, so that the model reports a failure rather than continuing to produce numbers from an impossible economy.

## Acceptance criteria

- [ ] **Bank run**: realised withdrawal demand exceeding reserves halts the run (§5.3.2). `halt_on_bank_run` defaults true.
- [ ] Unmatured time deposits are **not** withdrawal demand — they are precisely the liabilities that cannot run.
- [ ] **Negative equity**: `bank_equity ≤ 0` halts the run, as the capital constraint's version of the same event. Without this the bank would stop lending forever while the simulation carried on for another two hundred ticks reporting numbers from an economy with no credit.
- [ ] There is **no recapitalisation route** — a recapitalisation would need funds the town does not have.
- [ ] **The halt tick is the result**, recorded and reported, not an error.
- [ ] Runs are **emergent, not modelled**: no panic dynamic, no contagion, no confidence variable. Withdrawal demand comes only from §6.6 behaviour.
- [ ] The output records that absence of a run proves nothing about safety — real runs are driven by the panic this model omits.

## Where to start

The negative-equity halt was missing and its absence was subtle: nothing said the bank could not fail,
`h_capital` would simply go negative, lending would stop permanently, and the run would continue to
completion producing output that looked ordinary. A silent termination that does not trigger the halt
condition is worse than a crash.

Be precise about the asymmetry in what a run means. Its *presence* is meaningful — an ordinary liquidity
failure emerging from nothing but settlement behaviour. Its *absence* proves nothing, because real runs
are psychological and this model has no psychology. Write that into the output, not only into the
documentation, because the output is what gets quoted.

Both halts must interact correctly with 02-05's invalid-output marking, or the campaign runner will
aggregate a truncated run as though it completed.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TerminationTests
```

Both conditions firing in constructed scenarios, the halt tick recorded as data, and no output file
that a collector would mistake for a completed run.
