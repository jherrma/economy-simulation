# Wants, and ageing

**Epic:** E4 — The decision
**Depends on:** 02-04, 03-04
**New ground:** One unit per category, and the replacement cycle

## Story

As the model author, I want a household to want one unit of a category exactly when its current one has expired, so that durables are lumpy and the replacement cycle is a consequence of `life` rather than a schedule.

## Acceptance criteria

- [ ] A category is wanted if `life_g = 1`, or if `age_h,g ≥ life_g`. Otherwise not wanted.
- [ ] **At most one unit of a category per household per tick.** Extra income goes into quality, never into quantity.
- [ ] Ageing runs after the walk, so a unit bought this tick starts at 0 and is not wanted again next tick.
- [ ] A household that wants a durable and does not obtain it **keeps wanting it** every tick until it does. It does not skip a cycle.
- [ ] `wait_h,g` — ticks since the want first appeared — is tracked and reset on purchase. This is a required metric (07-03).
- [ ] A test runs 360 ticks with no credit and asserts the aggregate replacement demand per category is flat, not clustered.
- [ ] A test asserts total unit demand per category per tick converges to `households / life_g`.

## Where to start

The persistent want is what makes `wait` mean something. A household priced out of a phone this tick
is not out of the market; it is still in it next tick, and the number of ticks it stays there is the
cleanest statement the model can make about the timing channel. Implementing "wants" as a fresh draw
each tick would lose that entirely.

The flatness test is the check on 02-04's uniform initial ages, seen from the other end. If ages
were initialised badly the demand profile clusters, and it will be much more obvious here — in a
360-tick aggregate — than in the initialisation test.

The one-unit rule is a real limitation and it belongs in the write-up, not just the code: this model
cannot represent buying *more*, only buying *better*. That is why the tier ladder had to exist.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~WantsTests
```

The flat replacement profile over 360 ticks, and a household denied a purchase still wanting it on
the following tick with `wait` incremented.
