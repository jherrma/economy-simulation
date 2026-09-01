# The goods table

**Epic:** E4 — The world, and an empty tick
**Milestone:** M0
**Depends on:** 03-01
**New ground:** The twelve sectors, and the elasticity × financeability grid as data

## Story

As the model author, I want the §4 goods table as configuration, with the experimental grid derivable from it, so that the 2×2 grid the experiment rests on comes from numbers rather than from adjectives.

## Acceptance criteria

- [ ] Twelve sectors, each with durability, status weight `w_g`, financeability, loan term, `elasticity_g` and running cost, per §4 and §13.4.
- [ ] Grid membership is **computed** from numeric `elasticity_g`, never stored as a label — the words high/medium/low could not produce the grid and were the original defect.
- [ ] **No enum, no `switch` on sector** (S7). Housing's depreciation exception is a column, not a branch. A test adds a synthetic thirteenth sector to the table and asserts the engine runs it without a code change.
- [ ] Leisure/holidays is present and lands in elastic-financeable. It is the only financeable good with durability 1, which is why §1.3 calls its price series the most informative in the model.
- [ ] The **rental firm** and the **public transport operator** are sectors like any other, with headcount, capital and owners.
- [ ] `financeable_g` is a **per-good switch**, toggleable one good at a time for the C2a experiment (§1.3).
- [ ] Housing is flagged as the exception to generic depreciation: it uses `housing_depreciation_rate`, not `price / durability` (§6.5).
- [ ] A test reproduces §4's 2×2 grid from the table and compares it against the expected membership.

## Where to start

The grid is the experiment, so it must be a consequence of the data rather than a table someone typed
alongside it. When the two were maintained separately they disagreed — restaurants and furniture were
both 'medium' and sat in opposite cells — and the disagreement went unnoticed through a full review.

Housing's depreciation exception is small and worth doing here rather than in E5, where it will look
like a special case someone added to make a number come out. §4 is explicit that its 360 ticks are an
ownership horizon, not the physical life of the building.

The per-good financeability switch is not a debugging aid; it is the identified experiment that replaced
the confounded pooled ratio. Build it as a first-class part of the table.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~GoodsTableTests
```

The reconstructed grid matching §4, and leisure/holidays appearing in the elastic-financeable cell.
