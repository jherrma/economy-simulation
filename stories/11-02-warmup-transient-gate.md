# The warm-up transient gate

**Epic:** E11 — Validation gates
**Milestone:** M1
**Depends on:** 11-01
**New ground:** Checking that the initialisation transient actually decayed

## Story

As the model author, I want the run to detect whether the opening transient has cleared before statistics are taken, so that the 240-tick warm-up is verified rather than assumed.

## Acceptance criteria

- [ ] The run log records whether the transient actually decayed; **if it did not, the statistics are not usable regardless of the `warmup` setting**.
- [ ] Detection is on stated criteria — stability of the price level, loan book and tenure distribution over a trailing window — not on eyeballing.
- [ ] `warmup` defaults to 240, raised from 60 because mortgages run 300 months and `initial_remaining_term` draws up to 300, so 60 ticks cannot clear it.
- [ ] A failing gate marks the run's statistics invalid without necessarily halting the run.
- [ ] The detected decay point is reported, so `warmup` can be tuned on evidence.

## Where to start

§13.1 states the requirement and then leaves it to the implementation: the log records whether the
transient decayed, and if it did not the statistics are unusable *regardless of the setting*. That is a
gate, not a note.

Choose the detection criteria before looking at any output, and write them down. A test that is calibrated
after seeing the data it is meant to judge is not a test.

The synchronised-repayment artefact is the specific thing to watch for: if 03-04's drawn remaining terms
are not working, the loan book will show a wave rather than a decay, and it will look like a credit
cycle.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~WarmupGateTests
```

The reported decay point well inside 240 ticks. If it is close to or beyond 240, raise the warm-up
rather than accepting the run.
