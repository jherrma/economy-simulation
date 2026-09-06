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

> **Built 2026-09-06.** Four things worth recording, three of them measurements the story could not
> have predicted.
>
> **The thin-shelf floor is a parameter, off by default, and that is a statement about §3.1.** The v1
> calibration at 1,000 households has an appliance premium shelf of *two* units and an electronics
> premium shelf of six, so a floor turned on by default would reject the configuration every
> published v1 number came from. It is `run.min_shelf_units`; §3.6's table sets it to 7. The floor
> forces the town rather than the other way round — the threshold is **4,680** households and 5,000
> is the nearest round thousand above it, so the binding shelf has no margin at all.
>
> **The warm-up more than tripled, from 240 to 840, and one good sets it.** `eating_out` has the
> highest entry income in the table, so almost nobody wants it at the opening price and its budget
> shelf takes about seven hundred ticks to fall to where demand meets it — *systematically*, with a
> seed spread of 0.7% around a mean drift of −5.3% at tick 300. The general lesson is worth more than
> the number: **the model's convergence time is set by its most marginal good, and splitting a
> category manufactures marginal goods.**
>
> **The power got better, not worse.** The story expected worse. The abstainer headline resolves at
> *t* = −59.5 against v1's −23.8, because five thousand households cut the paired spread from 1.29%
> to 0.39% — the bigger town buys more than it costs.
>
> **The finding, and it is the epic's reason to exist.** The harm is graded by lump size: −58% on a
> €1,440 washing machine, −28% on a €600 phone, nothing measurable on €216 hobby equipment, and
> *positive* on everything cheap and frequent. And outerwear is the control the calibration
> accidentally provided — €576 every two years, not financeable, and the abstainer gets **16% more**
> of it while getting 28% fewer phones. It is not that expensive goods become hard to get; it is that
> **financeable** goods do. §10.6 carries it, with the caveat that one row is carrying a lot of the
> argument.

## How to verify

```sh
dotnet run --project tools/Gates -- nullrun
dotnet run --project tools/Gates -- pilot
dotnet run --project tools/Campaign -- --all
```

No shelf under the floor; V4 stationary on the grouped calibration; every paired difference reported next to its detectable floor.
