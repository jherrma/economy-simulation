# Firm state and the wage hierarchy

**Epic:** E4 — The world, and an empty tick
**Milestone:** M0
**Depends on:** 03-02, 02-01
**New ground:** Positions, tiers, and the identity that every household holds exactly one job

## Story

As the model author, I want firms carrying a headcount, a three-tier wage structure and their owners, so that income inequality has the place §5.2.3 says it originates, and full employment is representable.

## Acceptance criteria

- [ ] Each firm has a sector, `capital_stock`, inventory, price, cash, deposits, loans and a share register.
- [ ] Positions are distributed across `management` / `skilled` / `basic` per §13.6, with multiples drawn per position against `base_wage`.
- [ ] Total positions across all firms equal `n_households` **exactly** — this is an identity the loader enforces, not a target it approximates (D20).
- [ ] The loader reports the scaling factor it applied to reach that total; a factor far from 1.0 means the configured ranges are inconsistent with the town's size and should be fixed rather than absorbed.
- [ ] The bank is the 79th employer, with the same tier structure (§5.3).
- [ ] `base_wage` moves **only** by the annual profit review — there is no labour-scarcity term (D26 deleted it).
- [ ] A test asserts every household holds exactly one position and every position is filled.

## Where to start

The exact-match requirement is what makes E9's reallocation rule expressible at all. If positions and
households can diverge, 'no unemployment' becomes a claim rather than a property, and the model's only
income shock — demotion — stops being well defined.

Watch the scaling factor. The illustrative ranges in an earlier draft of §13.6 produced about 255
positions against the 800 required, a factor of 3.1, while the same section said a factor far from 1.0
should be fixed rather than absorbed. The current table reaches ~796 before scaling; if your loader
reports something wildly different, the table and the code have diverged.

Do not add a labour-scarcity term to wages even though it reads naturally. Under exact full employment
labour scarcity is a constant, so such a term either does nothing or — worse — does something.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~FirmStateTests
```

The scaling factor near 1.0, and the positions-equal-households assertion.
