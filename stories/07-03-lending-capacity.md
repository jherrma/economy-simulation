# Lending capacity: the three-way minimum

**Epic:** E7 — Credit
**Depends on:** 07-02, 06-06
**New ground:** Headroom, and the constraint that was promised and missing

## Story

As the model author, I want headroom computed as the minimum of the reserve, capital and leverage constraints, so that the constraint that binds first for a mortgage-heavy bank can actually bind.

## Acceptance criteria

- [ ] `h_reserve = 1 − demand_deposits / (reserves / reserve_ratio)` — **demand deposits only** (§7.1.1).
- [ ] `h_capital = 1 − RWA / (equity / capital_ratio_min)`.
- [ ] `h_leverage = 1 − total_assets / (equity / leverage_ratio_min)`.
- [ ] `h = min(h_reserve, h_capital, h_leverage)`. **Three terms, not two** — an earlier draft's two-term minimum made the leverage ratio unable to bind at all.
- [ ] Lending stops outright at `h ≤ 0`.
- [ ] **Which constraint binds** is recorded every tick, and all four values (reserve / capital / leverage / standards / demand) must be reachable.
- [ ] Three non-lending operations are exempt from the reserve inequality: deposit interest, bank profit credited to shareholders, and **the bank's own payroll** (§5.3).
- [ ] If those push the bank below its requirement it enters a **shortfall state** — no new lending until restored, `r_d` at maximum — and shortfall ticks are reported.

## Where to start

The leverage term is here because §7.2 promises it: it says the ratio 'frequently binds before the
risk-weighted ratio does' for a mortgage-heavy bank and 'enters the same min()'. With CRR3 mortgage risk
weights of 0.20–0.70, a 3% unweighted floor against a 7% risk-weighted one is exactly the case where the
unweighted measure bites first — which is why Basel III added it.

The payroll exemption is the kind of thing that breaks quietly. §7.1 exempted two operations; the bank
became an employer later, and at `reserve_ratio = 1.0` its wage payments breach the inequality on their
own. It is a *lending* rule, not a balance-sheet rule.

Make the which-constraint-binds metric a real enumeration and assert in tests that every value occurs in
some scenario. A value that never appears means a constraint is not wired in — which is precisely how
the missing leverage term went unnoticed.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LendingCapacityTests
```

All four binding values occurring across the scenario set, and the shortfall state being entered by
bank payroll at `reserve_ratio = 1.0` rather than breaching the inequality.
