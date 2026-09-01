# The buffer target φ and its feedback into λ

**Epic:** E6 — Status, stress, saving
**Depends on:** 05-03, 06-03
**New ground:** Thrift as partly endogenous, and the second abstainer exemption

## Story

As the model author, I want `φ` responding to conditions and driving λ through the buffer gap, so that saving reduces demand and reduces the need to borrow, as §6.6's first channel requires.

## Acceptance criteria

- [ ] A household below its `φ` buffer withholds from discretionary spending through a raised λ; above it, λ returns to `λ_base`.
- [ ] `φ` rises with the deposit rate `r_d`, with `stress`, and with recent demotions in the town; it falls when status pressure is high.
- [ ] **Abstainers are exempt from this feedback too** — their `φ` stays pinned at the top of range (§1.2).
- [ ] Higher `φ` measurably lowers aggregate demand in a controlled comparison, and lowers borrowing.
- [ ] A test asserts a household with a full buffer pays cash for a good that an identical unbuffered household finances.
- [ ] The aggregate savings rate and the split between the two §6.6 channels are recorded for §9.

## Where to start

The two channels of §6.6 must not be conflated and they push in opposite directions: withheld
consumption lowers demand and prices, while the *form* the buffer takes — 06-05 — changes credit supply.
A single 'savings' number would average them into nonsense.

The cash-versus-finance test in the criteria is the mechanism in miniature and it is worth having as a
named test, because it is the sentence the eventual blog post will make: a household with a buffer pays
cash for what an unbuffered household finances, and the difference is not preference but position.

Second exemption, same reasoning as 06-03. Build both into the same guard so neither can be forgotten
independently.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~BufferTests
```

The abstainer's `φ` unmoved by a run in which everyone else's rises, and the paired
cash-versus-finance households diverging as described.
