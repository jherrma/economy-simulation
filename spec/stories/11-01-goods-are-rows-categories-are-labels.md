# Goods are rows; categories are labels

**Epic:** E11 — Product groups and replacement cycles
**Depends on:** 02-03, 07-02
**New ground:** A `category` label on each good, and output rolled up by it

## Story

As the model author, I want each good to carry a category label and the output to roll up by it, so that eighteen goods can be reported as six readable series without the engine gaining a second kind of thing.

## Acceptance criteria

- [ ] Each row of the goods table carries a `category` label alongside its name. Rows sharing a label are a category; **nothing else defines one**.
- [ ] The label is the only new field. No group type, no nesting, no second index — a product group is a goods-table row (`01-SIMULATION.md` §5.5).
- [ ] A test loads a goods table of a length that is neither six nor eighteen and runs a tick, asserting nothing reads a fixed count.
- [ ] 07-02's CPI and tier mix are emitted **per good and per category label**, and a test asserts the category series is the unit-weighted roll-up of its goods.
- [ ] Tier shares are reported **within** a good and within a category, never pooled across categories (§10.4).
- [ ] **The goods table gains replace-semantics, and this story owns it.** `ConfigurationLoader.ReadCategories` currently *merges* — a category the file does not name is kept from the basis (02-02, deliberately, so `[categories.food]` changes food alone). An eighteen-good file therefore loads as twenty-four rows with `Σ price_ref / life = 1300`. Either a stated replace form for the whole table, or a calibration basis plumbed through the loader, `Runner` and `Campaign`; `Scenario.FromToml` overlays on `SimulationParameters.Default` unconditionally today.
- [ ] A test asserts an eighteen-good calibration loads as **eighteen** rows and satisfies `Σ price_ref / life = mean_income`. That test is the one that fails today.
- [ ] `tools/Gates/PilotProbe.cs` holds the one hard-coded list of six category names; it reads them from the goods table instead.
- [ ] The default configuration is unchanged: six rows, each its own category, and V5 stays green.

## Where to start

The temptation this story exists to resist is introducing a `ProductGroup` type. It reads as the obvious modelling of the domain and it is wrong here: everything a group has — a life, a price, a capacity, a value weight, a financeable flag — is what a goods row already has, so a group type would be a second name for one thing and every later story would have to decide which of the two it meant.

Start at the output instead, because that is the only place the distinction is real. A reader wants an electronics price index, not a laptop price index, and the roll-up is where a label is genuinely needed. Everything upstream of it can stay a flat list of goods, and it should.

Watch the roll-up weighting. Unit-weighted is right for a price index over goods of wildly different lives, but state it and assert it, because value-weighted and unit-weighted diverge sharply when a €1,440 washing machine sits in the same category as a €120 kettle.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~GoodsTableTests
dotnet run --project tools/Gates -- creditoff
```

The variable-length table runs; the category series equals the roll-up of its goods; V5 still passes on the default configuration.
