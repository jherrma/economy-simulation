# Money neutrality — **V3**

**Epic:** E11 — Validation gates
**Milestone:** M1
**Depends on:** 11-03
**New ground:** The check that catches a nominal illusion in a decision rule

## Story

As the person who will defend these results, I want every nominal quantity scaled simultaneously with nothing real changing, so that a nominal illusion hidden in a decision rule is caught by a test rather than by a reviewer.

## Acceptance criteria

- [ ] Scale `M0`, all cash, all deposits, all loan principals, bank equity, wages and posted prices by λ **simultaneously**.
- [ ] **Nothing real may change**: real consumption, quantities bought, tenure, defaults and every ratio must be identical.
- [ ] Scaling balances alone is **not** a valid test — `M0` is a fixed constant, so scaling cash without it drives `reserves = M0 − cash` negative, and scaling deposits without loan principals silently alters every real debt burden.
- [ ] `joy_g`, `status_scale` and `w_g` scale with the price level, per 05-01's indexation; `λ`, `σ`, `θ`, `φ` and every ratio do **not**.
- [ ] A failure names which quantity diverged, since the diagnostic value is in locating the illusion.
- [ ] The gate runs at several values of λ, including one below 1.

## Where to start

This is the gate that would have caught the largest defect in the specification's history. With `joy` in
utils against a cost in euros, doubling every price halved every score while the reservation threshold did
not move, so demand collapsed — a nominal illusion sitting inside the core decision rule, contradicting
the model's own claim that the money stock anchors the price level. 05-01 fixed the units; this proves
it stayed fixed.

The list of what scales and what does not is the whole test, and getting it wrong produces a failure that
looks like a bug in the model rather than in the test. Derive it from the units: anything denominated in
euros scales, anything dimensionless does not.

Include a λ below 1. Deflation exposes different rounding behaviour than inflation, and `Money` is
integer cents.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~NeutralityTests
```

Bit-identical real quantities at every λ. 'Close' is a failure — a real quantity that drifts slightly
under scaling means something is not homogeneous and it will drift over 480 ticks.
