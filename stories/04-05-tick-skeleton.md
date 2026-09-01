# The 17-step tick, doing nothing, in order

**Epic:** E4 — The world, and an empty tick
**Milestone:** M0
**Depends on:** 04-02, 04-03, 04-04, 02-05
**New ground:** The plan → arbitrate → settle structure, enforced before anything fills it in

## Story

As the model author, I want the tick sequence of §6.1 as an explicit ordered structure with every step a no-op, so that later stories fill in steps rather than inventing where their logic belongs.

## Acceptance criteria

- [ ] All 17 steps exist, named, in the five phases of §6.1.
- [ ] The step list is asserted against a **committed fixture**. Milestones fill steps in; adding, removing or reordering one is a deliberate edit to that fixture with a reason attached, never a side effect of implementing something else (S1).
- [ ] **Phases 2 and 3 move no money.** A test asserts they leave every balance unchanged. Note that phase 1 *does* move money — step 3 pays wages — and so does phase 5, so "money moves only in phase 4" is false and must not be asserted; the phase-4 label means *household settlement*, not the only settlement in the tick.
- [ ] Rate-setting (step 1) uses the **previous** tick's headroom; the bank cannot see the current tick.
- [ ] Rent is set at step 4 and is due at step 5 — the rental market clears before obligations are computed.
- [ ] Interest accrual (step 15) operates on balances **after** step 8's payments; payment and accrual are separate operations on the same loan and must not both be treated as interest.
- [ ] Step 17 runs the consistency check from 02-05.
- [ ] A run of 480 empty ticks completes, V1 green throughout, and leaves the town exactly as it started.

## Where to start

Getting the order under test before there is any behaviour is the point. Every one of the ordering
constraints in the criteria was a defect found in review rather than a design intention: rent had no step
at all and §6.5 and §6.1 each assumed the other handled it; the accrual/payment distinction is the
standard double-counting error; the previous-tick headroom exists because the bank discovers its reserve
position only after the tick's transactions settle.

Make phases 2 and 3 structurally unable to move money rather than testing that they do not: a plan phase
with no access to the transfer operations of 02-02 cannot violate the rule. Be precise about which phases
those are — wages are paid at step 3, inside phase 1, and phase 5 settles capital calls, dividends,
rebalancing and interest. Only the middle two phases are quiet, and an over-broad assertion here will fail
against a correct implementation.

The 480 empty ticks are a real test, not a formality: they will catch anything that accumulates when it
should not, and they run in milliseconds.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TickSkeletonTests
```

The empty 480-tick run finishing with every balance identical to t = 0, and V1 green on all 480.
