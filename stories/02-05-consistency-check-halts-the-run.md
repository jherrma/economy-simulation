# The consistency check, and halting on violation

**Epic:** E2 — The ledger
**Depends on:** 02-03, 02-04, 01-04
**New ground:** What actually happens when an invariant fails

## Story

As the person who will defend these results, I want a violated invariant to stop the run and say why, rather than being logged and survived, so that no output file can ever come from a run that was already inconsistent.

## Acceptance criteria

- [ ] The check is step 17 of the tick (§6.1) and runs after everything else has settled.
- [ ] A violation **aborts the run** (§10). It is not a warning and it is not recoverable.
- [ ] It returns a `Result` whose failure cannot be discarded — the analyzer from 01-04 must make ignoring it a build error.
- [ ] The abort names the tick, the invariant, the discrepancy, and the run seed, so the run can be reproduced exactly.
- [ ] Any partial output already written is marked invalid, so a halted run cannot be mistaken for a completed one by whatever reads the CSV later.
- [ ] A test runs a deliberately broken model and asserts the run halts at the expected tick with a non-zero exit code.

## Where to start

This story is where the no-exceptions rule earns a hard look. A `Result` that the tick loop ignores
is strictly worse than an exception, because the run continues and produces numbers. The analyzer from
01-04 is what makes the choice safe; if it is not working, do that first.

The 'mark partial output invalid' criterion is not fussiness. The campaign runner in E12 will launch
hundreds of processes and collect their files. A halted run that leaves a plausible-looking partial CSV
behind will be silently aggregated into a result, which is the exact failure this project cannot
tolerate. Write a sentinel or a completion marker and have the collector require it.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ConsistencyHaltTests
```

Exit code non-zero, the tick named, and no file left behind that a collector would accept.
