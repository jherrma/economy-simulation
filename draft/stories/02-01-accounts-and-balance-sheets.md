# Accounts and the agent balance sheet

**Epic:** E2 — The ledger
**Milestone:** M0
**Depends on:** 01-02, 01-04
**New ground:** The stocks every agent holds, before anything moves between them

## Story

As the model author, I want each agent to carry an explicit balance sheet in `Money`, so that every later story moves money between named places instead of inventing fields.

## Acceptance criteria

- [ ] A household holds: `cash`, `demand_deposits`, `time_deposits`, `shares`, dwellings, durables, and outstanding loans (§5.1).
- [ ] A firm holds: `cash`, `deposits`, `capital_stock`, inventory, and loans (§5.2).
- [ ] The bank holds: `reserves`, loans, demand deposits, time deposits and `equity` (§5.3).
- [ ] **Demand and time deposits are separate fields.** §7.1's reserve inequality and §7.3's `h_reserve` refer to demand deposits only; conflating them makes the full-reserve case wrong in a way no test would name.
- [ ] Every monetary field is `Money`. No field is a `double`.
- [ ] **Every field exists from M0**, zero-valued until the milestone that writes to it — loans until M2, time deposits until M3, dwellings until M6 (S6). Adding a balance-sheet line later would mean re-deriving V1 and re-validating every stored run.
- [ ] An account is `(owner, kind)`. The conservation invariant sums the cash and reserve **kinds** regardless of who owns them, so a new kind of holder does not require editing the check (S9).
- [ ] Net worth is computed in one place, per the single definition in §9, and includes bank shares — the bank sits in the same share register as any firm (§5.1.3).
- [ ] Nothing in this story moves money. Transfers are 02-02.

## Where to start

Keep these as flat, index-addressed arrays rather than an object graph — `LANGUAGE-CHOICE.md` §7
commits to that, and retrofitting it after E5 means rewriting the hot loop. An agent is an integer
index into parallel arrays, not a reference someone holds.

The separation of demand from time deposits looks like pedantry until you reach §7.1.1. Time deposits
carry no reserve requirement, which is the entire reason the full-reserve scenario can lend at all. If
they live in one field the decisive control run is wrong and nothing will tell you.

Resist adding derived quantities as stored fields. Net worth, loan-to-value and debt-service ratios are
computed, not kept — a stored copy is a second source of truth that will drift.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~BalanceSheetTests
```

Mostly shape tests at this point. The valuable one asserts that net worth is computed from the
single §9 definition, so that later stories cannot quietly introduce a second.
