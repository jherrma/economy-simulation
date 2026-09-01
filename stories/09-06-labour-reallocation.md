# Labour reallocation, and the empty pool

**Epic:** E9 — Firms
**Depends on:** 09-01, 04-03
**New ground:** Full employment as an identity rather than an aspiration

## Story

As the model author, I want firms releasing and claiming positions through a pool that must end each tick empty, so that every household holds exactly one position at all times and demotion is well defined.

## Acceptance criteria

- [ ] Each firm computes a desired headcount from capacity and utilisation.
- [ ] Firms wanting fewer positions **release their lowest-tier positions first**, into a pool.
- [ ] Firms wanting more **claim** from the pool in descending order of unfilled desire; ties broken by the tick's seeded shuffle, never by firm index.
- [ ] **The pool must be empty at the end of the step.** If desired total headcount falls short of `n_households`, residual households are assigned to the firms with the largest gap, at `basic` tier.
- [ ] Capacity investment raises a firm's *desired* headcount and its claim on the pool — it **cannot** add positions to the town, whose total is pinned for the whole run.
- [ ] A test asserts, every tick of a full run, that the pool is empty and every household is employed.
- [ ] Demotions are counted, since they drive route 2 of §6.8 and feed `φ` (§6.6).

## Where to start

'Hire or fire' under a fixed labour force is not self-explanatory, and this was one of five places an
implementer would have had to invent a mechanism. The residual-assignment rule in criterion four is what
makes full employment hold as an identity; without it the pool can end non-empty and the model quietly
acquires unemployment it claims not to have.

Be honest in the code comment about what this is: the labour market does not clear on a wage here, it
clears by assumption, and the wage adjusts only through the annual profit review. That is a defensible
simplification under D20 but it should not be disguised as a market.

Tie-breaking by firm index is the tempting shortcut and it is a determinism trap of the same family as
the credit queue: firm 0 would always win, which is a systematic advantage no parameter records.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LabourReallocationTests
```

The pool empty on all 480 ticks, and demotion counts non-zero in a contracting scenario — if they
are always zero, route 2 of §6.8 is unreachable and defaults will be understated.
