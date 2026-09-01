# Decision log

Design decisions and their reasoning, newest last. This log exists because the project's
credibility depends on showing that the model was not tuned to produce its conclusion. Anything
that could have gone the other way is recorded here with the reason it went the way it did.

---

## 2026-09-01 — Initial design review

Seven issues were raised against the original idea before any code was written. All seven are
settled below. See `MODEL.md` for the resulting specification.

### D1 — Bank lending capacity is a continuous dial, not a set of named regimes

**Decided:** `reserve_ratio ∈ (0, 1]`, a single continuous parameter. `1.0` is full reserve
(commodity money); `0.20` heavy fractional reserve; `0.01` the ECB's actual minimum. A separate,
switchable capital constraint (Basel risk weights) runs alongside it, and the model reports
which of the two binds each tick.

**Why:** the money-creation question decides the outcome before any agent acts. If the bank only
intermediates existing savings, the money stock is fixed and the thesis comes out false; if
lending creates deposits, it comes out true by assumption. A dial that is swept, rather than an
assumption that is made, converts the question into a result.

**Recorded factual correction:** the eurozone reserve requirement is 1%, not 20% — cut from 2%
in January 2012 — and at that level it is not what limits lending. EU bank lending is limited by
capital against risk-weighted assets. Further, the textbook causation is backwards for a real
banking system: banks lend first and obtain reserves after, with the central bank supplying them
elastically (Bank of England, *Money creation in the modern economy*, 2014; Bundesbank
*Monatsbericht*, April 2017). The multiplier logic is nevertheless valid **in this model**,
because the town has one bank and no central bank, so base money is genuinely fixed. Any writing
based on the simulation must not present it as a description of the ECB.

### D2 — C2 (relative prices) is the primary claim; C1, C3, C4 are recorded but not pursued

> **Superseded in part by D14.** The pooled financeable ÷ non-financeable ratio is *not* the
> headline result and must not be quoted as one — it is confounded, and D14 replaces it with the
> abstainer cohort. What survives from D2 is its actual argument: that the **relative** price
> claim is the falsifiable one and the general level is not. Read the rest with that substitution
> in place.

**Decided:** the headline result is the price index of credit-financeable goods divided by the
index of cash-only goods. The general price level, real consumption per household, and the
distributional result are measured and written to output every run, but do not drive design
decisions and are not argued from yet. `MODEL.md` §1 records what would make each of them
interesting later.

**Why:** "prices are higher than they need to be" is not measurable as stated — a proportional
rise in all nominal values changes nothing real. The relative claim is sharper, falsifiable, and
independent of D1. C4 in particular is currently contaminated by the single-bank assumption and
would overstate itself.

### D3 — Supply elasticity is modelled explicitly, not assumed

**Decided:** rising marginal cost as capacity is approached, plus credit-financed capacity
investment. Goods are spread deliberately across a financeability × elasticity grid, with
housing as the near-fixed-stock case.

**Why:** fixed capacity turns any credit expansion into pure inflation and free capacity turns it
into pure output. Either choice would have settled the result by assumption. The grid also
generates a falsifiable prediction: the price response should be ordered housing ≫ financeable
durables > services > food, and if prices move together, C2 is refuted.

### D4 — Stock-flow consistency is enforced, and written before any behaviour

**Decided:** an assertion runs at the end of every tick — every *inside* claim nets to zero
(deposits against loans), and `M0` equals public cash **plus firm cash** plus bank reserves to the
cent. A violation aborts the run. Implemented in phase 1, before any agent does anything.

> **Corrected.** This decision originally asserted that "all financial assets equal all
> liabilities". That is **false** in this model and must not be coded: cash is *outside* money, an
> asset of the public with no offsetting liability, so the aggregate identity is
> `Σ net financial assets = M0`, not zero. The original wording also omitted firm cash from the
> `M0` identity, which firms have held since D8. `MODEL.md` §10 carries the correct forms.

**Why:** in a closed town every payment is someone's receipt. Without the invariant the model can
quietly create or destroy money, and no result can be trusted. Reference: Godley & Lavoie,
*Monetary Economics* (2007).

### D5 — Status is relative, not absolute

**Decided:** a good's status contribution depends on the household's rank in the population's
holdings of it. Zero-sum in the cross-section. Switchable to absolute as a control.

**Why:** a fixed status score is utility under another name and produces nothing. Relative status
is what creates the treadmill — one household's purchase devalues another's — and it is the
mechanism that links this model to the essay *01 Wandel des Verhältnisses der Industrie zum
Konsumenten*.

### D6 — Defaults, collateral and repossession are in from the start

**Decided:** missed payments trigger forbearance, then repossession, then write-off against bank
equity.

**Why:** without default, interest is riskless and bank profit is a trivial identity — the rate
times the loan book. With it, bank profit becomes a genuine result, and write-offs hit equity,
which under an active capital constraint directly reduces lending capacity — the mechanism that
turns a wave of defaults into a credit crunch.

> **Corrected.** An earlier wording claimed write-offs "give the capital constraint a procyclical
> feedback the reserve constraint does not have". `MODEL.md` §7.2 rebuts this by name: both
> constraints respond to defaults, by different routes. The difference between them is *which*
> quantity they limit, not whether either reacts.

### D7 — The model is built so that it can refute the hypothesis

**Decided:** every assumption strong enough to produce the expected result on its own is exposed
as a switch, and the opposing setting is always run as a control. Each headline result is swept
across ±50% of the parameters it depends on. Runs that contradict the hypothesis are published
alongside those that support it.

**Why:** an agent-based model returns whatever its parameters imply and cannot demonstrate
anything about the real economy. What it can do is show whether a mechanism is coherent and what
it depends on. The sensitivity analysis is the real output.

**Explicitly confirmed by the author, 2026-09-01:** *"the simulation may refute me and this is
fine"*. This is recorded because it is a commitment about how results will be reported, and
because the decisive control run — `high_credit_gold`, eager borrowers under a 100% reserve
requirement — is designed to be able to fail.

### D8 — Households and firms both hold cash and deposits; all starting balances are configurable

**Decided:** two forms of money. Base money `M0` is fixed for the run and held as cash by the
public or as reserves by the bank; deposits are created by lending. Households and firms both
hold both. Every opening balance is set in the scenario file and the loader asserts the stocks
sum to `M0` before tick 0.

**Why:** the reserve dial in D1 only binds if base money can actually leave the bank. With one
bank and no cash, reserves never move and the reserve requirement is vacuous. This also makes
the bank's only lever over its own reserves explicit: it cannot create base money, it can only
bid it out of the public's cash holdings.

### D9 — Saving needs two parameters, not one

**Decided:** `savings_propensity φ` (target buffer in months of income) governs how much income
is withheld from consumption. `cash_preference κ` governs what share of that buffer is held as
cash rather than deposits.

**Why:** saving affects the economy through two channels that point in opposite directions.
Withheld consumption lowers demand and prices. But a buffer held as cash drains bank reserves
and contracts credit, while the same buffer on deposit leaves lending capacity intact — so
identical saving behaviour produces opposite effects on credit supply depending only on its
form. One parameter cannot represent both. Together they also close the credit cycle: lending
grows → reserve headroom falls → deposit rate rises → `κ` falls → reserves recover.

### D10 — Budget shares are an anchor, not a constraint

**Decided:** each household is initialised with a reference share of gross income per category,
drawn from configurable per-category ranges (`MODEL.md` §5.1.1), with its position within each
range tied to its income decile so that Engel's law holds. Categories carry a rigidity class —
contractual, subsistence, habitual, lumpy — that governs how the share may move. Actual spending
is set by the purchase decision and drifts back toward the reference share at rate
`habit_inertia`. Realised-minus-reference share is a reported output.

**Why not fixed shares:** enforcing constant budget shares is Cobb-Douglas demand. Spending on a
category would remain a constant fraction of income whatever its price, so the housing *share* of
income would be constant by construction — and a rising housing share is exactly what a credit
boom is supposed to produce. Fixed shares would assume away the phenomenon the model exists to
observe, and C2 would become untestable.

**What the lumpy class buys:** a durable cannot be bought in monthly slices, so its realised share
is zero for many ticks and then large for one. An instalment converts that lump into a flow, and
a flow fits a monthly budget that a lump breaks. A budget-constrained household can therefore buy
a financed good it could not have bought outright *at the same price*. This is the micro-mechanism
behind C2, reached through the budget rather than asserted, and it is the reason the rigidity
classes exist at all.

**Calibration warnings recorded in §5.1.1:** published Destatis EVS/LWR shares are of consumption
expenditure, not gross income, so they are not directly usable as the ranges here. **Food, drink
and tobacco** is roughly 14–15% of German consumption expenditure, not the 5–10% often quoted,
which is closer to a US figure — food *alone* is nearer 11%, and the qualifier matters. The 30–50% housing range is deliberately at the high end — characteristic of cities
and lower-income households rather than the German average — which is defensible but is a choice
and must be swept.

**Superseded in part by D18.** The rigidity classes, the habit anchor and `habit_inertia` were
removed when the category budget was dropped; only `contractual` and `subsistence` still bind.
The core of this decision stands and is now stronger: budget shares are an initialisation and a
reporting reference, never a decision rule, for exactly the Cobb-Douglas reason given above.

**Flagged as influential and unanchored:** the role `habit_inertia` played is now played by `α_g`,
the diminishing-marginal-value exponent (§6.2). It governs how fast a price change becomes a
quantity change and therefore the effective demand elasticity of the entire economy. It has no
empirical basis, and its sensitivity must be reported prominently.

### D11 — Default is a choice, not only a circumstance

**Decided:** households may spend beyond their reference budget shares and default as a result.
A `payment_priority π` parameter governs whether a squeezed household cuts consumption or stops
servicing debt. Defaults are reported split into **involuntary** (ran the squeeze order to the
floor) and **strategic** (had the funds, chose consumption). `MODEL.md` §6.8.

**Why:** default arising only from income shocks makes credit a hazard that befalls people.
Allowing voluntary overextension makes it partly a consequence of who the household is, which is
the more honest model and a sharper result. The involuntary/strategic split matters for the
essay in particular: "credit crushed them" and "they chose the phone over the payment" are
different claims, and if the model produces mostly strategic defaults that is an argument
*against* reading the result as structural coercion. It gets reported either way.

**Degeneracy closed:** costless default would have every household overspending forever. Arrears
accrue with penalty interest, collateral is repossessed, new lending is denied for a period, and
the risk premium rises with the credit record and decays slowly. There is no debt discharge in
v1, which makes default *more* punishing than reality — a bias against the thesis, and therefore
an acceptable one.

**Second procyclical channel:** the credit record feeds back into origination, so the town's own
default history tightens its credit supply, alongside the capital constraint of D1.

### D12 — Transport is one need with three means

**Decided:** cars and public transport added to the goods table alongside bicycles. Each
household has a `mobility_need` per tick satisfiable by car (large lump + recurring
`car_running_cost`, financed over 60 months, high status), public transport (pure flow, a fare
per tick, no credit, capacity-limited network) or bicycle.

**Why:** it is the only genuine substitution in the model. Everything else is a want that is met
or not; here the *same need* has a cheap flow option and an expensive financed option, so a shift
in that choice caused by credit availability needs no interpretation to be meaningful.

Three things this buys that a plain "transport" category would not:

- **The running cost is the trap.** A car is not affordable because its instalment is. Running
  costs continue regardless of income and are the most common route into overextension (D11,
  route 4). Omitting them would make a car a one-off decision and lose the mechanism.
- **Public transport is capacity-limited.** A town that shifts to cars lets the network wither,
  which raises the cost of *not* owning a car for everyone remaining — lock-in through nobody's
  intention. This is the selection argument from the accompanying essay, appearing as an
  infrastructure result rather than a claim.
- **Cars are the second-largest financed purchase after housing**, so leaving them out omitted
  most of the consumer loan book.

**Assumption flagged:** public transport carries a slightly negative status weight. That is a
claim about social meaning, not an economic fact. Switchable via
`transport_status_asymmetry`; any result depending on it must say so.

### D13 — Initial housing tenure and firm capital are drawn from configured ranges

**Decided:** households start as outright owners, mortgaged owners or renters, defaulting to
30/30/40 (`small_town` profile), with a `german_average` profile of 22/21/57 provided for
comparison. Firms are initialised by per-sector random draw over three separate fields:
`capital_stock` (real capacity), `cash` + `deposits` (financial working capital), and
`initial_debt`.

**Why the small-town default:** Germany has the EU's lowest home-ownership rate, roughly 43–47%
of households, but that national figure is pulled down by the large cities — Berlin is under 20%
— while small towns and rural areas run 55–65%. The model is a Kleinstadt. Both profiles get
run, because the tenure mix strongly determines how a credit expansion distributes: owners gain
from rising house prices, renters only pay for them.

**Initialisation artefacts closed:**

- Mortgaged owners draw a remaining term and an LTV, so the town does not amortise in lockstep
  and develop a synchronised repayment wave that is a pure artefact.
- Firms must differ. Identical firms set the same price, hit capacity together and all survive or
  all fail, so a sector never shows selection dynamics. The *width* of each capital range is
  therefore itself a parameter worth sweeping.
- Firms start with debt. A town of debt-free firms is not a neutral starting point; it is the
  most favourable one available to the credit thesis.

**Open item closed:** rented dwellings are owned 65% by private landlord households drawn from
the top wealth deciles and 35% by a rental firm, matching the German pattern in which roughly
two-thirds of rental dwellings are held by private individuals. This makes rent a transfer
*within* the population, which is the distributionally interesting case.

**New open item created:** outright ownership tracks age in reality and the model has no
demography, so it is assigned by wealth decile instead. This makes outright owners richer than
they are and slightly overstates the reported wealth gap.

### D14 — The primary experiment is the abstainer cohort, not a pooled price ratio

**Decided:** the headline result is the real consumption of a fixed subpopulation who never
borrow, as a function of the credit appetite of everyone else (`MODEL.md` §1.2). The relative
price claim is retained as a *mechanism check* in two identified forms: a per-good financeability
switch (C2a) and a difference-in-differences within the elasticity grid (C2b).

**Why:** the original pooled metric — financeable price index ÷ non-financeable index — was
confounded. In the goods table financeability is near-perfectly collinear with durability, status
weight, lumpiness and supply inelasticity, so *any* demand increase from any source would move it
and credit would have been given the credit. Worse, D3's predicted ordering (housing ≫ durables >
services > food) is exactly what the supply-elasticity column alone would produce, so the
confound would have read as confirmation.

The abstainer cohort is the author's own formulation made measurable: *"if person A buys on
credit, the same product gets more expensive for person B who could have afforded it without a
loan."* Hold B fixed, vary everyone else, measure B. No index construction, no confound.

**Also recorded:** "demand that would not otherwise exist" decomposes into four channels —
timing, money creation, never-would-have, and shortened replacement cycles — of which only the
last three are permanent. If the model produces the effect but the decomposition shows it is
mostly timing, that is a substantial partial refutation and gets reported as one.

### D15 — Firm profit goes to shareholders; income inequality comes from the wage hierarchy

**Decided:** profit splits two ways, retention and `shareholder_profit_share`. Employees receive
none of it. Instead each firm has a **headcount** drawn from a per-sector range and an internal
**wage hierarchy** — management ~8% of positions at 2.5–6× the firm's base wage, skilled ~32% at
1.2–1.8×, basic ~60% at 0.7–1.0× (§5.2.3).

**Why the profit channel was needed at all:** the earlier spec named `owner households` and never
distributed anything to them. That was not a simplification but a hole — money would have
accumulated on firm balance sheets over 480 ticks and produced deflation as an artefact
attributable to whatever knob happened to be under test.

**Why employees are excluded from it:** profit-sharing (*Erfolgsbeteiligung*) is uncommon in
Germany, so a meaningful employee share would be unrealistic — and it would be quietly favourable
to the thesis, returning profit to exactly the households that spend most of it and suppressing
the concentration the model exists to measure.

**Why the wage hierarchy is the better mechanism:** most income inequality lives inside firms, not
between owners and non-owners. A wage structure produces it directly, and it adds a route into
overextension the model previously lacked — a household demoted from a management to a basic
position takes the corresponding income fall while its debts do not change (§6.8, route 2).

**Calibration:** 3–8× top-to-bottom within a small-town firm. Large-listed ratios of 50× and up
are a different phenomenon and do not belong in a *Kleinstadt* of eighty firms. Both the tier
shares and the multiples are swept.

**Consistency requirement recorded:** total positions must equal `n_households` **exactly**. D20
removed unemployment, so this is an identity the loader enforces rather than a target it
approximates: there is no `target_employment_rate` and there is no "unemployment route into
default" — the income shock runs entirely through demotion. `MODEL.md` §13.6 carries firm counts
and headcount ranges that reach ~796 positions before scaling; the earlier illustrative ranges
reached 255 and would have needed a scaling factor of 3.1.

### D16 — Shares are owned and traded, but not priced by a market in v1

**Decided:** households hold shares, have an `equity_appetite ε` governing how much saving goes
into them, and trade them with each other. Valuation is `trailing_earnings × earnings_multiple`,
a configured constant. Endogenous share prices are deferred.

**Why include shares at all:** they answer the standing objection to the whole thesis. A critic
says B's premium is a transfer, not a loss — the seller gains it. Whether that matters depends on
*who owns the seller*, which is unanswerable without modelled shareholding. With it, the transfer
from cash buyers to concentrated owners becomes a measured quantity.

Shares also give the model an equity-versus-debt margin it otherwise lacked: newly issued shares
are the one route by which firm investment can be financed without credit.

**Why not a market price:** a traded equity price is a second asset market that credit can bid up,
with its own bubble dynamics, and it would very likely dominate the housing result and destroy
attribution. Until it exists, the model can say nothing about equity-price inflation — only about
who receives the profit stream. That limitation must be stated wherever share results are reported.

**A distributional channel with no price movement:** a credit-stressed household under the squeeze
order sells its stake to a household with surplus, transferring future profit income upward. This
requires no share price to move at all.

### D17 — Findings of the 2026-09-01 specification review

An adversarial review of `MODEL.md` and `DECISIONS.md` was commissioned before any code was
written. It confirmed all 55 `§` cross-references resolve, and found the defects below. Those
fixed are recorded here; those still open are in `REVIEW-BACKLOG.md`.

Fixed: the confounded C2 metric (→ D14); `effective_price` contradicting the instalment mechanism
in §6.2; `status_gain` computed as a level rather than a margin, which gave it the wrong sign and
meant the status treadmill could never fire; the missing firm profit distribution (→ D15); a tick
order in which the default decision needed information from four steps later; two validation
assertions that would have aborted correct runs (`assets = liabilities` is false in the presence
of outside money, and `M` is not constant at `reserve_ratio = 1.0` because the reserve rule is an
inequality); a null run contradicted by the initialisation; bank equity specified as both
configured and residual; repossession *loosening* the capital constraint by removing risk-weighted
assets; two different quantities both named `κ`, with a multiplier formula valid for only one of
them; and the absence of any instrument by which full-reserve intermediation could actually
happen, which had left the decisive control run determined by its own initialisation.

Corrected facts: the EU mortgage risk weight is LTV-graduated under CRR3 since January 2025, not a
flat 35%; with `equity` meaning CET1 the threshold is 7%, not 10.5%; the 3% leverage ratio was
missing and often binds first for a mortgage-heavy bank; German mortgaged owners outnumber
outright owners, so the tenure profile was inverted; the housing budget range is roughly 1.5× the
German average rather than merely "toward the high end"; v1 has no taxes, so the stated reason for
adjusting published expenditure shares was wrong; Engel's law covers food, while the housing
share is Schwabe's law and considerably weaker; and the README said 20:1 where 5:1 was meant.

### D18 — One decision rule: marginal value per euro. The category budget is removed

**Decided:** households rank **candidate units** — one dwelling, one car, one haircut, one
restaurant meal — by `marginal_value / unit_cost`, and buy in descending order while two tests
pass: the unit's score exceeds `λ` (the household's marginal value of holding money, which rises
as it falls below its buffer target), and it is affordable in cash now or as an instalment over
the loan term. Nothing else. The per-category euro budget of the previous draft is gone.

**Why:** the spec had two decision formalisms that were not derivable from one another — a
per-good ranking and a category budget with a habit anchor — and the second's formula was
circular, blending "demand from the ranking" with a reference share. Neither could produce what
the other needed. Which one actually decided purchases was undefined, and it was the single
largest implementation blocker.

**What the marginal rule solves that the ranking alone did not:** *quantity*. The old rule chose
*which* goods but never *how many* restaurant meals or how much clothing — and firms were told to
"observe sales" with no demand curve to observe. Diminishing marginal value per unit
(`marginal_value(g,n) = base · n^(−α)`) determines quantity endogenously: a household buys meals
until the next one is not worth its price, not until its money runs out.

Indivisibility of durables needs no special case — the second identical phone simply has near-zero
marginal value.

**Saving becomes a reservation price on money**, not a pre-committed set-aside. A household short
of its buffer demands more from each euro. This also removes the ordering problem where saving was
set aside at one step and cut to zero at the next.

**Budget shares are now an output.** They set the opening state and serve as a reference line for
reporting; nothing consults them during the run, and households may leave their initial ranges
permanently in either direction. Realised-minus-initial share is a headline result — the model's
answer to what credit did to how people live, in units a reader recognises from their own bank
statement. Only `contractual` (lease, mortgage, car running costs) and `subsistence` (food floor)
still bind anything.

### D19 — The housing auction derives the price instead of defining it

**Decided:** a bid is `min(credit limit, own valuation)`, where valuation is
`(rent_avoided + joy + σ·status_gain) × capitalisation_factor`. Sellers hold reserve prices,
movers may stay put, renters make an explicit rent-versus-buy comparison, and the market clears
**simultaneously** so that chains settle without bridging finance. Chain failures are recorded.

**Why:** with every bidder bidding their maximum loan, the clearing price *is* the second-highest
credit limit — so house prices were a deterministic function of `dsti_max` and `ltv_max`. Housing
is the heaviest weight in the price result, so the headline finding was close to being defined by
two policy parameters rather than emerging from anything. The credit limit must constrain
willingness to pay, not replace it.

**Which side binds is now a reported output**, and either answer is informative: if the credit
limit binds in nearly every transaction, credit really is setting house prices — a strong finding.
If valuations bind, credit is permissive rather than causal.

**Rent is no longer a fixed yield on price.** A constant yield passed every credit-driven price
rise straight into rents, making renters worse off by definition and largely determining the
distributional result before any agent acted. The rental market now clears between tenant demand
and landlord supply, and the yield is an output. Real yields compress in booms precisely because
they do not track prices one-for-one.

*Still not modelled and stated as such:* extrapolative house-price expectations (the canonical
amplifier in credit–housing models — omitting them makes the model conservative), and cooperative
or municipal rental stock priced on a cost basis, roughly a tenth of German rentals.

### D20 — No unemployment; interest rates are annual

**Decided:** every household holds a position at all times. Firms contract by *releasing*
positions, and released households are reassigned the same tick — at whatever tier is open, which
may be lower than the one they left.

**Why:** unemployment would be a second large shock channel competing with credit for explanatory
power, and with no state and no benefits an unemployed household would have zero income, which
would manufacture destitution and default at rates that say more about the missing safety net than
about credit. This also closes backlog item B4, where the absence of a safety net was biasing
*toward* the thesis.

**The cost, which must be stated wherever default figures appear:** the income-fall route into
overextension now runs entirely through **demotion**. That is a real mechanism but milder than job
loss, so **the model's default rates are conservative and cannot be compared with observed data.**

**Interest rates are annual nominal throughout**, with the per-tick rate `r_annual / 12` — simple
division, as instalment loans are actually quoted. Recorded because the omission is a silent 12×
error, and because the same convention must hold in the loan schedule, the deposit credit and the
DSTI test alike.

### D21 — Wages track profit annually; the price level is anchored by the money stock

**Decided:** every 12 ticks a firm reviews wages against the year's profit — up by
`wage_increment` in a profitable year, down by up to `wage_cut_max` in a loss year, with dividends
and retention out of what remains. Wages lag by a year, deliberately.

**Why:** wages previously responded only to labour scarcity, so under any positive inflation real
wages fell monotonically over 480 ticks and debt burdens rose mechanically — manufacturing the
falling-consumption result out of an omitted indexation channel rather than out of credit.

**The deeper half of the same problem, now answered in the spec:** nothing had said what pins the
nominal price level. It is **not** the markup rule, which is homogeneous of degree one in the price
level and pins nothing. It is the **quantity of money**: in a closed town, if every price doubled
while the money stock did not, households could not clear the market, inventory would accumulate
and prices would fall back.

Consequence worth carrying into the writing: under credit creation, broad money moves with the
loan book, so the general price level moves with it — meaning C1 is close to a restatement of the
money supply. A further reason it stays behind the abstainer cohort.

### D22 — Firms do not fail; no unemployment; no intermediate goods

**Decided:** a firm short of money cuts wages at the annual review, then calls capital from
shareholders pro rata, then borrows from the bank on ordinary terms. It never becomes insolvent.
Firms buy labour only.

**Why:** the project is about what credit does to prices and to consumers. Firm survival is taken
as given rather than simulated, and every gap is filled from a household or from the bank so the
accounting still closes. The capital call is the useful part — it converts a firm's loss into a
direct reduction of its owners' deposits, a real distributional channel.

**The cost, which must be reported alongside any result:** removing firm failure removes the
channel by which a credit boom becomes a bust on the supply side — no insolvency wave, no fire
sale, no cascade. Together with the exclusion of unemployment (D20) this town's supply side is
close to frictionless, so **the model is conservative about credit's harms and cannot produce a
crash.** Its results are a lower bound on disruption, and claims about instability are outside what
it can support.

### D23 — Transport is measured in mobility units, which puts it inside the one decision rule

**Decided:** the unit is *mobility*, not the vehicle. Each household needs `mobility_need` units
per tick; a car supplies `car_capacity` units per tick for its life at the cost of purchase plus
running costs, public transport supplies units per tick for a fare with no capital outlay, a
bicycle supplies fewer for almost nothing.

**Why:** transport was the one case that did not fit §6.2's marginal-unit formalism, being a need
met three ways rather than a good owned or not. Reframing the unit puts it inside the general rule
with no special case — **which is what makes the substitution evidence rather than assumption.**

Note the asymmetry this exposes: `car_running_cost` enters the household's `unit_cost` but **not**
the instalment the bank tests. That is precisely why cars are the commonest route into
overextension.

### D24 — A bank run halts the simulation, and the halt is the result

**Decided:** when realised withdrawal demand in a tick exceeds reserves on hand, the run is
recorded and **the simulation stops**. Not "deposits exceed reserves", which is true by
construction at any `reserve_ratio < 1`.

**Why halt rather than trade through:** everything that follows a run — deposit insurance,
central-bank support, resolution, panic — is outside this model and would have to be invented.

**Why this is a result and not a failure:** the reported quantity is the tick at which it happened
and the conditions that produced it. Across the reserve sweep this yields a frontier — the settings
at which the arrangement survives 40 years and those at which it does not — which may be among the
more interesting outputs of the project, at no cost beyond recording the event.

**Its limit, stated:** runs here are *emergent*, arising only from households and firms moving
between cash and deposits as §6.6 already has them do. There is no confidence variable, no
contagion, no panic — a confidence dynamic is trivially tunable to produce any desired result. Real
runs are driven by exactly the panic this omits, so **the absence of a run proves nothing about
safety**, while its presence is meaningful.

**The bank is also an employer**, with the same wage tiers and profit distribution as any firm. Its
staff are ordinary households whose income rises with the bank's profitability, so part of the
town's income depends directly on the size of the loan book — a small concrete instance of the
essay's argument, and reported as such.

### D25 — Value and cost are both per-tick flows; repossessed goods have a second-hand market

**Decided (B5):** `score = value_per_tick / user_cost_per_tick`, where
`user_cost = depreciation + financing cost + running cost` and a service collapses to its price.

**Why:** a car delivers joy over 60 ticks and a meal over one, so dividing either by a *total*
price made them incomparable and loaded an arbitrary bias between durables and services into the
ranking. Since the financeable goods are almost all durables, that bias landed squarely on the
primary result — a second route into the confound D14 was meant to close.

**What the fix buys beyond consistency:** the user-cost form puts the **interest rate inside the
durables-versus-services comparison**. Cheap credit makes durables cheaper per tick relative to
services and shifts the mix toward them; dear credit reverses it. That channel is central to what
the model tests and the total-cost form could not express it at all.

**Decided (B7):** the claim that the high/low-credit comparison "shifts `θ`, nothing else" is
corrected. One *configured* input changes; the realised joint distribution moves with it, because
`θ` and `φ` are drawn jointly. Realised means of every drawn parameter are reported per scenario.

**Decided (B15):** repossessed dwellings re-enter the housing auction with the bank as a forced
seller at a haircut; cars and durables are offered as **second-hand units** ranked by §6.2 like any
other, differing only in price, remaining life and a status penalty; unsold stock is written off at
zero after `liquidation_ticks`.

**Why this is more than plumbing:** a second-hand market is the abstainer's substitute. A household
that will not borrow can still obtain mobility by buying used — lower per-tick user cost, status
penalty. So the model can show whether credit pushes cash-paying households *down into the used
market* rather than merely making them pay more. That is a sharper and more concrete version of the
primary claim than any price index, and it is directly observable in the world.

**Emergent consequence to watch:** repossessions raise second-hand supply, which lowers used
prices, which lowers the collateral value of everyone else's durables and therefore what the bank
will lend against them — a real procyclical channel that arises rather than being coded.

---

### D26 — Findings of the second specification review: units, levels, and revision damage

A second review was commissioned specifically for **damage from repeated revision** — sections
edited at different moments that quietly contradict each other — rather than for design error.
That was the right thing to look for: most of what it found was self-inflicted.

**The three that decide whether the primary result means what it says:**

1. **§9 named two primary results.** It labelled the pooled financeable ÷ non-financeable ratio
   "primary result (C2)" while its own later block correctly named the abstainer cohort, and D2
   still asserted the pooled ratio with no supersession note. This is the exact defect the *first*
   review was commissioned to close, surviving in the two places a reader is most likely to quote
   from. The ratio is now labelled a mechanism check; D2 carries a supersession header.

2. **`value` was in utils and `user_cost` in euros.** `score` was therefore utils per euro, `λ`
   had no stateable unit, and — the real damage — the **price level was pinned by the joy scale
   rather than by the money stock**, contradicting §5.2.3's claim that the money stock is the
   anchor and guaranteeing that §10's neutrality check would fail for a reason having nothing to
   do with credit. `joy_g` is now euros per tick at the t = 0 price level, indexed to lagged CPI;
   `score` and `λ` are dimensionless; `λ` has an explicit form and an interpretation (`λ = 1`
   buys anything worth its cost). Indexation is to the **town-wide** index, not the good's own
   price, which would make every demand curve vertical.

3. **The abstainer cohort was not actually held fixed.** §1.2 said its behaviour was fixed;
   §6.4 raised `θ` with stress and §6.6 moved `φ` with the deposit rate, stress and demotions —
   both of which fire for abstainers. The pin and the endogeneity rules were written at different
   times and one had to yield: the cohort is now **exempt from §6.4 and §6.6 feedback**, and only
   from those.

   Worse, and not a contradiction but a genuine gap: abstainers are also wage earners,
   shareholders, landlords and bank employees, so under D21 their income *rises* with everyone
   else's credit. The headline could come out positive while the price claim is entirely correct.
   The number is now reported **decomposed** into (a) the cohort's t = 0 basket revalued at each
   scenario's prices with nominal income held on the low-credit path — the price-only
   counterfactual, and the actual headline — and (b) the income effect as the residual.

**The model had no price level, no wage level and no household opening balance sheet.** §13 opens
by declaring that a parameter not in its tables does not exist, and then omitted every euro
figure that would let any other figure be judged. This is why the transport magnitudes went
unnoticed for so long: `mobility_need = 1.0` against `fare = 3.0` priced a household's entire
monthly mobility at **€3.00** against a car's €180 running cost, so no household would ever buy a
car on cost grounds and the substitution test §4.1 calls the cleanest in the model was dead.
§13.11 now fixes `base_wage`, opening prices per sector, the dwelling price and household opening
balances, and the implications are worked through so they can be checked rather than assumed.

**`M0` was raised from €8m to €20m**, and the reasoning is recorded because the review's own
diagnosis needed correcting. It argued that a €40m opening mortgage book was impossible against
€8m of base money. It is not impossible: at full reserve the book is funded by **time deposits**,
which carry no reserve requirement, so a large loan book and a small fixed base money stock are
consistent. What `reserve_ratio = 1.0` actually pins is `demand_deposits ≤ reserves`, which
constrains the **composition** of household claims, not their total. The real failure was
narrower and worse: at €8m the loader could satisfy that constraint only by handing the bank an
implausible equity position, leaving the capital and leverage constraints slack in *every*
scenario — a swept dial that was never connected. Two loader assertions replace the old
"equity ≥ 0" test, which almost nothing fails.

**Five places where an implementer would have had to invent a mechanism** are now specified:
labour reallocation under exact full employment (the pool must be empty at the end of the step —
full employment is an identity, not an aspiration); the rental market's tick step, which §6.5 and
§6.1 each assumed the other provided while 40% of the town rents; time deposits, which §7.1.1
declared load-bearing and §6.6 never implemented; the three rival housing valuation rules, now
one rule applied at two horizons; and firm capacity investment, which had **no counterparty** and
was an open money leak that §10's first check would have aborted on at the first investment.

**Two calibrations were prohibitive rather than wrong.** `k_scarcity = 4.0` put the loan rate
above 75% at the headroom floor, and since nothing restrains how much the bank *wants* to lend,
`high_credit_gold` would have spent its life at the rate cap — the decisive control failing for a
reason unrelated to the thesis. It is now 0.25, with the realised rate distribution during warm-up
as a gate. And every rate is quoted in per cent, so the per-tick divisor is **1200, not 12**; read
literally, the old convention charged 25% a month.

**Two consequences of the units fix that the review did not reach**, found while applying it.
The §4 status weights (1.5 housing, 1.3 cars, 0.1 a haircut) were utils like `joy` and would have
become 1.5 *euros per tick* — negligible against a car's €480 monthly cost, silently deleting the
positional channel the model exists to test. `w_g` is now a **dimensionless relative weight** and
a separate `status_scale` supplies the euros, which also usefully separates a judgement one can
argue from observation (which goods are visible) from one nobody can observe (how much standing is
worth against rent). And `joy_g` could not simply be *assigned* in euros either: twelve invented
numbers would put the entire consumption pattern in the modeller's hands. It is **solved for** at
initialisation against the Destatis budget shares of §13.3 — which is what finally makes those
shares earn their place, and which carries its own admission, now stated in §13.11: the model is
calibrated to the world as it is, credit included, so the low-credit run is an extrapolation away
from the calibration point rather than a return to it.

**What was rejected.** The review proposed adding a bank recapitalisation route. Instead
`bank_equity ≤ 0` **halts the run**, like a bank run: a recapitalisation would need a source of
funds the town does not have, and the honest treatment of "the bank failed" is to report it as a
terminating condition rather than to paper over it.

### D27 — The engine is written in C# on .NET 10

**Decided:** C#, targeting .NET 10, with `TreatWarningsAsErrors`, `CheckForOverflowUnderflow` and
`Nullable` all enabled. Full reasoning and measurements in
[`LANGUAGE-CHOICE.md`](LANGUAGE-CHOICE.md); benchmark sources in `bench/`.

**Why, in short.** The speed question turned out to be binary rather than graded. A benchmark of
the §6.2 inner loop — the only part of the tick that resists optimisation, and about 95% of its
cost — put Python **36–76× behind every compiled candidate** (27.6 hours per sensitivity campaign
against under an hour), while the four compiled options span only **2.1×** among themselves. So
runtime eliminated exactly one language and then stopped being a criterion.

That left the criterion that actually matters for this program: it is an instrument whose
characteristic failure is a *plausible wrong number*, not a crash. Three project settings and two
value types (`Money(long Cents)` with no conversion from `double`, `Rate` whose only accessor
applies `/1200`) turn the four defect classes both reviews kept producing — unit confusion,
convention misapplication, float money, missed cases in closed sets — into **CS0019, CS0029,
CS8509 and `OverflowException`**. Each was confirmed by writing the mistake and watching the build
fail, not by assertion.

**Rejected, and why.** *Rust* has the strongest safety story of the five and was excluded by the
author on maintainability grounds — he does not know it well enough to maintain confidently, and
assistant-written but owner-unmaintainable code is a dependency, not an asset. Its unique
strengths (fearless concurrency, manual memory) are also the two things this workload does not
need, since parallelism is one process per seed with no shared state. *Zig* was the fastest
practical option but is pre-1.0; `std.time.Timer` ceased to exist mid-benchmark, which is fine when
the language is the project and not fine in an instrument backing a published claim. *Go* lost
narrowly: its named types do give unit safety, but it has no exhaustiveness checking and no checked
arithmetic, and the author is more fluent in C#.

**Two things recorded because they cost something.** First, the benchmark's own composition: the
**sort is 67%** of it, which means the results are really a measurement of sorting and the idiom
had to be equalised per language before the table meant anything (`qsort` vs inlined C was 1.83×;
`sort.Slice` vs `slices.SortFunc` was 1.42×; and C#'s supposedly-faster struct comparer was
*slower* than a plain delegate). An unequalised first table had to be retracted. Second, the
measurement assumed 120 candidate units per household per tick, which the spec does not state —
that scales all languages alike, so the ratios hold and only the wall-clock predictions move.

**Obligations this places on the implementation**, since the benchmark measured *achievable* rather
than automatic performance: agents in flat integer-indexed arrays rather than an object graph; no
allocation in the §6.2 hot loop; filter to `score > λ` before sorting, since candidates below λ are
never examined; and parallelism **across seeds only**, never within a tick, which preserves
determinism and the paired-seed protocol of §13.1. Plus one boundary: the engine writes CSV or
Parquet and nothing else — no analysis, no plotting — so it stays small enough to audit end to end
and the analysis tooling remains a separate, reversible choice.

### D28 — Profit is distributed annually, at a staggered fiscal year end

**Decided:** a firm distributes profit once every `fiscal_year_ticks` (12), at its own year end, as
part of the **same** review that sets wages (§5.2.2, §5.2.3). Year ends are staggered across the
calendar by a per-firm `fiscal_year_offset`. `wage_review_period` is replaced by `fiscal_year_ticks`,
since the two were always one event.

**Why:** §5.2.2 said "each tick, after investment is decided, a firm splits its profit two ways"
while §5.2.3, one subsection below, reviewed wages every twelve. Firms do not pay dividends monthly,
and having the two cadences differ meant the firm decided pay and payout from different profit
figures. One annual review computing one number, and deciding both from it, is both realistic and
simpler.

**Why staggered:** with 79 firms on a shared calendar, the town would receive a twelfth of its
annual property income in a single tick — a demand spike that is an artefact of the calendar rather
than of anything economic, and the same class of error as letting every mortgage amortise in
lockstep (§5.1.2). `synchronised_fiscal_year` remains available and is swept, because real economies
do have dividend seasons and the gap between the two settings is worth measuring.

**What it costs, recorded because it is not free:** between year ends, revenue minus wages
accumulates on firm balance sheets, so money returns to households later than before. It is bounded
at twelve ticks, but firms hold more cash on average, which withdraws base money from the vault
(§6.2) and tightens credit slightly. It also makes the capital call of §5.2 **less** frequent — a
firm short of money in month eight has not yet paid out the year's profit, so retention is still
available. Both effects are small and both are now reported.

### D29 — The household's affordability test is myopic, and the rich/poor asymmetry is emergent

**Decided:** a household's own affordability test is `instalment ≤ income − debt service already
running − rent − subsistence`, evaluated **this tick**. Not over the term of the loan. Running costs
are excluded from it by default. Both are switches (`affordability_horizon`,
`affordability_includes_running_cost`) so each assumption is measured.

**Why the horizon changed:** §6.2 previously required the instalment to fit "the monthly residual
for the term of the loan", which handed every household perfect foresight over five to twenty-five
years — and flatly contradicted §6.8, which needs households to stack loans that each pass alone
and together do not. With full-term foresight, routes 1, 3 and 4 of §6.8 all close: a household
could not stack, could not be caught by a later price rise, and would keep a buffer against both.
Myopia is not a simplification here; it is the behaviour under test.

**Why running costs are excluded:** §4.1 calls the running cost the commonest route into
overextension and says it enters `unit_cost` but not the instalment the bank tests. That only works
if the household is not already counting it either. The instalment is what a buyer looks at; the
fuel and insurance arrive afterwards and do not stop.

**The saying, and why it must not be coded:** there is a saying that the poor ask whether they can
afford the monthly payment and the rich ask whether they can afford the price. The model reproduces
it — but every household applies the **same** rule, and what differs is which test *binds*. A
household with the cash clears the price test and pays cash, and since `r_l > r_d` always,
financing strictly worsens a unit's score, so it strictly prefers to. A household without the cash
can only reach the same unit through the instalment test, which is a far lower bar.

That the asymmetry is **emergent from liquidity rather than assigned by type** is what lets the
model say anything about it. If the two tests were handed out by household class, every
distributional result would be a restatement of the assignment. As it stands, a household that
becomes liquid stops using the instalment test on its own, and a squeezed one starts.

### D30 — The replacement cycle is an output, and that is what instruments channel R

**Decided:** `durability` is a good's **functional life**, not its replacement cycle. Electronics
moves from 24 ticks to 60 — a five-year-old phone works. The observed replacement cycle of roughly
1–5 years is an **output** of the §6.2 decision under status decay, never a drawn parameter.

**Why the old value was wrong:** the goods table set electronics to 24 and annotated it "real
replacement cycle ~2 years", conflating the two. §4 already flags exactly this distinction for
housing — its 360 ticks are an ownership horizon, not the life of the building — and simply did not
flag it here. The consequence was mispriced depreciation: €25 a month against a €600 phone rather
than €10.

**Why the correlation with `θ` must not be an input.** Replacement cycles do correlate with credit
appetite, and it would be easy to draw a household's cycle from a distribution tied to `θ`. That
would be a mistake. The mechanism already produces the correlation unaided: a household that can
finance faces the instalment test, a far lower bar than accumulating €600, so a new unit's `score`
crosses `λ` earlier and it replaces sooner; a cash buyer waits for the price and lands at the long
end of the range. Households replacing at four to five years are, as a **result**, very unlikely to
have financed.

Imposing that correlation would make "credit shortens the replacement cycle" a restatement of the
setup, and channel **R** of §1.1 — the replacement-cycle channel — would be assumed into existence
rather than measured. As an output it is precisely the instrument R needs. This is the same class of
error as the confounded pooled price ratio that D14 replaced, and it would have been harder to spot.

**Consequences:** realised cycle by `θ` decile and the financed share of replacements are now
reported (§9), and there is a **calibration gate** — if the realised electronics distribution does
not span roughly 1–5 years, `status_decay` and `α_g` are miscalibrated and no treadmill result is
usable.

### D31 — Functional life, exponential car depreciation, and one value curve used three times

**Decided:** the `durability` column becomes **functional life** — how long a good keeps working —
and is no longer the replacement cycle. Cars 180 ticks, furniture 240, bicycles 180, clothing 60,
electronics 60. A good's market value at age is defined **once** by a per-sector curve, and three
places read it: the user-cost depreciation term (`V(age) − V(age+1)`), the second-hand opening ask,
and collateral value for LTV, risk weight and recovery.

**Cars depreciate exponentially at 16% a year; everything else is straight-line.** Straight-line is
badly wrong for cars — over a 180-tick life it values a five-year-old car at 67% of new against a
market figure near 40%. The exponential curve gives 59% at three years, 42% at five, 17% at ten.
**Every car depreciates at the same rate** regardless of price or segment; that variation is real
and deliberately not modelled, since it would add an unanchorable parameter. Car *prices* do vary,
drawn per unit around the sector price.

**What this fixes, which was worse than a wrong number.** The second-hand market priced a used unit
against the **current new price** regardless of age, so a fifteen-year-old car and a one-year-old
car had the same ask — and the abstainer's substitute, which §5.3.1 calls a sharper version of the
primary claim, could not work. Under straight-line depreciation over the old 60-tick life, a
five-year-old car was worth exactly zero.

**Three consequences now expressible:** depreciation is **front-loaded**, so a new car costs ~€440 a
tick against ~€289 for a five-year-old one — that gap *is* the cash buyer's substitute, quantified.
A 60-month car loan against a 16% curve leaves the borrower in **negative equity** for roughly two
years, which is why car repossession recovers so little. And the replacement cycle stays an
**output** (D30): the observed 5–15 year range is a result, and long-cycle households should turn
out to be the ones who did not finance.

## Open, deliberately

Listed in `MODEL.md` §12. The two that most affect how results may be stated:

- **Single bank.** Its monopoly inflates C4 by construction. Any distributional figure from v1
  must carry this caveat.
- **House price expectations.** Bidders have no extrapolative expectation of capital gains, the
  canonical amplifier in credit–housing models. The omission biases *against* the thesis.
- **Home-equity withdrawal.** No refinancing against a risen valuation, which is one of the main
  real-world routes from house prices into consumption. Also biases against the thesis.

*(Bank runs were listed here as a deliberate omission; **D24 added them** and made the halt tick a
headline result. The entry is removed rather than left to contradict the decision.)*
