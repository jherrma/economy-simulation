# Re-sizing the town: households, warm-up and the thin-shelf check

**Epic:** E11 — Product groups and replacement cycles
**Depends on:** 11-04, 08-03, 09-02
**New ground:** A shelf-thickness check, and a warm-up set on evidence for the second time

## Story

As the model author, I want a check that no shelf is too thin to carry a price series, and a warm-up measured rather than inherited, so that fifty-four repricing shelves produce a signal instead of noise.

## Acceptance criteria

- [ ] A **thin-shelf check** at load: report every `(good, tier)` whose opening unit count is below a stated floor, and fail the grouped calibration if any is. At `households = 1000` four premium shelves are at three units or fewer and one is at **one**; at 5,000 none is under seven. The counts come from `Allocation.LargestRemainder`, not from `round(share × capacity)` — the two disagree (a capacity of 19 splits 8/7/4, not 8/8/3).
- [ ] `households = 5000` for the grouped calibration, set by that check rather than by preference, and `capacity` follows the identity without any number being restated by hand.
- [ ] `warmup_ticks` is **measured on the null run** (V4) under the grouped calibration and the result written into `02-PARAMETERS.md` §3.6 with its evidence. §10.3 found relative prices converge eight times slower than the level at eighteen shelves; the figure for fifty-four is not to be guessed, and the current 240 is not to be assumed to carry over.
- [ ] V4 passes under the grouped calibration: the creditless baseline is stationary over the measured window, tested on `wait_median_met` and not on `wait_median` (§10.1).
- [ ] `tools/Gates pilot` is run under the grouped calibration and each measure's paired difference reported against what 30 seeds resolve.
- [ ] The campaign runs end to end on the grouped calibration and the wall-clock time is recorded in the README beside the v1 figure.
- [ ] A dated finding in `01-SIMULATION.md` §10 reports what the grouped calibration showed — **whatever it shows** — and `Notes.md` gets its block.

## Where to start

The thin-shelf check is cheap and it is the story's reason to exist. A shelf of one unit produces a price series that looks like data and is a coin toss, and it will be read as a finding by whoever plots it next year. Make the check name every offending shelf rather than merely failing, so the fix — more households, or a group merged back — is obvious from the message.

Everything else here is measurement, and the order matters. The warm-up has to be set before V4 can pass, V4 has to pass before the pilot probe means anything, and the probe has to be read before any difference between arms is quoted. Doing them out of order produces numbers at every step, which is the danger: none of them is wrong-looking.

Expect the power to be worse than §10.4's. This epic adds dispersion in replacement timing on top of §5.4's dispersion in taste, and pairing cancels the draw but not the trajectory. If the headline no longer resolves at thirty seeds, the honest responses are more seeds or a bigger town — never a finer cut of the same data, and never the difference reported without its floor beside it.

## How to verify

```sh
dotnet run --project tools/Gates -- nullrun
dotnet run --project tools/Gates -- pilot
dotnet run --project tools/Campaign -- --all
```

No shelf under the floor; V4 stationary on the grouped calibration; every paired difference reported next to its detectable floor.
