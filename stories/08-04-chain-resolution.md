# Simultaneous settlement and the chain problem

**Epic:** E8 — Housing
**Depends on:** 08-03
**New ground:** Buying and selling in the same instant, and the one cost the spec could not bound

## Story

As the model author, I want all housing transfers to execute together, with unclosable chains failing as a unit, so that no household is momentarily homeless or doubly mortgaged, and no bridging finance is invented.

## Acceptance criteria

- [ ] A mover must sell in order to buy and buy in order to have sold; the market clears **simultaneously**, not sequentially.
- [ ] All transfers execute together. Where a chain cannot close, **the whole chain fails** and those households remain where they are.
- [ ] Chain failures are **recorded** — they are a real feature of housing markets, not an error.
- [ ] The outgoing mortgage is repaid from sale proceeds at settlement; any shortfall stays with the seller as unsecured debt.
- [ ] V1 holds across the whole simultaneous settlement, not merely before and after.
- [ ] **The cost of chain resolution is measured and reported**, because §6.5 is the one step whose complexity could not be bounded from the specification, and it is the main risk to the 95%-of-tick estimate in `LANGUAGE-CHOICE.md` §4.1.

## Where to start

Simultaneous settlement is what avoids inventing a bridging-finance mechanism the model does not have.
Treat the tick's transfers as one transaction: compute the whole assignment, verify every chain closes,
then apply. Anything that applies as it goes will leave the ledger in a state that V1 rejects halfway
through.

The measurement criterion is not routine. §4.1 of `LANGUAGE-CHOICE.md` estimates §6.2 at 95% of the tick
on the assumption that housing involves about eight movers; if chain resolution turns out superlinear,
that estimate is wrong and the whole performance argument needs revisiting. Print the cost from the
first implementation rather than discovering it during a campaign.

Chain failure being recorded rather than retried is deliberate. Retrying until something clears would
manufacture a liquidity the market does not have.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ChainResolutionTests
```

Chain failures occurring at a non-trivial rate, V1 green through settlement, and the reported
resolution cost as a share of tick time.
