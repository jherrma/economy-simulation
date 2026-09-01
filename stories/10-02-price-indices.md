# CPI, sector indices, and realised budget shares

**Epic:** E10 — Metrics and output
**Depends on:** 10-01, 09-01
**New ground:** The index that 05-01's indexation depends on, and shares as an output

## Story

As the model author, I want an expenditure-weighted CPI, per-sector indices, and realised budget shares by decile, so that value indexation has a price level to follow and 'how credit changed how people live' is measurable.

## Acceptance criteria

- [ ] CPI over all goods, expenditure-weighted, recorded every tick.
- [ ] The **lagged** CPI is what 05-01 indexes against — a test asserts no circular dependency within a tick.
- [ ] Per-sector price indices, and price dispersion within each sector.
- [ ] Realised budget share per category, by income decile, plus **realised minus reference share** — the §6.7 headline for how credit changed consumption patterns.
- [ ] The financeable ÷ non-financeable ratio is recorded as **mechanism check C2b only** — never labelled a primary result, since it is confounded (§1.3, D14).
- [ ] Households in each stage of the squeeze order, and postponed replacements.
- [ ] The transport share is reported knowing it is **bimodal by construction**: a public-transport household spends ~2% and a car owner ~18%, so the mean is not a household anyone resembles.

## Where to start

The circularity check matters. Prices are set at step 2 and `value` is indexed against CPI; if the
indexation reads the *current* tick's CPI, the two define each other within the tick. Lagging by one tick
resolves it and is also what §6.2 specifies.

The C2b labelling is not pedantry — it is the exact defect the first review was commissioned to fix and
the second review found still present in the metrics section, because a fix applied in one place had not
been applied where the value was actually quoted. Label it at the point of writing, so it cannot be
copied into a chart as 'the result'.

The bimodality note belongs in the output, not only here. A mean transport share averaged across the two
modes describes nobody, and someone will otherwise plot it.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~PriceIndexTests
```

The no-circularity assertion, and the C2b series carrying its label in the output header.
