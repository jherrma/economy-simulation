# `score`, and λ as a reservation ratio

**Epic:** E5 — The purchase decision
**Milestone:** M1
**Depends on:** 05-01, 05-02
**New ground:** A dimensionless ranking, and saving as a price on money

## Story

As the model author, I want `score = value / user_cost` as a dimensionless number, compared against a dimensionless λ, so that the reservation test has a meaning that can be stated and checked.

## Acceptance criteria

- [ ] `score` is dimensionless, both sides being euros per tick.
- [ ] `λ = λ_base · (1 + λ_gap · max(0, (φ − buffer_months) / φ))`, per §6.2.
- [ ] **`λ_gap` defaults to 0**, so at M1 λ is constant at `λ_base` and the buffer term is inert. 06-04 turns it on at M3 (S4); the expression here is already the final one.
- [ ] `λ = 1` means the household buys anything worth at least what it costs. A test pins this interpretation.
- [ ] At or above the buffer target, `λ = λ_base`; with an empty buffer, `λ = λ_base · (1 + λ_gap)`.
- [ ] λ is **scale-free** and therefore never indexed — that is the point of having put the price level into `value` instead.
- [ ] `buffer_months` is current financial assets ÷ monthly income, computed and not stored.
- [ ] A test asserts that doubling all nominal quantities leaves both `score` and `λ` unchanged.

## Where to start

λ is where saving enters the model, and the form matters: it is not a pre-committed set-aside but a
*reservation price on money itself*. A household short of its buffer simply demands more from each euro.
That is a different mechanism from withholding a fixed fraction of income and it produces different
behaviour under stress.

The invariance test in the last criterion is the other half of 05-01's doubling test. Together they say
the ranking is homogeneous of degree zero in the price level, which is what makes V3 pass for a real
reason rather than by luck.

Keep this on the no-allocation path with the other two. It is called once per candidate per household per
tick — around 96,000 times per tick at the default town size.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ScoreTests
```

The nominal-invariance test, and λ hitting exactly `λ_base` at the buffer target rather than
approaching it.
