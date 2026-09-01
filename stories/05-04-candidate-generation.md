# Candidate units, and indivisible durables

**Epic:** E5 — The purchase decision
**Milestone:** M1
**Depends on:** 05-03, 04-01
**New ground:** What a household is actually choosing between

## Story

As the model author, I want the household to rank candidate *units* — one dwelling, one car, one meal — rather than goods, so that the rule determines quantity as well as selection, and firms have a real demand curve to observe.

## Acceptance criteria

- [ ] A candidate is a unit: one dwelling, one car, one phone, one haircut, one meal, one week of groceries above subsistence.
- [ ] A service offers many units; `α_g` governs how quickly successive units lose value and therefore sets quantity.
- [ ] **Durables are indivisible.** A household holds at most one unit per durable sector, so `n ∈ {{0,1}}` and `n^(−α)` is never evaluated for them.
- [ ] Dwellings are the single exception — a landlord may hold several — and those are valued as an investment in E8, not by this formula.
- [ ] Second-hand units (§5.3.1) appear as candidates alongside new ones, with reduced price, remaining life, a `secondhand_status_factor` status penalty and finance only over remaining life.
- [ ] Renting and buying a dwelling are **two candidate units for the same need**, scored identically, so rent-versus-buy falls out of the ranking and needs no separate rule.
- [ ] The candidate buffer is allocated once per household and reused.

## Where to start

The indivisibility rule replaces a claim the specification made and could not support: that
diminishing marginal value handled durables automatically because 'the second identical phone has
near-zero marginal value'. At `α = 0.9` the formula gives the second unit 54% of the first, and `α` is
listed as not applicable for durables — so the rule was undefined exactly where the text said it worked.
A small explicit special case is better than a formula that misbehaves silently.

Rent-versus-buy being two candidates rather than a separate comparison is worth respecting even though a
dedicated rule would be easier to write. Three rival housing valuations coexisted in the spec and nothing
said which governed what; collapsing them was one of the larger fixes.

Reusing the buffer is not premature optimisation — it is the condition under which the benchmark numbers
in `LANGUAGE-CHOICE.md` are achievable at all.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CandidateTests
```

That no household ever holds two of a durable, and that a rented and an owned dwelling are scored
through the same code path.
