# The null run — **V4**

**Epic:** E11 — Validation gates
**Depends on:** 11-02
**New ground:** A stationary economy that must stay still

## Story

As the person who will defend these results, I want a run with credit held stationary to produce constant prices, so that any drift is identified as an artefact before any scenario is interpreted.

## Acceptance criteria

- [ ] **`credit_stationary` caps aggregate new lending each tick at that tick's principal repayments**, rationing across the §6.1 queue.
- [ ] `θ` is left at its normal low-scenario value. **The lever is not `θ = 0`** — at `θ = 0` new household lending is zero and the book amortises, which is the very artefact this test exists to avoid.
- [ ] Under the stationary condition the economy reaches a stable state with constant prices.
- [ ] Any drift is reported with its magnitude and must be explained before other results are trusted.
- [ ] A zero-lending run is explicitly **not** the null test, and a comment records why: the town starts with 240 mortgages and firm debt, and forbidding new lending would amortise that book to zero, destroying deposits and producing secular deflation that is an artefact of the test.

## Where to start

The lever is the subtle part. §10 originally asked for `θ = 0` *and* credit stationary, which cannot both
be had — §6.2 grants credit with probability rising in `θ`, so at zero the book amortises and the test
reproduces exactly the artefact it was rewritten to avoid. Imposing stationarity directly is the fix.

Rationing matters for the same reason 07-05 shuffles the queue: capping aggregate lending means choosing
who gets it, and a fixed order would introduce a systematic bias into the control run of all things.

Treat drift here as blocking. A model that does not sit still when nothing is changing will produce
differences between scenarios that are indistinguishable from the effect being measured.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~NullRunTests
```

Price level flat over 480 ticks with credit stationary. Any trend, however small, compounds over the
run and will show up as a scenario difference.
