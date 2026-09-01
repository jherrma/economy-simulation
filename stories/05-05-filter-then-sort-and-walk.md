# Filter before sorting, then the descending walk

**Epic:** E5 — The purchase decision
**Depends on:** 05-04
**New ground:** The rule itself, and the optimisation the benchmark identified

## Story

As the model author, I want candidates filtered by λ before being sorted, then walked in descending order, so that the model buys what §6.2 says it buys, without sorting units it will never look at.

## Acceptance criteria

- [ ] Candidates with `score ≤ λ` are discarded **before** sorting; only survivors are ordered.
- [ ] The walk proceeds in descending `score`, applying the affordability test: cash now, or an instalment over the term that passes the bank's standards (§5.3).
- [ ] The budget depletes **sequentially** — each purchase changes what is affordable next.
- [ ] When affordability fails and the unit is financeable, the household takes credit with probability rising in `θ` and in the gap between desired and affordable consumption.
- [ ] Abstainers (`θ = 0`) never finance. A test asserts no abstainer ever originates a loan in any scenario.
- [ ] The result is identical to a naive full-sort implementation, proven by a differential test against one.
- [ ] No allocation in the loop.

## Where to start

The filter-then-sort ordering comes from measurement rather than instinct: the sort is 67% of the
benchmark, and since the walk breaks at λ, every candidate below λ is sorted for nothing. Keep the naive
full-sort version around as a test oracle — it is the only place in this project where a reference
implementation is available, and a differential test between the two is worth more than any unit test of
the optimised path.

The sequential budget is what makes this loop unvectorisable, and it is not an accident of the
formulation: it is how spending actually works. Do not be tempted to precompute affordability for all
candidates at once.

The abstainer assertion belongs here rather than in E7 because this is where the decision is made. It is
the load-bearing property of the primary experiment — if an abstainer ever borrows, the headline result
is measuring something else.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~PurchaseWalkTests
```

The differential test against the naive implementation, over many seeds. Any divergence means the
filter is dropping something the walk would have reached.
