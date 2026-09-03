# The diagnostics the reviews made mandatory

**Epic:** E10 — Metrics and output
**Milestone:** M8
**Depends on:** 10-04, 07-03, 08-03
**New ground:** Numbers that exist to stop a result being misread

## Story

As the person who will defend these results, I want the specific diagnostics the two reviews required, emitted every run, so that known confounds are visible in the output rather than remembered from a document.

## Acceptance criteria

- [ ] **B21 — tenure mobility.** Renters crossing into ownership per scenario, the realised distribution of time-to-deposit, and the share of the `ltv_max` price effect that survives when tenure transitions are held at their low-credit rate.
- [ ] **Which constraint binds** (07-03), with all four values reachable.
- [ ] **Which side binds in the housing auction** (08-03) — credit limit versus valuation, per transaction.
- [ ] **The loader's time-deposit shift** (03-05), since it makes scenarios start from different states.
- [ ] **Realised `r_l` distribution** in warm-up (07-04), as the calibration gate.
- [ ] **Realised candidate count** per household-tick (05-07), the largest uncertainty in the performance estimate.
- [ ] **Realised replacement cycle by `θ` decile** and the financed share of replacements (§4.2) — the instrument for channel R, plus the gate that the electronics distribution spans roughly 1–5 years.
- [ ] **Shortfall ticks**, chain-failure rate, halt tick and reason.
- [ ] Each is written with a one-line note saying what it guards against, because a diagnostic whose purpose is not recorded gets dropped.

## Where to start

These are not ordinary metrics. Each one exists because a review found a way the headline could be
misread, and each would otherwise live only in `REVIEW-BACKLOG.md` where nobody looks while reading a
chart.

B21 is the largest of them. With a price level finally in place it became visible that the deposit
requirement is a hard gate on tenure change riding on a swept policy parameter: at `ltv_max = 0.80` a
basic-tier renter needs about 252 ticks of accumulation against a 240-tick measurement window, and at 0.95
about 63. So `ltv_max` is doing two jobs at once — lending standard and on/off switch for entry into
ownership — and its swept effect will read as a finding about credit standards while being substantially
a finding about tenure mobility.

The one-line notes are the durable part. In a year the numbers will still be emitted and nobody will
remember why.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DiagnosticTests
```

Every diagnostic present with its note, and the B21 time-to-deposit distribution straddling the run
length at one `ltv_max` setting and not the other.
