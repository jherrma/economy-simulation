# `value`: joy, mobility, status, and CPI indexation

**Epic:** E5 — The purchase decision
**Depends on:** 04-01, 04-02
**New ground:** The numerator of the decision rule, in euros per tick

## Story

As the model author, I want `value` computed in euros per tick and indexed to the price level, so that money is neutral by construction and the price level is anchored by the money stock rather than by a utility scale.

## Acceptance criteria

- [ ] `value(g, n) = (joy_g(t) + mobility_value_g(t) + σ · status_gain(g, n)) · n^(−α_g)`, per §6.2.
- [ ] **Everything is euros per tick at the t = 0 price level**, indexed as `joy_g(t) = joy_g(0) · P(t)/P(0)`. There are no utils anywhere in the codebase.
- [ ] Indexation uses the **lagged, town-wide** CPI — never the good's own price, which would make every demand curve vertical and destroy the experiment.
- [ ] `w_g` is a dimensionless relative weight; `status_scale` supplies the euros and is indexed alongside `joy`.
- [ ] `mobility_value_g` is non-zero only for transport, and equals `min(capacity_g, mobility_need) · fare` — capacity beyond the household's need is worth nothing.
- [ ] A unit test doubles every nominal quantity and asserts every `value` doubles exactly.
- [ ] No allocation on this path.

## Where to start

The units are the whole story, and getting them wrong here is not a local error. In the specification's
own history `joy` was in utils against a cost in euros, which made the score a utils-per-euro quantity,
left the reservation threshold with no stateable unit, and — the real damage — pinned the real price
level to the joy scale. Doubling every price then halved every score while the threshold did not move,
so demand collapsed: a nominal illusion sitting inside the decision rule. V3 exists to catch exactly
this, and the doubling test in the criteria is its local form.

The lagged, town-wide indexation is a modelling choice worth understanding rather than copying. A
household notices that money buys less in general; it does not revalue its taste for phones in
proportion to the price of phones.

`joy_g` itself is not a constant you pick — it is solved for in 03-04's calibration against the §13.3
budget shares. This story consumes it.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ValueFunctionTests
```

The doubling test. If `value` does not scale exactly linearly, something in the chain is not indexed
and V3 will fail later for a reason that is much harder to locate.
