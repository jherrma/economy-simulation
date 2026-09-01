# The headline, decomposed

**Epic:** E10 — Metrics and output
**Milestone:** M2
**Depends on:** 10-02, 03-03
**New ground:** The primary result, reported as two numbers because one would mislead

## Story

As the person who will defend these results, I want the abstainer cohort's outcome split into a price-only counterfactual and an income effect, so that the headline measures the claim actually being made rather than a mixture of two opposing channels.

## Acceptance criteria

- [ ] **(a) The price-only counterfactual — the headline.** The cohort's t = 0 basket revalued at each scenario's realised prices, holding cohort nominal income on the **low-credit path**.
- [ ] **(b) The income effect**, as the residual between (a) and realised real consumption.
- [ ] Realised real consumption is reported as the sum, and **never quoted without both components**.
- [ ] Also recorded: nominal expenditure, the quantity gone without, the share of cohort income absorbed by prices set at the margin by borrowers, and cohort wealth relative to the town.
- [ ] The **channel decomposition** of §1.1 — T (timing), M (money creation), N (never-would-have), R (replacement cycle) — identified by the switch runs.
- [ ] A test asserts (a) + (b) reconstructs realised consumption exactly.
- [ ] Realised means of the cohort's drawn parameters are reported, so a cohort differing in some third respect is visible (B7).

## Where to start

The decomposition exists because the raw number is not price-only and must not be reported as if it
were. Abstainers are also wage earners, shareholders, landlords and bank employees; under D21 more credit
raises firm revenue, which raises wages a year later, and it also raises dividends and bank pay. So the
headline could come out **positive while the price claim is entirely correct** — the two channels run in
opposite directions.

(a) is the counterfactual in the author's original sentence, stated as a number: what B pays for the same
life. Holding nominal income on the low-credit path is what strips out the general-equilibrium feedback.

If (b) offsets (a), that is a finding and should be stated as one rather than buried: credit made B's
basket dearer *and* his income larger, and which dominates is an empirical question this model can
answer.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~AbstainerMetricTests
```

The reconstruction identity holding exactly, and (a) and (b) reported as separate columns rather than
netted before writing.
