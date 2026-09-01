# Capital calls

**Epic:** E9 — Firms
**Depends on:** 09-03
**New ground:** A firm's loss becoming its owners' loss, with an ordering trap

## Story

As the model author, I want a firm short of money to call capital from shareholders before it borrows, so that a loss reduces owners' deposits directly rather than being financed from nowhere.

## Acceptance criteria

- [ ] The ladder is: wages fall at the annual review → **capital call** from shareholders pro rata → bank loan (§5.2).
- [ ] The call happens at **step 12**, after wages have been paid from the buffer — so a shareholder who is also the firm's employee is never asked to fund the wage he has not yet received.
- [ ] Money comes from a household or from the bank, **never from nowhere**. V1 holds through the call.
- [ ] A rule exists for shareholders who cannot cover it, and a cap on how much may be called.
- [ ] Capital calls are counted and reported per §9, alongside investment.
- [ ] **Firms do not fail** (D22) — the ladder has no last rung. A test asserts no firm ever exits.

## Where to start

The ordering trap in the second criterion is real and easy to write wrong: a firm that cannot pay wages
calls capital from shareholders' deposits, but if a shareholder is that firm's employee, his deposits are
funded by the wage the firm cannot pay. Placing the call after wages settle resolves it, and the
specification had no step for it at all before this was noticed.

The capital call is the interesting rung of the ladder — it turns a firm's loss into a direct reduction of
its owners' deposits, a real distributional channel rather than a bookkeeping fiction. Report it as such.

Removing firm failure costs something, and it is worth stating where the output will carry it: no
insolvency wave, no fire sale of productive assets, no cascade. Combined with the exclusion of
unemployment, the supply side is close to frictionless, which makes the model **conservative about
credit's harms** and unable to produce a crash.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CapitalCallTests
```

V1 green through a call, no firm exiting in any scenario, and the employee-shareholder case not
deadlocking.
