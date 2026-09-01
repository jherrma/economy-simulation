# Seeded per-agent draws, including correlated θ and φ

**Epic:** E3 — Configuration and opening state
**Depends on:** 03-02, 01-05
**New ground:** Turning ranges into a heterogeneous population, reproducibly

## Story

As the model author, I want each agent's parameters drawn from its own stream, with the stated correlations honoured, so that the population is reproducible and the abstainer cohort can be held genuinely fixed.

## Acceptance criteria

- [ ] Every `{{min, max}}` parameter is drawn per agent from that agent's own stream (01-05).
- [ ] `θ` and `φ` are drawn **jointly** with correlation `rho_theta_phi` (default −0.3), not independently.
- [ ] `rho_theta_phi = 0` is available as the control run the spec requires.
- [ ] Where §13.0 says a draw's position in its range is tied to income or wealth decile, it is — not drawn uniformly.
- [ ] The **abstainer cohort** (`abstainer_share`) has `θ` pinned at 0 and `φ` at the top of range, and is flagged so §6.4 and §6.6 feedback can skip it (§1.2).
- [ ] **Realised** means of every drawn parameter are reported per scenario, so a cohort that differs from the town in some third respect is visible rather than assumed away (B7).
- [ ] Changing `n_households` does not change the draws of the agents that already existed.

## Where to start

The joint draw is the part to get right. Drawing θ and φ independently and then reordering to fake a
correlation changes both marginals; use a proper joint method (a Gaussian copula onto the two marginals
is the conventional route) so each parameter still has the distribution §13.2 specifies.

The abstainer flag is load-bearing and easy to under-build. §1.2 requires that the cohort's θ and φ are
exempt from the endogenous feedback in §6.4 and §6.6 — that exemption is checked in E6, but the flag has
to exist here, and it must be a property of the agent rather than a rule applied at each call site.

The last criterion is a determinism trap: if agents draw sequentially from a shared stream, adding one
household shifts everyone after it. Seeding from the agent's identity avoids it, which is why 01-05 came
first.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~AgentDrawTests
```

The realised correlation of θ and φ against the configured −0.3, and both marginals still matching
their §13.2 ranges. A copula implemented carelessly gets the correlation right and the marginals wrong.
