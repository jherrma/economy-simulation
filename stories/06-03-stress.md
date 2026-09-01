# Stress, and its switch

**Epic:** E6 — Status, stress, saving
**Depends on:** 06-01
**New ground:** The debt → stress → consumption → debt loop, and its exemptions

## Story

As the model author, I want stress accumulating from debt service, low joy and lost rank, with decay toward zero, so that the feedback the specification calls a strong assumption is present, switchable and reported.

## Acceptance criteria

- [ ] `stress ← clamp(stress + a_dsr·DSR − b_joy·joy_consumed + c_rank·rank_drop − stress_decay·stress)`, bounded to [0,1].
- [ ] **`stress_decay` is present.** Without it stress ratchets monotonically for any indebted household, which was a defect in an earlier draft.
- [ ] High stress raises the weight on immediate joy in §6.2 and slightly raises `θ`.
- [ ] **Abstainers are exempt.** Their `θ` does not move with stress (§1.2). A test asserts an abstainer's `θ` is 0 at every tick of a stressed run.
- [ ] `stress_feedback = false` disables the whole mechanism, and any result depending on it must be reported alongside the disabled run.
- [ ] All four weights are ⚠ and act on quantities with incommensurable scales — a test asserts the swept ranges are exposed to E12.

## Where to start

The abstainer exemption is the criterion to build first and guard hardest. §1.2 claims the cohort's
behaviour is held fixed, and §6.4 raising `θ` with stress would break that claim silently: more credit
elsewhere raises prices, which raises the cohort's stress, which raises its borrowing — and the control
stops being a control at exactly the moment the effect appears.

The weights are the model's soft underbelly. They are unanchored and they combine a ratio, a euro flow
and a rank change as though those were commensurable. That is not fixable by choosing better numbers; it
is why the switch exists and why every dependent result is reported twice.

Keep the decay term. It is one line and it is the difference between a feedback and a ratchet.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~StressTests
```

The abstainer's `θ` pinned at zero through a run that stresses everyone else, and stress decaying
rather than climbing monotonically for an indebted household.
