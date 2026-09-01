# Review backlog

Findings from the specification review of 2026-09-01 that are **not yet fixed**. Recorded so they
are not lost. Fixed items are summarised in `DECISIONS.md` D17.

Ordered by how much each could distort a result.

---

## Would predetermine or distort a result

### B1 — The housing auction defines the price rather than deriving it

**RESOLVED 2026-09-01** — bids are now `min(credit limit, valuation)` with seller reserves, a stay-put option and an explicit rent-vs-buy decision; which side binds is reported. See §6.5.
§6.5 has each bidder bid up to "the largest mortgage the bank will grant plus available cash". In
an ascending auction where everyone bids their maximum, the clearing price **is** the second
highest credit limit — so house prices become a deterministic function of `dsti_max` and
`ltv_max`. Housing is the heaviest weight in the price result, so the outcome is close to being
defined by two policy parameters.

There is no reservation value anywhere: no willingness-to-pay from joy/status/rent-equivalent, no
seller reserve price, no option to stay put, and no rent-versus-buy decision at all despite 40% of
the town renting.

*Fix:* bid `min(credit limit, own valuation)`, valuation derived from the household's own
joy/status/rent-equivalent calculation; report how often each side binds. If the credit limit binds
100% of the time that is itself the finding — but it must be measured, not assumed.

### B2 — Rent is proportional to house price by construction

**RESOLVED 2026-09-01** — the rental market now clears between tenant demand and landlord supply; the yield is an output, not a constant. See §6.5.
§6.5 sets rent as a fixed yield on the prevailing price, so every credit-driven price rise passes
fully and immediately into rents and renters are hit by credit standards *by definition*. Real
yields compress in booms precisely because they do not track prices one-for-one. With 40–57% of
the town renting, this largely determines the distributional result.

*Fix:* clear a rental market between tenant demand and landlord supply, or at minimum sweep the
yield and run a control where rent tracks tenant income. Note also that roughly a tenth of German
rental stock is cooperative or municipal and priced on a cost basis; the model has no such landlord.

### B3 — No wage response to the price level

**RESOLVED 2026-09-01** — annual wage review against firm profit (§5.2.3), and the price-level anchor stated explicitly: it is the quantity of money, not the markup rule.
§5.2 has wages adjust only to labour scarcity. Over 480 ticks with any positive inflation, real
wages fall monotonically and debt burdens rise mechanically, manufacturing C3 and rising defaults
as artefacts of an omitted indexation channel.

Deeper version of the same gap: with `M0` fixed, no state, and markup-over-wage-cost pricing, **the
spec never says what pins the nominal price level.** §2 principle 5 promises adaptive expectations but they
appear concretely only in construction's "expected price exceeds build cost". This should be
answered before code is written — it decides whether the null run and the neutrality check are even
coherent.

### B4 — No safety net, and the bias runs *toward* the thesis

**CLOSED 2026-09-01 by decision, not fix** — unemployment is excluded from the model entirely (§5.2), so the zero-income case cannot arise. Default rates are consequently conservative and must not be compared with observed data.
The state is absent in v1, so an unemployed household has zero income and goes straight to
destitution. Unlike the "no debt discharge" bias the spec notes with approval (D11), this one
maximises the frequency and severity of "credit crushed them" outcomes.

*Fix:* add a minimal subsistence transfer funded by a lump-sum levy — enough to keep the accounting
closed without the full state module — or state prominently that v1 default and destitution rates
are upper bounds and are not comparable to German data.

### B5 — `score = value / total_cost` is not unit-consistent across goods

**RESOLVED 2026-09-01** — both sides of the ratio are now per-tick flows: value per tick of ownership over user cost per tick (depreciation + financing + running cost). This also puts the interest rate inside the durables-versus-services comparison, a channel the total-cost form could not express. See §6.2.
§4 defines joy as accruing "per tick of ownership **or** at the moment of consumption", so a car's
joy accrues over 144 ticks and a meal's over one, yet both are divided by a total price. The
ranking carries an arbitrary bias between durables and services — and durables are the financeable
set, so this is a second independent channel into the same confound D14 was meant to close.

*Fix:* rank by lifetime value ÷ total cost, or per-tick joy ÷ per-tick user cost. Either is
defensible; the current mix is not, and the choice moves the result.

### B6 — `habit_inertia` is swept into a region the spec calls untestable, and is named backwards

**PARTLY RESOLVED 2026-09-01** — `habit_inertia` no longer exists; the category budget it belonged to has been removed. The naming and sweep-range concerns are moot.
At ι = 0 the formula in §6.7 is exactly fixed budget shares, which D10 says is Cobb-Douglas and
makes the price claim untestable. Grid C sweeps from 0.05, so the low end will show "no effect" for
a definitional reason and, reported as a robustness range, will make the result look fragile when
that region is excluded by the spec's own argument.

Separately the name is inverted: high `habit_inertia` should mean *sticky*, but in the formula high
ι means reoptimising every month. Rename to `reoptimisation_rate` before anyone writes code.

*Fix:* state the admissible lower bound and why; report the excluded region separately and labelled.

### B7 — "Nothing else changes" is false as written

**RESOLVED 2026-09-01** — the claim is now that one *configured* input changes and the realised joint distribution moves with it; realised means of every drawn parameter are reported per scenario. See §5.1.
§5.1: "The high-credit / low-credit comparison shifts the distribution of `θ`, nothing else." Two
paragraphs later θ and φ are drawn jointly with `rho_theta_phi = −0.3`, so shifting θ shifts φ. The
ρ = 0 control half-catches this, but the flat sentence is exactly the kind that ends up quoted in
the blog post.

---

## Underspecified — a programmer must invent something that changes results

### B8 — Two incompatible decision formalisms *(largest implementation blocker)*

**RESOLVED 2026-09-01** — the category budget is gone. §6.2's marginal-value rule is the only decision rule; budget shares are an output (§6.7).
§6.2 is a per-good score ranking over discrete items. §6.7 is a per-category euro budget with a
habit anchor blended with "demand_from_§6.2". Neither is derivable from the other — a ranking does
not yield a euro amount for a lumpy good — and §6.7's formula is circular. **Which one decides
purchases?** Must be resolved before any code.

### B9 — Quantity is never determined

**RESOLVED 2026-09-01** — diminishing marginal value per unit determines quantity, and gives firms a demand curve. See §6.2.
Except for food's subsistence floor, nothing says *how many* restaurant meals or how much clothing.
Firms are told to "observe sales" but there is no demand curve to observe.

### B10 — Transport cannot be expressed in §6.2's formalism

**RESOLVED 2026-09-01** — the unit is *mobility*, not the vehicle. Each means supplies mobility units at its own cost structure, so §6.2 ranks them with no special case. See §4.1.
§4.1's `mobility_need` is a discrete choice among three means for one need; §6.2 ranks goods the
household "does not currently own". And a "share of the need" (§5.1.1's transport row) cannot be a
share of income — it is a category error as written.

### B11 — Interest rate units are never stated

**RESOLVED 2026-09-01** — all rates are annual nominal; per-tick rate is `r_annual / 12`. See §7.3.
Per tick or per annum? With monthly ticks this is a 12× error waiting to happen. `k_scarcity`,
`k_deposit`, `r_base`, `spread`, `r_d_base` all lack units and defaults. `r_base` and `spread` are
also redundant — both are additive constants.

### B12 — Settlement medium unspecified

**RESOLVED 2026-09-01** — cash first, remainder from deposits. See §6.2.
Nothing says whether a purchase is paid in cash or from deposits, or in what mix. This drives
reserves, which drive §7, which the spec calls the heart of the model.

### B13 — Housing auction mechanics

**RESOLVED 2026-09-01** — simultaneous clearing, chain failure recorded, outgoing mortgage repaid at settlement. See §6.5.
Where do available dwellings come from? A mover must sell before bidding or bid before selling — a
chain problem with no stated resolution. Is the vacated mortgage repaid from proceeds? Do renters
bid? Can a mover choose to rent instead? Who buys newly constructed dwellings, with what money?

### B14 — Landlord assignment is arithmetically tight and unspecified

**RESOLVED 2026-09-01** — landlords are owner-occupiers holding rental dwellings in addition, drawn from the top 3 wealth deciles, with a Pareto draw for dwellings each. See §5.1.2.
320 renter households need 320 rented dwellings; 65% = 208 held by "top wealth deciles", i.e. at
most ~160 households. How many each? Are landlords themselves owner-occupiers (they must be, to be
consistent with 30/30/40)? This drives the distributional result.

### B15 — Repossessed goods have no market

**RESOLVED 2026-09-01** — dwellings re-enter the housing auction with the bank as a forced seller; cars and durables are offered as second-hand units ranked by §6.2, with reduced price, remaining life and a status penalty; unsold stock is written off after `liquidation_ticks`. See §5.3.1.
§6.8 repossesses dwellings, vehicles and durables and §5.3 holds them as assets, but §7.2's
liquidation rule is stated only for the capital constraint. The price at which they clear, and to
whom, is undefined — so bank equity can get stuck in phones.

### B16 — Firms that cannot pay wages

**CLOSED 2026-09-01 by decision** — firms do not fail (D21). Wage cut → shareholder capital call → bank loan. Cost: no supply-side bust channel, so the model is conservative about credit's harms.
Wages are paid before revenue arrives. §5.2 gives a buffer; nothing says what happens when it runs
out short of the "negative equity for n ticks" failure test.

### B17 — §6.4's stress equation ratchets

**RESOLVED 2026-09-01** — `stress_decay` added, clamp bounds stated, all four weights given
defaults and flagged as unanchored in §13.5. Any result depending on stress feedback must be
reported with `stress_feedback = false` alongside it.
`stress ← clamp(stress + a·dsr − b·joy + c·(rank drop))` has no decay term, so it rises
monotonically for any indebted household. `rank drop` is undefined; joy and the debt-service ratio
have incommensurable scales so `a` and `b` are unconstrained; the clamp bounds are unstated. Stress
feeds θ and π, so this propagates through the whole credit cycle.

### B18 — Seed protocol

**RESOLVED 2026-09-01** — stated in §13.1: the same 30 seeds across all scenarios, compared
paired seed-by-seed.
§2 principle 4 says one seed per run, §8 says 30 seeds per scenario, §5.2.1 warns that differing firm draws
would confound comparisons. The resolution — the *same* 30 seeds across all scenarios, compared
paired seed-by-seed — is implied but never stated, and skipping it costs real statistical power.

---

## Missing mechanisms, not currently listed as deliberate omissions

- **House price expectations.** Bidders have no expectation of capital gains. Extrapolative
  expectations are the canonical amplifier in credit–housing models; omitting them biases *against*
  the thesis, which is acceptable, but it should be said.
- **Home-equity withdrawal / refinancing.** Rising collateral values loosen credit only for new
  purchases. Existing owners cannot borrow against appreciation — one of the main real-world
  channels from house prices to consumption.
- ~~Intermediate goods.~~ **CLOSED 2026-09-01 by decision** — no intermediate goods; the
  "and suppliers" phrase is removed. Wages are the whole of a firm's marginal cost.

---

## Editorial

- ~~§8's grids unnumbered; Grid B empty; Grid A missing `low_credit_light`.~~
  **RESOLVED 2026-09-01** — numbered §8.1–8.3, cells named, `low_credit_light` added.
- ~~§5.1 states `bank_shares` are "concentrated in a minority of households" as fact.~~
  **RESOLVED 2026-09-02** — there is no separate `bank_shares` field. The bank sits in the same
  `shares{}` register as any firm, valued on the same `earnings_multiple` and allocated by the
  same concentration parameter (§5.1.3). It was previously outside the net-worth definition of
  §9, and therefore outside the wealth Gini, which is C4.
- ~~§4's supply column and its 2×2 grid disagree.~~ **RESOLVED 2026-09-01** — `elasticity_g` is
  now a numeric per-sector parameter (§13.4), so the grid is derivable from the table. Original
  finding: **§4's supply column and §4's 2×2 grid disagree.** Restaurants are "medium" in the table and
  inelastic in the grid; furniture and bicycles are "medium" and elastic; cars are "medium" and
  inelastic. "Medium" lands in both rows arbitrarily, so the grid — "that grid is the experiment" —
  is not derivable from the table it summarises. Use a numeric elasticity per sector.
- ~~Warm-up an order of magnitude short.~~ **RESOLVED 2026-09-01** — raised to 240 ticks; the run
  log records whether the transient actually decayed.
- ~~"Two full credit cycles in 480 ticks" asserted with no stated cycle length.~~
  **RESOLVED 2026-09-01** — replacement cycles set from real-world figures (housing 30 y as the
  ownership horizon, cars 5 y, phones 2 y) and the run length justified against them.
- ~~No parameter appendix.~~ **RESOLVED 2026-09-01** — §13 lists every parameter with units,
  default and sweep status, plus §13.10 gathering the fourteen with no empirical anchor. This also
  closed B11 (rate units), B17 (stress had no decay term, weights unbounded) and B18 (seed
  protocol: the same 30 seeds across all scenarios, compared paired).


---

## Second review — 2026-09-02

Commissioned to look for **damage from repeated revision** rather than for design error, and that
is mostly what it found. Twenty-nine of its findings are fixed; D26 records the reasoning for the
substantive ones. What remains open is listed here.

### Open

- **B19 — MODEL.md and DECISIONS.md restate every decision at near-full length.** This is why so
  many of the contradictions above appeared in pairs: a rule was fixed in one file and left
  standing in the other. D2, D4, D6 and D15 all had to be corrected in this pass for exactly that
  reason. The duplication is not accidental — DECISIONS.md is meant to be readable on its own —
  but it has now caused the same class of defect twice, and a third review will find more of it
  unless the decision log is cut back to *what was decided and why*, with the mechanism left to
  MODEL.md alone. **Not done**: it is a large edit with no effect on any result, and it should be
  done once the spec stops moving rather than during it.

- **B20 — `α_g` sets the demand elasticity of the whole economy and has no anchor.** Unchanged
  from the first review, restated here because the units fix made it sharper: with `joy` now in
  euros, `α_g` is the only thing standing between the model and unit-elastic demand everywhere.
  It is swept and ⚠-flagged, which is the honest treatment, but no sweep makes an unanchored
  parameter anchored.

- **B21 — the deposit requirement gates tenure change, and it rides on a policy parameter.**
  Only visible once §13.11 supplied a price level. A deposit is €36,000 at `ltv_max = 0.80` and
  €9,000 at 0.95 — 13.5 versus 3.4 months of mean gross income, against a `φ` that tops out at 8.

  **The mechanism is a missing behaviour, not a missing number.** Nothing in the model directs
  saving toward a house: `φ` is homeostatic, and §6.2's affordability test for a financed purchase
  is a *flow* test, so the deposit is the only **stock** a household must assemble and no rule
  assembles it. Accumulation past `φ` is an undirected by-product of `λ_base`, `ε` and the `joy`
  calibration. At €100/month of surplus a basic-tier renter needs **252 ticks** for the tight
  deposit and **63** for the loose one, against a 240-tick measurement window — so tenure mobility
  is routine at one setting and near-absent at the other, decided by where an unanchored
  accumulation rate falls relative to the run length rather than by anything about housing.

  **Four consequences**, all now reported rather than fixed:

  1. **`ltv_max` does two jobs.** It is a lending standard *and* an on/off switch for entry into
     ownership. Its swept effect will read as a finding about credit standards while being
     substantially a finding about tenure mobility — the confound of §1.3 in a new place.
  2. **Frozen tenure cuts both ways.** It removes first-time buyers as a demand channel, which
     biases *against* the thesis; it also traps renters under landlord-set rents, which biases
     *toward* the distributional half of it. Which dominates is not deducible and must be measured.
  3. **Procyclical exclusion, and it is sharp.** A basic-tier renter (€1,700/month) on the cheapest
     dwelling at 95% LTV sits at DSTI 0.334 against a tight cap of 0.35. A **50 bp** rise in `r_l`
     — well inside what §7.3's scarcity premium produces — lifts the instalment from €568 to €599
     and DSTI to 0.352, and the marginal buyer is excluded *exactly when* credit is abundant and
     prices are rising. That is an emergent mechanism worth reporting, not noise to average over.
  4. **It reaches the primary result through rents.** Abstainers have `θ = 0`, so they never
     finance and can essentially never buy. Their entire housing cost is rent, which means the
     headline number's housing channel runs wholly through the rental market and is therefore
     governed by `sitting_tenant_lag` — a parameter introduced in this same pass, which *damps*
     exactly that transmission. The headline must be reported across its swept range.

  **The eventual fix is probably (c), not more sweeping.** Options considered: (a) explicit
  down-payment saving — realistic, but it manufactures the tenure transitions the model exists to
  observe, and adds an unanchored parameter; (b) recalibrate prices or incomes so the deposit is
  reachable at 80% — decalibrates from German reality (price-to-income ~5.6×) to make a mechanism
  work; (c) **family transfers and inheritance**, which is how a large share of German first-time
  buyers actually raise a deposit. (c) is the honest one and its absence is a *second* defect:
  the model has no demography, so it cannot show the main route by which housing wealth
  concentrates across generations — which is C4, a claim the model already reports. Deferred
  because it needs a demographic layer the model does not have.

- **B22 — the labour market clears by assumption, not on a wage.** §5.2's reallocation rule now
  guarantees full employment as an identity: the pool must be empty at the end of the step. That
  is a defensible simplification under D20 and it is stated as one, but it means the only wage
  mechanism in the model is the annual profit review, and relative wages between sectors cannot
  respond to anything. A sector in a boom cannot bid labour away from a sector in a slump.

- **B23 — house price expectations.** Bidders have no extrapolative expectation of capital gains,
  the canonical amplifier in credit–housing models. Carried forward from the first review;
  omitting it biases *against* the thesis, which is why it is tolerable.

- **B24 — home-equity withdrawal.** No refinancing against a risen valuation. Also carried
  forward, also biasing against the thesis.

### Fixed in this pass

Primary-result labelling (§9 named two); the utils/euro units break in the core decision rule and
the price-level anchor that depended on it; the abstainer cohort's exemption from §6.4/§6.6
feedback and the price-only decomposition of the headline; the missing price, wage and opening
balance-sheet levels (§13.11); `M0`; transport magnitudes; firm counts and headcounts; the
leverage ratio that was promised into `min()` and absent from it; `spread`; the per-cent vs
fraction rate convention; `k_scarcity`; the `c̄`/`κ̄` formula; firm failure surviving in five
places; the labour reallocation rule; the rental market's missing tick step; time deposits; the
three rival housing valuation rules and the undefined `credit_limit`; housing depreciation charged
against the ownership horizon; firm investment's missing counterparty; second-hand prices that
could not fall; repossession marking; bank wages breaching the reserve inequality; bank equity
missing from the wealth metrics; circular tenure initialisation; firm credit standards; the
capital call's missing tick step; the unreachable null run; negative bank equity as a silent
terminating condition; `α_g` on indivisible durables; leisure/holidays missing from §4's grid;
the `investment_lag`/`construction_lag` duplicate; the §13.10 list that had drifted; and the
assorted stale counts and off-by-ones.
