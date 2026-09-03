# The opening state and the money identity

**Epic:** E3 — The ledger and the opening state
**Depends on:** 02-04, 03-02
**New ground:** `M0` as a derived quantity, asserted before the first tick

## Story

As the model author, I want the opening balance sheet built and checked before anything runs, so that `M0` is a consequence of the configuration rather than a number someone typed.

## Acceptance criteria

- [ ] `cash_h = income_h · opening_cash_share`, and `pool = pool_months × Σ income_h`.
- [ ] **`M0 = Σ cash_h + pool`, computed, never configured.** A test asserts it matches the specification's 8,450,000 € at default parameters.
- [ ] `loans_outstanding = 0` at `t = 0`.
- [ ] Opening tier prices are `price_ref_g · price_mult_t`. There are eighteen of them and none is tuned.
- [ ] Stock is reset to `units(g,t)` at the start of every tick — it does not carry over.
- [ ] A loader assertion refuses to start if `pool` is less than one tick of total income.
- [ ] V1 holds at `t = 0` trivially, and the check runs before the first tick rather than after it.

## Where to start

The opening prices are deliberately **not** an equilibrium, and this story is where that is written
down rather than discovered. At `t = 0` the premium tiers sit in heavy surplus because supply is
40/40/20 while most households want budget or standard. The warm-up exists so relative prices can
find the mix, and the pool carries twelve months of income so the transient is survivable.

The temptation will be to tune the opening prices so the market clears at tick 1 and the warm-up can
be short. Resist it: the realised tier mix is the output the whole experiment turns on, and choosing
opening prices to produce a particular mix is choosing the answer.

Stock resetting each tick rather than accumulating is what "fixed supply per tick" means. Unsold
premium units do not pile up into a glut; they are simply production that was not taken.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~OpeningStateTests
```

`M0` matching to the cent, and the assertion firing when the pool is configured too small.
