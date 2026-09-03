# Relative status, and why the marginal form is required

**Epic:** E6 — Status, stress, saving
**Milestone:** M4
**Depends on:** 05-01
**New ground:** A positional channel that cannot be raised by aggregate spending

## Story

As the model author, I want status computed as a marginal gain in rank, not as a level, so that the treadmill fires at all, and aggregate status stays constant as §6.3 requires.

## Acceptance criteria

- [ ] `status_level(g,h) = status_scale(t) · w_g · (rank of h's holding within the population − 0.5)`.
- [ ] The decision uses **`status_gain`** — the level after the purchase minus the level now — not the level.
- [ ] `holding` is defined per sector: generation index for goods with obsolescence, assessed value for housing and cars, count for clothing, per-tick expenditure for services.
- [ ] `status_relative = false` makes status an absolute constant, as the control run.
- [ ] `σ` defaults to 0, and with `σ = 0` the milestone reproduces M3 **byte for byte** on the same seeds (**V7**, 01-06). The status term is an addend into 05-01's sum, not a second version of `value` (S5).
- [ ] With `status_relative = true`, **aggregate status is ≈ constant** every tick. This is an internal check §9 requires and it is the cheapest test that the mechanism is right.
- [ ] A test asserts that using the *level* instead of the gain would make status subtract from the value of every purchase a household does not already own.

## Where to start

The distinction between level and gain is not cosmetic and the failure mode is silent. §6.2 evaluates
goods the household does *not* own, and a non-owner sits at the bottom of the rank distribution, so the
level there is negative. Using it would make status reduce the appeal of every new purchase and the
treadmill would never fire — the model would run, report numbers, and have no positional channel at all.

The aggregate-constancy check is this epic's cheap invariant. Individually rational purchases produce
demand growth without anyone ending up better off; if the aggregate drifts, ranks are being computed
against a stale or partial population.

Note that `status_scale` is one of the ⚠ parameters and it decides whether this channel is decorative or
dominant. Every result involving the treadmill has to be reported across its swept range, which E12 will
need to know about.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~StatusTests
```

Aggregate status flat across a long run, and the level-versus-gain test demonstrating the sign error
that the marginal form avoids.
