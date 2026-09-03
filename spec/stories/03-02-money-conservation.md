# **V1**: money conservation, and the halt

**Epic:** E3 — The ledger and the opening state
**Depends on:** 03-01
**New ground:** The single invariant that catches almost everything

## Story

As the person who will defend these results, I want `Σ cash + pool == M0 + loans_outstanding` asserted to the cent every tick, so that an accounting bug stops the run on the tick it is made instead of becoming a plausible number.

## Acceptance criteria

- [ ] The identity is checked at the end of **every** tick, in every scenario, at both settings of `money_creation`.
- [ ] A violation **halts the run**. It does not warn, it does not log and continue, and the output it leaves carries no completion marker.
- [ ] The halt message reports the discrepancy in cents and the per-reason totals from 03-01, so the guilty operation is named rather than guessed at.
- [ ] Also asserted every tick: `pool ≥ 0`, `cash_h ≥ 0` for all `h`, `stock ≥ 0` for all eighteen tiers.
- [ ] A **negative pool halts with a different message**: this is a calibration failure, not a code failure, and conflating the two costs a day.
- [ ] With `credit_enabled = false`, `loans_outstanding == 0` at every tick.
- [ ] A deliberately broken build — interest destroyed along with principal — is used once, by hand, to confirm the check actually fires.

## Where to start

Two things make this invariant work, and both are easy to get wrong in ways nothing else notices.

**Interest is a transfer, not a destruction.** Only the principal component of an instalment reduces
the money stock; the interest goes back out to households as bank profit. Destroy it too and the
money stock drifts down under high credit, which damps the very effect being measured — a bug that
biases the answer rather than breaking it.

**Money in flight counts.** Every euro is in exactly one of two places at the end of a tick. If the
implementation grows a third — an in-transit buffer, a seller's till — it belongs on the left-hand
side of the identity, and the moment it does not, the check starts failing for a reason that has
nothing to do with the economics.

Confirming the check can fail is not ceremony. A conservation test that passes because it is summing
the wrong array is worse than none, because it will be trusted.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ConservationTests
```

Green on a 360-tick run in every scenario, and the deliberate break producing a halt with a
non-zero discrepancy.
