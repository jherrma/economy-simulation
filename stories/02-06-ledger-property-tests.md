# Property tests: random transfer sequences

**Epic:** E2 — The ledger
**Depends on:** 02-05
**New ground:** Fuzzing the ledger, since there is no external oracle for it

## Story

As a maintainer, I want thousands of random sequences of ledger operations checked against the invariants, so that the ledger is trusted because it survived adversarial input rather than because the examples I thought of passed.

## Acceptance criteria

- [ ] A generator produces random sequences of: payments, cash deposits and withdrawals, loan grants, repayments, interest accrual and profit distribution.
- [ ] After **every** operation, V1 and the inside-claims identity both hold.
- [ ] Amounts include the adversarial cases: zero, one cent, an agent's entire balance, and amounts one cent beyond it.
- [ ] Failing cases are **shrunk** to a minimal reproducer and printed with the seed that produced them.
- [ ] The test is seeded and reproducible — a failure that cannot be replayed is a rumour.
- [ ] It runs in CI-reasonable time; the exhaustive version is a separate, slower category.

## Where to start

There is no reference implementation to diff a ledger against, so property testing is doing the job
that `git` does for a git clone and `redis-cli` does for a redis clone. The invariants are the oracle.

Weight the generator toward the cases that actually break ledgers: repeated small amounts that expose
rounding, sequences that drive a balance to exactly zero, and pro-rata distributions across many
holders — that last one is where 01-02's remainder rule gets its real test, because the property here
is stronger than the one in 01-02 and runs against the live ledger.

Shrinking is worth the effort. A 400-operation counterexample tells you nothing; the same failure
shrunk to three operations usually tells you everything.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~LedgerPropertyTests
```

Green, and the reported case count. If it is finding nothing after thousands of runs, look at the
generator — it is probably producing amounts too tidy to expose rounding.
