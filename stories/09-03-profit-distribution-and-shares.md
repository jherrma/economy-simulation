# Profit distribution and the share register

**Epic:** E9 — Firms
**Depends on:** 09-02, 04-04
**New ground:** Where profit goes, and a store of wealth that can be liquidated under pressure

## Story

As the model author, I want profit split between retention and shareholders, with shares transferable between households, so that the ownership channel of income is separable from the employment channel.

## Acceptance criteria

- [ ] **Distribution is annual, not per tick** — once every `fiscal_year_ticks` at the firm's own year end, as part of the same review that sets wages (09-02).
- [ ] Profit splits two ways: retention up to `firm_buffer_months` plus investment needs, and `shareholder_profit_share` distributed **pro rata by holding**, using 01-02's exact-sum rule.
- [ ] Year ends are staggered by `fiscal_year_offset`; a test asserts dividend income is spread across the calendar rather than concentrated in one tick in twelve. `synchronised_fiscal_year` is the swept alternative.
- [ ] Between year ends, revenue minus wages accumulates on the firm's balance sheet. A test asserts this is bounded — no firm carries undistributed profit for more than `fiscal_year_ticks`.
- [ ] **Profit does not go to employees.** Income distribution inside a firm lives in the wage hierarchy (D15).
- [ ] Valuation is `trailing_earnings × earnings_multiple` — **no endogenous share price in v1** (D16).
- [ ] Households above their buffer direct a fraction rising in `ε` into shares; sellers are households below buffer, in the squeeze order, or in arrears.
- [ ] A share purchase between households transfers deposits and leaves reserves and `M` unchanged.
- [ ] Newly issued shares move deposits from household to firm — the one route by which investment is financed without credit.
- [ ] The bank is in the same register (04-04). A test asserts bank shares reach the §9 net-worth definition.

## Where to start

Distribution being annual has a consequence worth watching rather than assuming away: between year
ends, money that would have reached households immediately now sits on firm balance sheets. That is
realistic and bounded at twelve ticks, but it withdraws base money from the bank's vault (a firm
holds `firm_cash_preference` of its buffer as cash), so it tightens credit slightly — and it makes
the capital call of 09-04 *less* frequent, since a firm short of money in month eight has not yet
paid out the year's profit.

The distributional channel here needs no price movement at all: a credit-stressed household sells its
stake to a household with surplus, transferring future profit income upward. That is worth making visible
in the metrics, because it is a mechanism the eventual write-up can state concretely.

Employees are excluded from profit deliberately and the reasoning is worth keeping in view: profit-sharing
is uncommon in Germany, so routing profit that way would be both unrealistic *and* quietly favourable to
the thesis — it would return profit to the households that spend most of it and suppress the concentration
the model exists to measure.

There is no endogenous share price on purpose. A traded equity price would be a second asset market that
credit can bid up, with its own bubble dynamics, and it would very likely dominate the housing result and
make attribution impossible. The cost is that the model can say nothing about equity-price inflation.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ProfitDistributionTests
```

Pro-rata distributions summing exactly, and share transfers leaving `M` and reserves untouched.
