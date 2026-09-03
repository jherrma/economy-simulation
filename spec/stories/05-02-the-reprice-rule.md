# The reprice rule

**Epic:** E5 — Prices
**Depends on:** 05-01
**New ground:** Eighteen prices, each finding its own level

## Story

As the model author, I want each tier repriced on its own excess demand, so that the tier mix clears through relative prices rather than being fixed by the unit shares.

## Acceptance criteria

- [ ] `price ← price · (1 + k · clamp((D − units) / units, −1, +1))`, per tier, then floored at `price_floor`.
- [ ] Applied to all eighteen tiers independently. There is no category-wide adjustment.
- [ ] New prices take effect from the **next** tick. A test asserts prices are constant through a walk.
- [ ] `price_floor` **scales under the neutrality test** — a fixed floor would break V3.
- [ ] A test drives a single tier to a persistent 20% shortage and asserts the price rises about 1% per tick and converges rather than oscillating.
- [ ] A test starts premium in heavy surplus, as at `t = 0`, and asserts its price falls until households take the upgrade and the shelf clears.
- [ ] The realised tier mix is **recorded, never configured**.

## Notes from implementation (2026-09-03)

- Prices are carried as a dimensionless factor on the opening price and rounded to the cent from
  there each tick, so rounding does not compound and V3 holds to within a cent per price.
- The persistent-shortage test asserts a monotone 1% rise with no oscillation; "converges" is
  shown by the premium-surplus test, where demand responds. Premium food clears by tick 40 and
  has settled by tick 100 (seed 1).
- **The default pool does not survive the warm-up, and not because of the transient.** See
  `01-SIMULATION.md` §7.2. Two tests pin the failure. The premium-clearing test therefore runs on
  a 400-month pool.

## Where to start

The symmetry of the rule is what makes it converge. A fixed "sold out, raise by 2%" step with no
proportionality overshoots and rings; scaling the move by the size of the shortage damps it.

The reason each tier reprices separately is worth being firm about. A category-wide signal moves all
three prices together, relative prices never change, and the tier mix is frozen at whatever the unit
shares were. Since the question is precisely *how credit shifts the mix*, freezing it would answer
the question by assumption. The premium-surplus test is the one that proves the mechanism works: at
opening prices premium is 60–95% unsold, and the run is only valid if prices, not parameters, resolve
that.

Do not tune the opening prices to shorten the warm-up. The mix at `t = 0` is deliberately not an
equilibrium, and choosing prices to produce a particular starting mix is choosing the answer.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~RepriceTests
```

The convergence test on a persistent shortage, and premium clearing from its opening surplus within
the warm-up window.
