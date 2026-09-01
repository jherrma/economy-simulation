# The 17-step tick, doing nothing, in order

**Epic:** E4 — The world, and an empty tick
**Depends on:** 04-02, 04-03, 04-04, 02-05
**New ground:** The plan → arbitrate → settle structure, enforced before anything fills it in

## Story

As the model author, I want the tick sequence of §6.1 as an explicit ordered structure with every step a no-op, so that later stories fill in steps rather than inventing where their logic belongs.

## Acceptance criteria

- [ ] All 17 steps exist, named, in the five phases of §6.1.
- [ ] **No money moves before phase 4.** A test asserts that phases 1–3 leave every balance unchanged.
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

Make the phases structurally enforce the no-money-before-phase-4 rule if you can, rather than testing for
it. A plan phase that has no access to the transfer operations from 02-02 cannot violate it.

The 480 empty ticks are a real test, not a formality: they will catch anything that accumulates when it
should not, and they run in milliseconds.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TickSkeletonTests
```

The empty 480-tick run finishing with every balance identical to t = 0, and V1 green on all 480.
