# The auction, and the option to do nothing

**Epic:** E8 — Housing
**Milestone:** M6
**Depends on:** 08-02
**New ground:** A clearing rule that derives the price instead of defining it

## Story

As the person who will defend these results, I want bids capped by both credit and valuation, with sellers free to withdraw, so that house prices emerge rather than being a deterministic function of two policy parameters.

## Acceptance criteria

- [ ] **A bid is `min(credit_limit, own valuation)`** — never the credit limit alone.
- [ ] Sellers have a reserve price — their own valuation of staying — plus `seller_reserve_margin`, and withdraw below it.
- [ ] A household wishing to move may **stay put** if nothing clears above its valuation.
- [ ] Supply for the tick = willing movers + completed construction + repossessed stock held by the bank.
- [ ] Demand = movers + renters for whom buying beat renting in the §6.2 ranking.
- [ ] One ascending-auction clearing round per quality tier.
- [ ] **Which side binds is a reported output**: if the credit limit binds in nearly every transaction, credit really is setting house prices; if valuations bind, credit is permissive rather than causal.

## Where to start

If every bidder simply bid their maximum loan, the clearing price would *be* the second-highest credit
limit, and house prices would become a deterministic function of `dsti_max` and `ltv_max`. Since housing
dominates the price result, the model's headline finding would then be defined by two policy parameters
rather than emerging from anything. The credit limit must be a *constraint* on willingness to pay, not a
substitute for it.

The which-side-binds metric is the most informative single number this epic produces, and it is nearly
free to compute. Record it per transaction, not only in aggregate.

Sellers withdrawing matters more than it looks: without a reserve price the market clears at whatever
buyers can borrow in a bad tick, and prices ratchet down in a way no real housing market does.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~HousingAuctionTests
```

The binding-side split across a run. If credit binds in essentially 100% of transactions at every
setting, check that valuations are being computed at all.
