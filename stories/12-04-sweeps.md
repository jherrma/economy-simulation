# The sweeps, and the per-good financeability switch

**Epic:** E12 — Scenarios and the campaign
**Depends on:** 12-03, 10-05
**New ground:** The sensitivity programme, enumerated from the schema rather than by hand

## Story

As the person who will defend these results, I want the parameter sweeps generated from the schema, including the identified per-good experiment, so that §10's sensitivity requirement is executed rather than intended.

## Acceptance criteria

- [ ] `reserve_ratio` swept continuously from 1.00 to 0.01.
- [ ] **C2a — the per-good financeability switch**: the identical goods table run with one good's flag toggled at a time, all twelve goods. This is the identified experiment that replaced the confounded pooled ratio.
- [ ] **C2b — difference-in-differences within the elasticity grid**, so the elasticity effect cancels rather than being attributed to credit.
- [ ] ±50% sweeps over every parameter a headline result plausibly depends on, **enumerated from the schema** (03-01), not from a hand-written list.
- [ ] Every ⚠ parameter of §13.10 is swept, and any headline moving materially across its range is reported as **a finding about that parameter, not about credit**.
- [ ] `status_scale`, `k_scarcity`, `scarcity_form`, `α_g`, `λ_gap` and `h_floor` are specifically flagged in the report, being the ones most able to move a headline.
- [ ] Results contradicting the hypothesis are collected and reported alongside those supporting it.

## Where to start

Enumerating from the schema is what makes this survive. A hand-maintained sweep list drifts exactly like
the hand-maintained ⚠ list in §13.10 did, and the drift is invisible — a parameter silently stops being
swept and nobody notices until someone asks.

C2a is the reason the per-good switch was built as a first-class part of the goods table in 04-01. Toggling
one good's financeability while holding elasticity, durability, status weight and indivisibility fixed is
an identified experiment; the pooled ratio was not, and would rise on any demand increase from any source.

The last criterion is the project's own standard and the easiest to quietly drop. D7 is explicit that the
model is built so it can refute the hypothesis, and the author's own words were that the simulation may
refute him and this is fine. The runner should make publishing the contradicting runs the default rather
than a decision.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~SweepTests
```

The sweep list generated from the schema matching §13.10's ⚠ set, and all twelve C2a runs enumerating.
