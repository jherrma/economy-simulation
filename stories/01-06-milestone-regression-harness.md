# The milestone regression harness

**Epic:** E1 — Foundations you cannot retrofit
**Milestone:** M0
**Depends on:** 01-05
**New ground:** **V7.** The device that makes "one dimension at a time" mean something

## Story

As the person building this in nine milestones, I want a harness that runs the engine with a named dimension switched off and compares the output byte for byte against a stored baseline, so that each milestone's contribution can be both regression-tested and *measured* rather than assumed.

## Acceptance criteria

- [ ] A command runs a stored scenario at a named milestone configuration and writes output to a directory.
- [ ] A command compares two output directories and reports the first differing byte, with the file, row and column named — not just "differs".
- [ ] Baselines are stored under version control as small files: a fixed short run (48 ticks) on a fixed small seed set, not a full campaign.
- [ ] A baseline records the **commit, the effective configuration and the engine version** that produced it. A comparison against a baseline whose configuration does not match the current schema fails loudly rather than comparing anyway.
- [ ] Regenerating a baseline is a separate, explicit command. It is never a side effect of a comparison failing.
- [ ] The harness is used at M0 against itself — an empty 48-tick run compared to its own baseline — so that the mechanism is exercised before there is any dimension to switch.
- [ ] A test proves the harness can fail: perturb one parameter by one cent and assert the comparison reports it.

## Where to start

V7 is two instruments in one body, and the second is the reason it belongs in E1 rather than being
improvised at M2.

As a **regression test** it says a new dimension disturbed nothing it had no business disturbing.
Most mistakes made at a milestone boundary show up here first and nowhere else, because the
economy is perfectly capable of absorbing a wrong number into a plausible one.

As a **measurement instrument** it is what makes the on-versus-off difference the attributed effect
of exactly one mechanism, on paired seeds, with everything else held bit-identical. That is the
sentence the write-up wants to be able to make about the status treadmill or the second-hand
market, and there is no way to make it honestly after the fact.

The harness depends on 01-05 and on nothing else, because named streams are what make "everything
else held fixed" achievable at all. Without them a new dimension shifts every draw downstream of it
and byte-identity is impossible even when the model is correct.

Keep the baseline runs deliberately small. The temptation is to baseline a realistic run; the value
is in being able to run this in seconds after every change, and a 48-tick run on four seeds catches
essentially everything a 480-tick run would.

The "prove it can fail" criterion is not ceremony. A comparison harness that silently passes
because it is reading the wrong directory is worse than none, since it will be trusted.

## How to verify

```sh
dotnet run --project tools/Regress -- baseline --milestone M0
dotnet run --project tools/Regress -- compare --milestone M0
```

The empty-run comparison passing, and the one-cent perturbation being reported with a file and a
column rather than a bare non-zero exit code.
