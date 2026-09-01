# Credit standards, for households and for firms

**Epic:** E7 — Credit
**Depends on:** 07-01
**New ground:** Two different tests, because a firm has neither wage income nor a dwelling

## Story

As the model author, I want origination tests that are defined for each kind of borrower, so that firm lending is not silently governed by rules written for households.

## Acceptance criteria

- [ ] Households: debt service ÷ income ≤ `dsti_max`, and loan ÷ collateral ≤ `ltv_max`, both at origination.
- [ ] **Firms: coverage and gearing instead** — projected annual operating surplus ÷ annual debt service ≥ `firm_icr_min`, and total debt ÷ `capital_stock` ≤ `firm_gearing_max`.
- [ ] DSTI uses the same per-tick rate convention as the schedule and the deposit credit; a mismatch between any two produces a plausible-looking wrong run.
- [ ] A borrower inside the `credit_denial_ticks` window after a default is refused outright.
- [ ] `risk_premium` is set by credit record and enters the offered rate (§7.3).
- [ ] A firm failing both tests is refused and — since it cannot fail (D22) — cuts wages further and re-runs its capital call.
- [ ] Tight and loose settings of both household parameters are exercised by tests.

## Where to start

§5.2 sent a loss-making firm to 'a bank loan at the going rate, subject to the same constraints as any
other lending', but §5.3's standards are DSTI against income and LTV against collateral, and a firm has
neither defined. Two tests for two kinds of borrower is the fix; do not try to make one pair serve both.

The rate-convention criterion looks like a restatement of 01-03 and is not: DSTI is computed from an
instalment, so it consumes the same annuity code as the schedule. If they ever diverge, loans will be
granted on one arithmetic and serviced on another, and the discrepancy will be small enough to look like
noise.

Note what refusal means for a firm. There is no insolvency; the ladder simply continues, and the pressure
reaches households through wages and capital calls. That is the model's only route from firm difficulty
to household income.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CreditStandardTests
```

That firm applications never touch DSTI or LTV, and that the denial window actually blocks.
