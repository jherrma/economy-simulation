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
- [ ] Postponed replacements — goods held past their intended replacement date — are counted for §9.
- [ ] A test with the switch off shows replacement driven purely by durability; with it on, replacement comes earlier.

## Where to start

Keep function and standing strictly separate. The interesting claim in the blog post this feeds is that
people replace things that still work, and the model can only show that if working and being current are
different properties.

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
