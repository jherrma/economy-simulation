# Time deposits, and what they mean for the reserve test

**Epic:** E6 — Status, stress, saving
**Milestone:** M3
**Depends on:** 06-05, 04-04
**New ground:** The instrument without which the decisive control run is set by its own initialisation

## Story

As the model author, I want households placing surplus on term, and those balances exempt from the reserve requirement, so that lending at full reserve is genuine intermediation rather than an artefact of the opening loan book.

## Acceptance criteria

- [ ] A household splits its buffer between demand and time deposits; the share on term rises in `τ`, in the premium `r_t − r_d`, and in the excess over `φ`.
- [ ] **Only the excess over `φ` is eligible** — the buffer proper stays liquid.
- [ ] Time deposits carry **no** reserve requirement. `deposits` in §7.1's inequality and in `h_reserve` means **demand deposits only**.
- [ ] A household may break a time deposit early, forfeiting accrued interest plus `time_deposit_break_penalty`. Breaking sits in the squeeze order after selling shares and before default.
- [ ] In the bank-run test (§5.3.2), **unmatured time deposits are not withdrawal demand**.
- [ ] A test at `reserve_ratio = 1.0` shows lending funded by time deposit formation, with V1 holding throughout.

## Where to start

§7.1.1 states the stakes plainly: without this instrument, lending at full reserve is pinned to the flow
of principal repayments, which is set by the *initialisation* of the loan book — so `high_credit_gold`,
the decisive control, would be determined by a starting value rather than by `θ`. That is a
result-predetermining choice hiding in the setup.

The four consequences in the criteria are there because two reasonable implementations would resolve them
differently, and the specification says so explicitly. Fix them here rather than discovering the ambiguity
when the gold run behaves oddly.

The accounting is worth walking through once by hand: a household moves a demand deposit to term, the
bank lends it, and the borrower spends it into someone else's demand deposit. Demand deposits end
unchanged, time deposits and loans both up. Reserves untouched. That is intermediation, and it is why the
loan book is not bounded by `M0`.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TimeDepositTests
```

Lending occurring at `reserve_ratio = 1.0` with V1 green, and `h_reserve` responding to demand
deposits only — if time deposits move it, the exemption is not implemented.
