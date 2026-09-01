# Filter before sorting, then the descending walk

**Epic:** E5 — The purchase decision
**Milestone:** M1 (cash walk) · M2 (financed branch)
**Depends on:** 05-04
**New ground:** The rule itself, and the optimisation the benchmark identified

## Story

As the model author, I want candidates filtered by λ before being sorted, then walked in descending order, so that the model buys what §6.2 says it buys, without sorting units it will never look at.

## Acceptance criteria

- [ ] Candidates with `score ≤ λ` are discarded **before** sorting; only survivors are ordered.
- [ ] The walk proceeds in descending `score`, applying the affordability test (§6.2): a cash purchase must fit `cash + demand deposits` **now**; a financed purchase's **instalment** must fit `income − debt service already running − rent − subsistence` **this tick**, and pass the bank's standards (§5.3).
- [ ] **The horizon is one tick, not the loan term.** `affordability_horizon = myopic` is the default and the behaviour under test; `full_term` is the control. A test asserts that under `full_term`, loan stacking (§6.8 route 1) all but disappears — if it does not, the horizon is not being applied.
- [ ] The household's own test **excludes** `car_running_cost` by default (`affordability_includes_running_cost = false`). This is the §4.1 trap and it must be switchable.
- [ ] A test demonstrates the emergent asymmetry: a liquid household pays cash for a unit that an otherwise identical illiquid household finances — same rule, different binding test.
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
