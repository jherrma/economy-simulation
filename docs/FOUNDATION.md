# The foundation — what makes dimensions additive

The engine is built in milestones (see [`../stories/MILESTONES.md`](../stories/MILESTONES.md)).
Each milestone adds one economic dimension to a simulation that already runs. That strategy only
works if adding a dimension is genuinely *additive* — if it does not force a rewrite, does not
silently invalidate the results of every earlier milestone, and does not make the previous
answer unreproducible.

That property is not free. It rests on nine structural commitments, listed here because they are
cheap at M0 and expensive at M4. Each one is enforced by a story, and each has a failure mode that
is silent rather than loud: nothing crashes, the numbers just quietly stop being comparable.

| | Seam | Enforced by |
|---|---|---|
| **S1** | The tick has all seventeen steps from the start; milestones fill steps, never insert them | 04-05 |
| **S2** | RNG streams are named by purpose, never numbered or sliced | 01-05 |
| **S3** | Every dimension has an off switch, and *off* reproduces the previous milestone bit-for-bit | 01-06 (**V7**) |
| **S4** | Parameters introduced by a later milestone default to the earlier behaviour | 03-07, 03-02 |
| **S5** | `value` and `user_cost` are sums of gated terms, not monoliths | 05-01, 05-02, 05-08 |
| **S6** | Balance sheets are complete at M0, even where fields stay zero for six milestones | 02-01, 02-03 |
| **S7** | Goods are data; no enum, no switch on sector | 04-01 |
| **S8** | The metrics schema is additive; an M8 collector can read an M2 file | 10-01 |
| **S9** | The ledger knows account *kinds*, not agent *types* | 02-01, 02-03 |

---

## S1 — Seventeen steps from the first tick

§6.1's full tick sequence exists at M0 with almost every step a no-op. Milestones fill steps in.
**No milestone may insert a step.**

Inserting a step reorders everything after it. That does two kinds of damage. The obvious one is
that every stored result from every earlier milestone becomes incomparable, so the whole
milestone-diff method stops working. The subtler one is that ordering defects get *reintroduced*
at each milestone instead of being fixed once: rent-before-obligations, accrual-after-payment, and
previous-tick headroom were each found in review, and each would have to be rediscovered.

A test asserts the step list against a committed fixture. Adding a step is then a deliberate edit
to that fixture with a reason attached, rather than something that happens while implementing
something else.

## S2 — Streams named by purpose

A stream is derived as `H(run_seed, agent_id, purpose)`, where `purpose` is a string constant such
as `"initial_theta"` or `"credit_queue_shuffle"`. Streams are never handed out by index and never
carved as slices from one advancing generator.

**This is the load-bearing seam of the whole strategy.** If M4 adds a consumer of randomness and
that shifts the draws M1 made, then M4's run and M1's run are on different random worlds and the
difference between them is noise plus dimension, with no way to separate the two. With named
streams, M4 can add `purpose = "obsolescence_shock"` and every draw taken at M1 is bit-identical.

The concrete test: register a new stream, never consume it, and assert the run's output is
byte-identical. Also note the corollary already used at M0 — **θ and φ are drawn at initialisation
even though nothing reads θ until M2 and nothing reads φ until M3.** Drawing them late would have
shifted the initial state.

## S3 — Off reproduces the previous milestone

Every dimension arrives behind a switch, and with the switch off the engine must reproduce the
previous milestone's output **bit-for-bit on the same seeds**. This is verification device **V7**.

V7 is doing two jobs at once, and the second is the more valuable:

- As a regression test, it says the new dimension did not disturb anything it had no business
  disturbing. Most implementation mistakes at a milestone boundary show up here first.
- As a **measurement instrument**, it makes the on-versus-off difference the clean, attributed
  effect of that one dimension, on paired seeds, with everything else held exactly fixed. This is
  how the eventual write-up gets to say "the status treadmill accounts for X of the effect" with
  something behind it.

A dimension that cannot be switched off is a dimension whose contribution cannot be measured. If a
milestone's design does not admit an off switch, that is a signal to redesign the milestone, not
to skip the switch.

## S4 — New parameters default to the old behaviour

A scenario file written at M2 must still load at M7 and produce M2's numbers. Every parameter a
later milestone introduces carries a default that disables its dimension.

This is what makes S3 usable in practice rather than in principle: reproducing M2 at M7 must not
require reconstructing an M2-era config by hand. It also means the §13 appendix grows monotonically
and old fixtures never need editing.

The loader rejects unknown keys (a parameter not in §13 does not exist) but must accept *absent*
ones and fill them from defaults, recording the resolved values in the effective configuration
written beside the output.

## S5 — Sums of gated terms

`value` and `user_cost` are accumulating sums, not expressions written once and rewritten per
milestone:

```
value     = joy + mobility + σ · status_gain          (σ = 0 until M4)
user_cost = depreciation + running_cost + financing   (financing ≡ 0 until M2)
```

Each milestone contributes an addend whose coefficient is zero by default. That is what lets S3
hold for the hot loop **without a second code path**. §6.2 is the one place in this project that
cannot afford a fork: it is ~95% of the tick, it is the part under a performance gate, and a
forked version of it would mean the milestone comparison runs different code on each side, which
defeats the purpose entirely.

## S6 — Complete balance sheets at M0

Loans, time deposits, shares and dwellings exist as `Money` fields from the first tick, all zero,
six milestones before anything writes to them.

Adding a balance-sheet line later is not a local change. It means re-deriving V1, re-auditing every
transfer that could touch the new stock, and re-validating every run stored before the change. The
cost of carrying zeroes is a few unused fields; the cost of not carrying them is that the
conservation invariant — the one device that catches almost everything — has to be re-established
from scratch mid-project.

## S7 — Goods are data

Twelve sectors, each a row of numeric attributes. No enum with a `switch` on it, no
`if (sector == Housing)` scattered through the decision code. Housing's depreciation exception is a
*column*, not a branch.

The test of this seam: adding a thirteenth sector, or splitting one in two, should add rows and
touch no control flow. §4's 2×2 experimental grid is already computed from `elasticity_g` rather
than stored as labels for exactly this reason, and that decision came out of a defect where the
labels and the numbers disagreed for a full review cycle without anyone noticing.

## S8 — An additive metrics schema

Columns are added, never renamed, never reordered by meaning, never repurposed. Every row carries
the run seed, the scenario id, the milestone and a hash of the effective configuration.

The M8 campaign collector must be able to read a file written at M2. Without that, every milestone
either invalidates its predecessors' output or forces a full re-run of everything — and a full
re-run of 150 scenarios is not a thing to do casually, which means in practice the old results
would just be discarded and the milestone comparisons lost.

## S9 — Account kinds, not agent types

An account is `(owner, kind)`. V1 sums the cash and reserve *kinds* regardless of who owns them.

The invariant `M0 = public cash + firm cash + bank reserves` is then stated over kinds and does not
have to be rewritten when M6 introduces landlords as holders of cash, or when the deferred state
sector arrives with a treasury account. A conservation check written against a fixed list of agent
types has to be edited every time the cast changes, and every edit is a chance to accidentally
exclude something from the sum — which is precisely the bug the invariant exists to catch.

---

## Adding a milestone: the procedure

1. Add the milestone's parameters to §13 with defaults that disable the dimension (S4).
2. Implement it as a gated addend or a filled-in tick step (S1, S5).
3. **Run V7 with the switch off**: output must be byte-identical to the previous milestone on the
   same seeds. Do this before looking at any economics.
4. Run V1–V4 with the switch on. They stay green or the milestone is not done.
5. Run the on-versus-off comparison across the §13.1 seed set. That difference is the dimension's
   measured effect, and it is the material for that milestone's entry in `MILESTONES.md`.
6. Write the entry, including anything the milestone revealed that the specification got wrong.
