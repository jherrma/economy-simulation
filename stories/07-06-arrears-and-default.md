# Arrears, forbearance, and the choice to default

**Epic:** E7 — Credit
**Depends on:** 07-01, 06-03
**New ground:** Default as a decision, split from default as a circumstance

## Story

As the model author, I want households that cannot meet everything to choose between debt service and consumption, so that strategic and involuntary default are distinguishable, which is what D11 requires.

## Acceptance criteria

- [ ] At step 7 each household resolves obligations against plan; if both cannot be met, `π` versus pressure decides whether debt service is paid or consumption is protected.
- [ ] The classification — **strategic versus involuntary** — is produced here, and only here, because it is the one point where both quantities are known.
- [ ] The squeeze order (§6.7) determines what is cut; **step 3 is cutting food toward subsistence**, and the material-deprivation proxy counts households at step 3 **or beyond**.
- [ ] Breaking a time deposit sits after selling shares and before default in that order (§6.6).
- [ ] `forbearance_ticks` missed payments are tolerated before repossession.
- [ ] The four overextension routes of §6.8 are counted separately: stacking, income fall, price rise, no buffer.
- [ ] Route 1 (stacking) depends on the myopic affordability horizon of §6.2. A test asserts it all but closes under `affordability_horizon = full_term`, which is the control that shows how much of the model's default rate is a foresight assumption.
- [ ] **Route 2 runs entirely through demotion**, since there is no unemployment — so default rates are conservative and cannot be compared with observed data. A test asserts no other income-loss path exists.

## Where to start

The strategic/involuntary split is why the tick has a separate arbitrate phase at all. Saving is a
*residual claim* here — whatever survives this step — rather than a pre-commitment, and that ordering was
itself a fix: an earlier draft set money aside at one step and cut it to zero at the next.

Be careful with the off-by-one in the deprivation proxy. Cutting food *is* step 3, so 'below step 3'
excludes exactly the households the metric is meant to count.

Record the conservatism prominently in whatever this writes out. Demotion is a real income shock and a
sufficient one, but it is milder than job loss, and any default figure this model produces has to carry
that caveat wherever it is quoted.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~DefaultTests
```

Both classifications occurring, all four routes non-zero across a stressed scenario, and no
income-loss path other than demotion.
