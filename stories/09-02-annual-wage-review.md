# The annual wage review

**Epic:** E9 — Firms
**Depends on:** 09-01
**New ground:** The only live wage mechanism, and what closes the indexation gap

## Story

As the model author, I want wages reviewed every 12 ticks against the year's profit, with a lag, so that real wages do not fall monotonically under inflation and manufacture a result out of an omitted channel.

## Acceptance criteria

- [ ] Every `wage_review_period` ticks: a profitable year raises wages by `wage_increment`, scaled by profit relative to the wage bill; a loss-making year cuts by up to `wage_cut_max`.
- [ ] Wages move **once a year and they lag** — a firm raises pay on *last* year's result.
- [ ] **There is no labour-scarcity term.** Under exact full employment labour scarcity is a constant (D26).
- [ ] A test over a long inflationary run shows real wages recovering rather than falling monotonically.
- [ ] A loss-making year distributes nothing and retains nothing; the ladder of §5.2 continues from there.
- [ ] Demotion — a household moved to a lower tier — takes the corresponding income fall while its debts do not change. This is route 2 of §6.8 and the model's only income shock.

## Where to start

This story closes a specific hole. Previously wages responded only to labour scarcity, so under any
positive inflation real wages fell monotonically across 480 ticks and debt burdens rose mechanically —
manufacturing a falling-consumption result out of an omitted channel rather than out of credit. That is
the most seductive kind of bug: it produces exactly the result the author expects.

The lag is deliberate and should not be tuned away. It is how pay actually behaves, and it means real
wages can fall for a year or more before recovering, without falling forever.

Do not reintroduce a scarcity term even though `MODEL.md` mentioned one in two places before D26 deleted
it. With positions pinned to households, such a term either does nothing or does something unmotivated.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~WageReviewTests
```

Real wages over 480 inflationary ticks. A monotone decline means the review is not firing or the
profit signal is not reaching it.
