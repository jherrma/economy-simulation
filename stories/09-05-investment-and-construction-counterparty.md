# Investment, and who receives the money

**Epic:** E9 — Firms
**Milestone:** M7
**Depends on:** 09-04, 08-05
**New ground:** Closing an open money leak

## Story

As the model author, I want capacity investment paid to the construction sector, so that the money a firm spends on capacity arrives somewhere instead of leaving the economy.

## Acceptance criteria

- [ ] If utilisation stays above `target_util` for the configured run of ticks, the firm invests — retained profit first, then credit.
- [ ] **Investment is bought from the construction sector**, which is its counterparty. Money flows investing firm → construction firm → construction wages → households.
- [ ] Capacity appears `investment_lag` ticks after payment.
- [ ] Construction's own capacity constrains total investment across the town.
- [ ] V1 holds through investment. A test asserts it — an earlier draft had investment spending leave a balance sheet and arrive nowhere, which V1 would abort on at the first investment.
- [ ] Capacity built on credit must be serviced, which requires demand to persist. A test exercises the case where it does not.

## Where to start

This was an open money leak in the specification and V1 would have caught it immediately — which is a
good advertisement for writing the ledger first. Firms buy labour only, no sector produced capital goods,
so `capital_stock` denominated in euros had no seller.

Construction having two products is the fix, and it couples housing supply to industrial investment
through a shared capacity constraint. That coupling is realistic and it is worth reporting, because a
housing boom that crowds out firm investment is a mechanism the model can now show.

The last criterion is the loop back to the growth argument in the blog post: capacity built on credit
must be serviced, so a demand increase that reverses leaves debt against idle capacity. Make sure that
case is reachable rather than only describable.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~InvestmentTests
```

V1 green at the first investment — that alone proves the counterparty exists — and construction
capacity binding when several sectors invest at once.
