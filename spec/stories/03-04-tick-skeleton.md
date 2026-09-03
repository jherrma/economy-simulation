# The seven-step tick, doing nothing, in order

**Epic:** E3 — The ledger and the opening state
**Depends on:** 03-02, 03-03
**New ground:** The order, under test, before anything fills it in

## Story

As the model author, I want the tick of `01-SIMULATION.md` §6 as an explicit ordered structure with every step a no-op, so that later stories fill in steps rather than inventing where their logic belongs.

## Acceptance criteria

- [ ] All seven steps exist, named, in order: income, debt service, wants, the walk, ageing, repricing, the check.
- [ ] The step list is asserted against a **committed fixture**. Adding or reordering a step is a deliberate edit to that fixture with a reason attached.
- [ ] **Debt service runs before the walk.** A test asserts a household can never spend money it owes this tick, which is what makes arrears impossible by construction in v1.
- [ ] **Ageing runs after the walk**, so a unit bought this tick starts at age 0 and is not immediately wanted again.
- [ ] **Repricing runs after the walk**, on this tick's demand, and the new prices apply from the next tick. A test asserts prices do not change mid-walk.
- [ ] Step 7 runs V1 and halts on violation.
- [ ] A run of 360 empty ticks completes, V1 green throughout, and leaves the town exactly as it started.

## Where to start

Three of the four ordering constraints above have a failure mode that produces a working run with
wrong numbers, which is why they are pinned now rather than assumed later.

Debt service before shopping is what makes the affordability test at origination sufficient: the
instalment is guaranteed to fit because it was taken out of income before anything else could claim
it. Move it after the walk and households can spend their way into arrears, which v1 has no
machinery to handle.

Prices changing only between ticks matters because the walk visits households in sequence. If a
stockout moved the price mid-walk, the household visited first would face a different price from the
one visited last, and the rationing order would silently become a price advantage.

The 360 empty ticks are a real test, not a formality: they will catch anything that accumulates when
it should not, and they run in milliseconds.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TickTests
```

The empty run finishing with every balance identical to `t = 0`, and the mid-walk price-stability
assertion.
