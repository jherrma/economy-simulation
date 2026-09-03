# Distribution and wealth

**Epic:** E10 — Metrics and output
**Milestone:** M8
**Depends on:** 10-02
**New ground:** Real terms throughout, and three answers to one question

## Story

As a reader of the results, I want wealth, income and consumption distributions reported in real terms with their disagreements visible, so that a nominal-only report cannot obscure the finding, and no single measure can stand in for the others.

## Acceptance criteria

- [ ] **Reported in real terms throughout**, deflated by the consumption basket. Nominal wealth can rise while real consumption falls, and that divergence is close to the thesis itself.
- [ ] Net worth uses the single §9 definition and **includes bank shares** (04-04).
- [ ] Levels by decile; wealth, income and **consumption** Ginis — the last being closest to lived experience and usually the smallest.
- [ ] At-risk-of-poverty rate on the EU definition (below 60% of median equivalised income), so the number is comparable to published figures.
- [ ] Material deprivation proxy: households at squeeze-order **step 3 or beyond**.
- [ ] Movement between deciles over the run, not only the spread.
- [ ] **'Is the town better off?' answered three ways** — total real consumption per household, median, and bottom-quintile — with the disagreement between them reported as the finding when they disagree.
- [ ] Sensitivity of every wealth statistic to the housing valuation rule is reported alongside it, since housing valuation dominates the measure.

## Where to start

The three-way welfare answer is the honest core of this story. The mean, the median and the bottom
quintile diverge exactly when distribution is changing, which is the case the model is built to examine —
so reporting any one of them alone would answer the question by choosing the measure.

Real terms throughout is not a presentational preference. The thesis is about real consumption falling
while nominal quantities rise; a nominal report would show the opposite of the claim and be arithmetically
correct.

Bank shares are easy to leave out — they were left out of the specification's own net-worth definition —
and the bank is plausibly the town's most concentrated asset, so omitting it understates the wealth Gini,
which is C4.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DistributionTests
```

A constructed run where mean and median consumption move in opposite directions, and all three welfare
measures reported rather than reconciled.
