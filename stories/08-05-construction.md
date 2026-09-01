# Construction

**Epic:** E8 — Housing
**Depends on:** 08-03, 09-05
**New ground:** The only way the housing stock grows, and the second product of a sector

## Story

As the model author, I want construction adding dwellings on a long lag when expected price exceeds build cost, so that supply can respond to price, slowly, as the near-zero elasticity of housing implies.

## Acceptance criteria

- [ ] Construction adds dwellings only if expected price exceeds build cost, after `investment_lag` of 24 ticks for dwellings.
- [ ] Construction is a **sector with two products**: dwellings, and productive capacity for other firms (09-05).
- [ ] Its own capacity constrains the town's total investment, which is realistic and is reported.
- [ ] Completed dwellings enter the auction as supply (08-03).
- [ ] Build cost is a wage bill, since firms buy labour only.
- [ ] **Not modelled, and biasing against the thesis:** bidders have no expectation of capital gains. Extrapolative price expectations are the canonical amplifier in credit–housing models, so omitting them makes the model conservative. The output states this rather than relying on it silently.

## Where to start

The 24-tick lag is what makes housing behave like housing. A supply response that arrives within a few
ticks would damp the price effect the experiment is trying to measure, and one that never arrives would
exaggerate it.

The two-products point matters for E9 and is easy to miss here: an earlier draft had firms investing in
capacity with the money arriving nowhere, because no sector produced capital goods. Construction is the
counterparty for both dwellings and capacity, and its capacity constraint therefore couples the two.

Write the missing-expectations caveat into the output, not just into the documentation. It is the kind of
limitation that gets lost between the model and the eventual blog post, and it cuts *against* the thesis
— which makes it the honest kind to be loud about.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ConstructionTests
```

Dwellings appearing 24 ticks after the decision, and construction capacity binding in a boom rather
than supplying unlimited building.
