# The runner, and paired-seed comparison

**Epic:** E9 — Scenarios and the campaign
**Depends on:** 09-01, 07-03, 08-04
**New ground:** Producing the number, with the pairing preserved

## Story

As the person writing this up, I want every scenario run across the same seed set and collected into one dataset keyed by scenario and seed, so that the comparison is paired and a partial run cannot enter it.

## Acceptance criteria

- [ ] One process per (scenario, seed). Five scenarios × 30 seeds = 150 runs, and the full campaign completes in minutes.
- [ ] The collector **requires the completion marker** (07-01) and refuses any run without one, naming it.
- [ ] Output is concatenated with scenario id and seed on every row, so the pairing is recoverable from the data alone.
- [ ] A test asserts the abstainer set is identical across scenarios for each seed — if it is not, the comparison is not paired and the whole campaign is void.
- [ ] Only ticks after `warmup_ticks` enter the summary, and the warm-up rows are still present in the raw files.
- [ ] The runner writes a manifest: engine commit, effective configuration hash per scenario, seed list, start and end time.
- [ ] Nothing is differenced, averaged or plotted inside the engine. The campaign produces a dataset, not a result.

## Where to start

The collector's insistence on the completion marker is the criterion that protects everything
upstream. A run that halted on V1 may still have left a plausible partial CSV, and a collector that
treats a file's existence as success will fold it into the average and produce a number that is
wrong for a reason nobody will ever find.

The pairing test is worth running on every campaign rather than once. §13-style paired comparison is
the entire statistical strategy here — the same twenty per cent of households, the same seeds, one
mechanism changed — and it fails silently if the abstainer draw ever moves.

The manifest is what makes a result citable six months later. A CSV whose engine commit and
parameters cannot be recovered is not a result.

## How to verify

```sh
dotnet run --project tools/Campaign -- --all
```

150 runs, 150 markers, one dataset, and a deliberately halted run being named and excluded rather
than absorbed.
