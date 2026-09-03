# Milestones — one dimension at a time

The backlog is 71 stories. Built epic by epic, none of them produces a running simulation until
nearly all of them are done, and the first time anyone learns whether the model behaves is also the
last moment at which changing it is cheap.

So the build order is not the epic order. It is nine milestones, each of which **runs, produces
output, and passes its own gate**. Each adds exactly one economic dimension to the milestone before
it, behind a switch that reproduces the previous milestone when turned off
([`../docs/FOUNDATION.md`](FOUNDATION-draft.md), S3 / **V7**).

That constraint buys three things. Every milestone is a place the project can stop and still have
something. The difference between consecutive milestones is a *measurement* — the clean, attributed
effect of one dimension on paired seeds — and not merely a diff. And a specification error surfaces
at the milestone that introduces it, against a simulation that was working an hour earlier, rather
than in month four against 71 stories' worth of suspects.

| | Milestone | Adds | Stories | Cumulative |
|---|---|---|---|---|
| **M0** | A town that conserves money | The machine, no economy | 24 | 24 |
| **M1** | A town that trades | Wages, goods, prices, the purchase decision | 15 | 39 |
| **M2** | **Credit** | Loans, lending capacity, the abstainer decomposition | 10 | 49 |
| **M3** | Saving and the portfolio | The buffer, λ feedback, cash-vs-deposits | 3 | 52 |
| **M4** | The treadmill | Relative status, obsolescence, stress | 3 | 55 |
| **M5** | Credit under stress | Default, repossession, the second-hand market | 4 | 59 |
| **M6** | Housing | Rental, valuation, auction, chains | 4 | 63 |
| **M7** | The supply side answers back | Construction, wage review, capital, labour | 5 | 68 |
| **M8** | The campaign | Distribution metrics, sweeps, the published result | 3 | 71 |

**M2 is the first answer to the question the project exists to ask.** Everything after it asks
whether that answer survives contact with another dimension — which is a different, and better,
question than whether the answer can be produced at all.

Each entry below carries a line headed *deliberately wrong*. Those are known, chosen
simplifications for that stage. They are not defects and they are not to be "fixed" opportunistically;
each is scheduled at the milestone that removes it.

---

## M0 — A town that conserves money

**Adds:** everything structural and no economics at all. Value types, the ledger, the configuration
loader, the agents as data, and the seventeen-step tick with every step a no-op.

**Question it answers:** none about the economy. It answers whether the machine is trustworthy,
which is the prerequisite for every later answer being worth reading.

**Stories:** 01-01 … 01-06 · 02-01 … 02-06 · 03-01 … 03-07 · 04-01 … 04-05

**Gate:** 480 empty ticks complete; V1 green on every one; the town at t = 480 is identical to
t = 0 to the cent; V2 byte-identical serial versus parallel; V5 compile-fail tests build-fail.

**Deliberately wrong:** nothing happens. Households hold balances and buy nothing; firms hold
inventory and produce nothing. All of §6.2 is absent.

**Why this much before any economics:** these are the seams of `FOUNDATION.md`, and every one of
them is cheap now and a rewrite later. The three that would be most expensive to retrofit are the
named RNG streams (S2), the complete balance sheets (S6), and the full tick order (S1) — and all
three are invisible until the moment they are needed, at which point it is too late.

## M1 — A town that trades

**Adds:** the circular flow. Firms pay wages, households score and buy, firms price against
inventory and cost, and profit is distributed annually. `credit_enabled = false` throughout — there
is no bank lending in this milestone, and no household can spend money it does not have.

**Question it answers:** *does the calibration hold?* Are the realised budget shares close to the
§13.3 Destatis shares the joy coefficients were solved against; is the price level stationary; is
the model nominally neutral. A town that cannot sit still cannot be shown to have been disturbed.

**Stories:** 05-01 … 05-07 · 09-01 · 09-03 · 10-01 · 10-02 · 11-01 … 11-04

**Gate:** **V4** — with nothing changing, the CPI is flat to within tolerance over 480 ticks.
**V3** — scaling every nominal quantity simultaneously changes nothing real. Realised budget shares
within tolerance of §13.3. The warm-up transient has demonstrably decayed by tick 240, which is what
makes the flat CPI a claim rather than an artefact of averaging. V1 still green. **V6** — the scoring
benchmark passes its gate.

**Deliberately wrong:** no credit, no saving feedback (λ is constant at `λ_base`), no status
(σ = 0), no obsolescence, no housing market — tenure is fixed at initialisation and rent is a
constant. Wages never change. Nobody ever defaults, because nobody ever borrows.

**Worth stopping to look at:** this milestone is the control arm of the entire experiment, and it
is the one place where V4 and V3 can be established against a model simple enough that a failure has
few possible causes. If the null run drifts here, it will drift in every scenario afterwards and be
mistaken for a result.

## M2 — Credit

**Adds:** the bank lends. The loan schedule, credit standards on both sides, the three-way lending
capacity minimum, scarcity pricing, the credit queue, the financing term in `user_cost` — and the
household's myopic affordability test, which is where the monthly-payment asymmetry lives.

**Question it answers:** **the one the project was built for.** Run high credit appetite against
low, paired seeds, and read the abstainer cohort decomposition: does a household that never
borrows pay more for the same goods because its neighbours do? First number, with the T/M/N
channels separable. Channel R is not yet measurable — replacement cycles need M4's obsolescence.

**Stories:** 05-08 · 07-01 … 07-05 · 10-03 · 12-01 · 12-02 · 12-03

**Gate:** **V7** — `credit_enabled = false` reproduces M1 byte-for-byte on the same seeds. V1–V4
still green with credit on. A test asserts financing strictly *worsens* a unit's score for every good,
so credit widens the choice set without ever making anything look cheaper.

**Deliberately wrong:** no default and no repossession, so the credit channel here is the pure
demand effect with none of the losses — the answer is an **upper bound on the benefit of borrowing
and says nothing yet about its downside**. Wages do not respond, so the real-income effect on the
abstainer is also an upper bound. No second-hand market, so the cash buyer has no substitute to
escape into, which cuts the other way. All deposits are demand deposits.

**The result at this milestone is provisional and must be labelled as such** — but it is a number
produced by a running model that passes four invariants, which is a considerably better position
than the specification alone.

## M3 — Saving and the portfolio

**Adds:** thrift becomes endogenous. The buffer target φ, its feedback into λ, the cash-versus-
deposit split κ, and time deposits — which is also the point at which the reserve test starts to
distinguish between kinds of deposit and the full-reserve control run becomes meaningful.

**Question it answers:** does endogenous saving damp the M2 effect or amplify it? Households short
of a buffer demand more from each euro, which lowers demand; but the *form* the buffer takes changes
what the bank can lend. §6.6's two channels push opposite ways and this milestone measures which
wins.

**Stories:** 06-04 · 06-05 · 06-06

**Gate:** V7 against M2 with the feedback disabled and κ pinned. The two §6.6 channels are recorded
separately — a single savings-rate number averages them into nonsense.

**Deliberately wrong:** still no status, so φ's response to status pressure is inert. Still no
default.

## M4 — The treadmill

**Adds:** the positional channel. Relative status as a marginal rank gain, generations and
obsolescence, and stress.

**Question it answers:** does the social mechanism amplify the credit effect? This is where
channel **N** — purchases that would never have happened — becomes measurable, and where the
replacement cycle becomes an *output* rather than a parameter, which is the precondition for
channel **R**.

**Stories:** 06-01 · 06-02 · 06-03

**Gate:** V7 with σ = 0 and `status_relative = false`. Aggregate status is ≈ constant every tick
with the relative form on — the cheapest available check that the mechanism is the intended one.
The θ-correlated replacement cycle must **emerge** here; if it does not, channel R has been assumed
rather than found, and that must be reported rather than tuned away.

**Deliberately wrong:** still no default, still no housing.

## M5 — Credit under stress

**Adds:** the downside. Arrears, forbearance, the choice to default, repossession marked in two
steps, the second-hand market with a clearing price, and the terminating conditions.

**Question it answers:** does the second-hand market give the cash buyer an escape route, and how
much of M2's effect survives once borrowing has consequences? Channel **R** closes here.

**Stories:** 07-06 … 07-09

**Gate:** V7 with default probability at zero. The second-hand opening ask must come from the *same*
`V(age)` curve as user cost and collateral value, not a reimplementation. Repossession must not
create or destroy base money — V1 is the check.

**Deliberately wrong:** default is still conservative relative to reality, because there is no
unemployment and no firm failure (D20, D22). This is a standing limitation of the model, not of the
milestone, and it must accompany any published result: **the model is structurally conservative
about credit's harms and cannot produce a crash.**

## M6 — Housing

**Adds:** the largest single price effect and the hardest clearing problem. The rental market with
sitting tenants, valuation and the credit limit, the auction with the option to do nothing, and
simultaneous chain resolution. **Housing supply is fixed** — construction is M7.

**Question it answers:** what does the effect look like in the market where credit matters most and
supply cannot respond at all? Fixing supply here is deliberate: it isolates the pure bidding effect,
and M7 then measures how much of it new building takes back.

**Stories:** 08-01 … 08-04

**Gate:** V7 with a fixed-rent, no-ownership-transfer configuration reproducing M5. Chains settle
simultaneously or not at all, and V1 holds across a settled chain.

**Deliberately wrong:** no house-price expectations and no home-equity withdrawal — both left open
deliberately, and both would plausibly strengthen the result, which is the direction an honest model
should err in.

## M7 — The supply side answers back

**Adds:** construction, the annual wage review, capital calls, investment and its counterparty, and
labour reallocation between sectors. Everything that lets supply move.

**Question it answers:** how much of the price effect is absorbed once wages and capacity respond?
Every earlier milestone held the supply side fixed, which made the abstainer's real-income loss an
upper bound. This milestone puts a floor under it.

**Stories:** 08-05 · 09-02 · 09-04 · 09-05 · 09-06

**Gate:** V7 with wage review and reallocation disabled. The wage-price interaction must not
produce a runaway — if it does, that is a finding about the pricing rule and belongs in
`DECISIONS.md` before it is damped.

**Deliberately wrong:** still no unemployment and still no firm failure. The ladder has no last
rung.

## M8 — The campaign

**Adds:** the output surface for publication. Distribution and wealth metrics, the required
diagnostics, and the sweeps including the per-good financeability switch.

**Question it answers:** the published one — with sensitivity ranges over the seventeen unanchored
parameters of §13.10, and with the runs that contradict the hypothesis reported alongside those that
support it.

**Stories:** 10-04 · 10-05 · 12-04

**Gate:** the full 150-scenario campaign completes with a completion marker on every run; paired-seed
comparison across the §13.1 seed set; every headline number carries the milestone at which its
dimension entered, so a reader can see which parts of the result depend on which mechanism.

---

## What to do when a milestone contradicts the specification

It will happen — that is most of what running the thing is for. The rule is that the milestone
stops and the specification is corrected first, in `DECISIONS.md`, with the contradiction stated.
A parameter tuned until the milestone passes, without a recorded reason, is how a model stops being
evidence and becomes an illustration.
