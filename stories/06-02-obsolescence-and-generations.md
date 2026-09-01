# Generations and status decay

**Epic:** E6 — Status, stress, saving
**Depends on:** 06-01, 04-01
**New ground:** Goods losing standing without losing function

## Story

As the model author, I want goods subject to perceived obsolescence to carry a generation, with older generations losing rank, so that the replacement cycle is driven by standing rather than only by wear.

## Acceptance criteria

- [ ] Sectors flagged for obsolescence (§4.2) carry a generation index; new generations appear on a configured cadence.
- [ ] `status_decay` reduces an older generation's standing per tick once superseded.
- [ ] `perceived_obsolescence = false` switches the mechanism off entirely, as the control.
- [ ] A good's *function* is unaffected — only its rank. A three-year-old phone still supplies its `joy`.
- [ ] **`durability` is the functional life, not the replacement cycle.** Electronics is 60 ticks, not 24; the observed 1–5 year replacement range is an **output** of the §6.2 decision, never a drawn parameter.
- [ ] **The correlation between replacement cycle and `θ` must emerge, not be imposed.** A test asserts no household carries a drawn replacement interval. If it were an input, channel R of §1.1 would be assumed into existence rather than measured.
- [ ] Realised replacement cycle per sector **by `θ` decile** is reported, with the share of replacements that were financed.
- [ ] **Calibration gate:** the realised distribution for electronics must span roughly 1–5 years. If it does not, `status_decay` and `α_g` are miscalibrated and no treadmill result is usable.
- [ ] Postponed replacements — goods held past their intended replacement date — are counted for §9.
- [ ] A test with the switch off shows replacement driven purely by durability; with it on, replacement comes earlier.

## Where to start

Keep function and standing strictly separate. The interesting claim in the blog post this feeds is that
people replace things that still work, and the model can only show that if working and being current are
different properties.

The mechanism to keep in view: a household replaces when a new unit's `score` crosses `λ`, and
status decay is what pushes it there. A household that can finance faces the instalment test, a far
lower bar than accumulating the price, so it crosses earlier; a cash buyer waits and lands at the
long end. That is the whole of the `θ`-to-cycle correlation, and it needs no help.

`status_decay` is unanchored (§13.10), so build the switch and the sweep at the same time as the
mechanism. The comparison between on and off is the finding; the value of the parameter is not.

The cadence of new generations is worth a thought: driving it off a fixed tick count makes the treadmill
exogenous, which is honest but limits what the model can say. Note whichever you choose in `NOTES.md`
rather than leaving it implicit.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ObsolescenceTests
```

The on/off comparison. Replacement should come measurably earlier with the switch on, and postponed
replacements should be non-zero in a squeezed run.
