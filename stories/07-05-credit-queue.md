# One queue, in seeded random order

**Epic:** E7 — Credit
**Depends on:** 07-04
**New ground:** Who gets credit when capacity is scarce, and why the order must not be fixed

## Story

As the person who will defend these results, I want consumer credit and mortgages processed in a single queue in per-tick random order, so that the processing order cannot silently move the headline number.

## Acceptance criteria

- [ ] **One queue.** Consumer credit and mortgages together — not consumer first (§6.1 step 9).
- [ ] Order is a per-tick shuffle from `application_order_seed`, derived from the run seed. **Never a fixed agent order.**
- [ ] Each application is tested against current headroom, so an early grant can exhaust capacity for a later applicant.
- [ ] The rule is **swept**: fixed-order runs are compared against randomised ones to confirm the result is not a queueing artefact.
- [ ] Refusals are recorded with their reason — standards, denial window, or exhausted capacity.
- [ ] A test shows that under a binding constraint, changing only the shuffle seed changes *who* borrows but not the aggregate quantity lent.

## Where to start

Ordering the two loan types would let consumer loans systematically crowd out mortgages whenever
capacity is scarce, and since housing dominates the price result, that ordering would silently move the
headline. It is the sort of implementation detail that looks like plumbing and is actually a modelling
choice.

The final test in the criteria is the one that tells you the mechanism is right: under scarcity the
*identity* of borrowers should be seed-dependent while the *aggregate* is not. If the aggregate also
moves with the shuffle seed, capacity is being consumed inconsistently.

A fixed agent order is the natural thing to write and the wrong thing. Household 0 would get credit in
every tick of every scenario, which is a systematic advantage no parameter records.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CreditQueueTests
```

Aggregate lending stable across shuffle seeds while the borrower set changes, and the fixed-order
comparison available for the sweep.
