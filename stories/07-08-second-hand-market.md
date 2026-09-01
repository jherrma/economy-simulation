# The second-hand market, with a clearing price

**Epic:** E7 — Credit
**Depends on:** 07-07, 05-04
**New ground:** The abstainer's substitute, and a used price that can actually fall

## Story

As the model author, I want repossessed durables offered as second-hand units at a price that responds to supply, so that the procyclical channel the specification claims can emerge rather than being asserted.

## Acceptance criteria

- [ ] Used units are ranked by §6.2 exactly like new ones, with reduced price, remaining life, a `secondhand_status_factor` status penalty and finance only over remaining life.
- [ ] **The price clears, it is not fixed.** The opening ask is `V(age) · (1 − liquidation_haircut)` — the §4.3 curve at the unit's **actual age**, not a fraction of the current new price — marked down by `used_markdown_step` each tick unsold; the realised price is what a household's ranking accepts.
- [ ] A test asserts a fifteen-year-old car and a one-year-old car have **different** asks. Under the old rule they had the same one.
- [ ] A test with many repossessed cars and few buyers clears **low**; one car against many buyers clears near the ask.
- [ ] Unsold after `liquidation_ticks`, the item is written off at zero against equity.
- [ ] The claimed emergent channel is observable: repossessions → more used supply → lower used prices → lower collateral values → less lending.
- [ ] A test shows an abstainer obtaining mobility by buying used rather than borrowing.

## Where to start

An earlier draft fixed the used price at a constant fraction of the *new* price, which makes it unable
to respond to used supply at all — while the paragraph immediately after it claimed a falling-used-price
feedback. Either the price clears or the channel does not exist; there is no version where a fixed
fraction produces the claimed dynamic.

The abstainer test is the one that matters for the headline. A second-hand market is the cash buyer's
substitute: a household that will not borrow can still obtain mobility, at a lower per-tick user cost and
a status penalty. So the model can show whether credit pushes cash-paying households *down into the used
market* rather than merely making them pay more — a sharper and more concrete version of the primary
claim than a price index, and one that is directly observable in the real world.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~SecondHandTests
```

Used prices differing between the glut and scarcity scenarios. If they are identical, the price is
still pinned to the new-goods price.
