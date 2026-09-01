# Model Specification

Status: **draft**. This document defines the simulated economy before any code exists.
Where a design choice would predetermine the result, it is exposed as a switch rather than
fixed here. Open questions are collected in the last section.

---

## 1. The claim under test

The claim, in the author's words:

> If person A buys an item on credit because he cannot afford it, the same product gets more
> expensive for person B, who could have afforded it without a loan. If A had not bought, B
> would not have to pay that premium.

This is a **pecuniary externality**: A's credit-financed demand raises the market price, and B,
who never borrowed, pays the difference. The mechanism itself is not controversial. What has to
be established is that the effect exists, that it is caused by credit rather than by something
credit happens to correlate with, and that it is large enough to matter.

### 1.1 The two conditions the claim depends on

**Condition 1 — supply must be less than perfectly elastic.** If the producer can make one more
unit at the same cost, A's purchase adds output rather than price, and B pays nothing. The claim
is therefore conditional, and the condition is a property of the good, not of credit. This is why
housing is the strongest case and why an elastic manufactured good may show nothing at all. Any
statement of the result must carry this condition with it.

**Condition 2 — the demand must be additional, not merely earlier.** This is the harder one, and
it is where most naïve versions of the argument fail. If A would have bought the phone in 24
months after saving, credit moves the purchase forward but does not change the steady-state flow
of purchases: in a town where everyone does this, the same number of phones is bought per month,
and everyone simply owns theirs 24 months sooner. Prices rise during the transition and then
settle.

So "demand that would not otherwise exist" decomposes into four channels, which behave
differently and **are measured separately**:

| Channel | Permanent? | Mechanism |
|---|---|---|
| **T — Timing** | No | The purchase is brought forward. Raises prices during the transition only. The weakest channel and the first one a critic will reach for |
| **M — Money creation** | While the loan book grows | Lending creates deposits (§7), so the extra demand does not have to come out of anyone's saving |
| **N — Never-would-have** | Yes | A buys what he would never have deliberately saved for. Requires the status treadmill (§6.3) or a high discount rate. The purest form of "artificial" demand |
| **R — Replacement cycle** | Yes | With credit perpetually available, A replaces the good every 24 months instead of every 40, because the instalment is always affordable and rolls into the next one. A permanent increase in the demand *flow*, and the direct link to the obsolescence argument |

**Channels N and R are where the thesis lives.** If the model produces the effect but the
decomposition shows it is almost all T, the honest conclusion is that credit changes *when*
people buy, not *how much* — which would be a substantial partial refutation. The decomposition
is therefore not a diagnostic; it is part of the result.

### 1.2 The primary experiment — the abstainer cohort

The claim is about a specific person: B, who does not borrow, and who pays more because others
do. That is directly implementable.

**The abstainer cohort** is a subpopulation with permanently low `θ` and high `φ` who never
finance anything. Only the credit appetite of *everyone else* varies.

"Held fixed" means something precise, and the rest of this document has to honour it: the
cohort's `θ` and `φ` are **exempt from the endogenous feedback of §6.4 and §6.6**. Stress does
not raise their `θ`; the deposit rate, stress and recent demotions do not move their `φ`. Without
that exemption the treatment leaks into the control — more credit elsewhere raises prices, which
raises their stress, which raises their borrowing — and the cohort stops being a fixed yardstick
at exactly the moment the effect appears. Their other behaviour (the §6.2 ranking, the squeeze
order, moving house) is fully endogenous, as it must be: the point is to watch what they *do*
about the prices they face. The primary result is:

> **Real consumption of the abstainer cohort, as a function of the credit appetite of the rest
> of the town.**

This is the counterfactual in the author's sentence, stated as a number. It needs no
interpretation and no index construction, and it cannot be produced by any of the confounds that
afflict a pooled price ratio.

**But it is not price-only, and must not be reported as if it were.** Abstainers are also wage
earners, shareholders, landlords and bank employees. Under D21 more credit raises firm revenue,
which raises wages a year later; under §5.2.2 and §5.3 it also raises dividends and bank pay. So
raw real consumption of the cohort mixes the pecuniary externality with a general-equilibrium
income effect that plausibly runs the *other way* — and the headline could come out positive
while the price claim is entirely correct. The number is therefore reported **decomposed**:

- **(a) The price-only counterfactual — this is the headline.** The cohort's t=0 basket revalued
  at each scenario's realised prices, holding cohort nominal income on the low-credit path. This
  is "what B pays for the same life", which is exactly the claim.
- **(b) The income effect**, as the residual between (a) and realised real consumption. If (b)
  offsets (a), that is a finding and is stated as one: credit made B's basket dearer *and* his
  income larger, and which dominates is an empirical question the model can answer.

Reported alongside: the cohort's nominal expenditure, the quantity it goes without, and the share
of its income absorbed by prices set at the margin by borrowers.

### 1.3 The mechanism check — C2, corrected

The price channel still has to be demonstrated, but **not** as a pooled ratio of financeable to
non-financeable goods. In the goods table (§4) financeability is near-perfectly collinear with
durability, status weight, indivisibility and supply inelasticity, so a pooled ratio would rise
whenever demand rose from *any* source — a wage increase, the status treadmill, stress feedback —
and credit would get the credit. The predicted ordering "housing ≫ durables > services > food"
is exactly what the supply-elasticity column alone would produce.

C2 is therefore measured two ways, both of which hold the good's other properties fixed:

**C2a — the per-good financeability switch.** `financeable` is a per-good flag. The identical
goods table is run with one good's flag toggled and everything else — elasticity, durability,
status weight, indivisibility — unchanged. The price change of *that good* is the effect of credit on
*that good*. This is an identified experiment; the pooled ratio was not.

**C2b — difference-in-differences within the elasticity grid.** The ratio is computed *within*
each cell of the financeability × elasticity grid (§4) and then differenced, so the elasticity
effect cancels rather than being attributed to credit.

Leisure/holidays is the only good in the table that breaks the collinearity — durability 1,
financeable, 12-month term — which makes its price series the single most informative one in the
model. It is reported on its own.

### 1.4 Secondary claims — recorded, not pursued

Measured every run and written to output, so the data exists if one of them becomes the more
interesting story. None drives a design decision, and none should be argued from until it has had
the scrutiny the primary experiment is getting.

| # | Claim | Measured by | Why parked |
|---|---|---|---|
| C1 | Credit raises the general price level | CPI across all goods | Nominal-level claims are weak: if every price and balance doubles, nothing real changes. Becomes interesting if the abstainer effect turns out to be driven by a general level shift rather than a relative one |
| C3 | Credit lowers real consumption per household long-run | Real basket per household per year | The strongest welfare claim and the hardest to defend — needs the labour-supply, wage-indexation and saving margins to be trustworthy first (§12) |
| C4 | Credit shifts income and wealth toward the bank | Interest share of household income; wealth Gini | Contaminated by the single-bank monopoly assumption (§12), which inflates it by construction. Reportable once competing banks exist |

### 1.5 The objection to have ready

B pays more, and the money goes to the seller. A critic will say this is a **transfer, not a
loss**: the town is no poorer, the surplus has merely moved. The answer has three parts, and the
model should be able to quantify each:

1. **B specifically is not compensated.** B pays more and receives nothing. That the seller gains
   is no consolation to B, and the distributional question is a real question.
2. **Interest is not a transfer within the period.** It is a claim on future output, and under
   money creation it is a claim created without a corresponding act of saving.
3. **The genuine deadweight losses are narrower**: resources drawn into producing goods that
   would not otherwise have been produced, and B priced out of purchases entirely rather than
   merely paying more.

Which of these the model actually produces is an empirical question for it to answer. "It is just
a transfer" is the first thing a trained economist will say, and the essay should not meet it
unprepared.

**A result that contradicts any of this is a valid result and gets published as such.** This is
not a disclaimer. If the simulation refutes the thesis, the refutation is the finding.

---

## 2. Design principles

1. **Stock-flow consistency.** The town is closed. Every payment is another agent's receipt, and
   every *inside* claim — deposits, loans, shares — is another agent's liability. Cash is outside
   money and has no counterpart, so the aggregate identity is `Σ net financial assets = M0`, not
   `assets = liabilities`. A consistency check runs at the end of every tick and aborts the run on
   violation (§10). This is written before any behaviour.
2. **Switches, not assumptions.** Any parameter that could by itself produce the expected
   result is a configuration value, and the opposing setting is always run as a control.
3. **The model must be able to refute the thesis.** No rule is added whose only purpose is to
   make credit look harmful.
4. **Determinism.** One seed per run, all randomness drawn from it, results byte-identical on
   re-run.
5. **Behavioural, not equilibrium.** Agents follow simple rules with adaptive expectations.
   Nothing is solved for equilibrium; prices and quantities emerge from the rules. There is no
   guarantee the economy settles, and failing to settle is itself an observation.

---

## 3. The town

One town, no trade with the outside world, no migration in or out.

| Quantity | Default | Note |
|---|---|---|
| Households | 800 | Each is one economic decision-maker |
| Persons per household | 2.4 | Used only for per-capita reporting |
| Firms | ~30 | See sector table; 2–4 per sector so price competition exists |
| Banks | 1 | The monopoly lender. Its monopoly position is itself a finding, not a neutral choice — see open questions |
| Dwellings | 850 | Slightly more than households; near-fixed stock, expandable only by construction |
| Tick | 1 month | |
| Run length | 480 ticks (40 years) | Covers more than one full housing cycle (360 ticks), twenty car cycles and forty phone cycles |
| Warm-up | 240 ticks | Discarded from all statistics. See §13.1 — 60 was too short to clear a 300-month mortgage book |
| Base money `M0` | 8,000,000 | Fixed stock of physical currency. See §3.1 |

Population is constant. Households age only in the sense that debts amortise; there is no
demography in v1.

### 3.1 Money and initial balances

Two kinds of money exist, and the distinction is what makes the reserve setting in §7 bite.

- **Base money (`M0`)** — physical currency. Its quantity is **fixed for the whole run** and
  cannot be created by anyone. Think of it as the town's stock of gold coin. It is held either
  as cash by households and firms, or as reserves in the bank's vault.
- **Deposits** — claims on the bank. Created when the bank grants a loan (subject to §7),
  destroyed when a loan is repaid. Deposits are money for spending purposes but are *not*
  base money.

Accordingly:

```
bank reserves = M0 − cash held by households − cash held by firms
broad money M = deposits + cash held by the public
```

The only way the bank's reserves can rise is for the public to hold less cash. Nothing the
bank does can increase `M0`. This is the hard constraint that gives the reserve ratio its
teeth, and it is why households and firms need an explicit cash-versus-deposit choice (§6.6).

**Initial balances are configuration, not derived.** Every starting stock is set in the
scenario file: cash and deposits per household (by income decile), cash and deposits per firm
(by sector and size), the bank's initial reserves, and any pre-existing loans. **Bank equity is
not configured — it is the residual**, `equity = reserves + loans + repossessed assets −
deposits`, exactly as firm equity is (§5.2.1). Configuring both equity and the balance sheet
would over-determine it.

The loader asserts, before tick 0, that:

- all cash plus bank reserves equals `M0` exactly;
- the reserve constraint (§7.1) holds;
- the capital constraint (§7.2) holds;
- bank equity is non-negative.

A scenario file that violates any of these is **rejected with a diagnostic**, not silently
started. This matters more than it sounds: an initial mortgage book of 240 dwellings can easily
exceed `M0` several times over, which would hand the bank an enormous residual equity and leave
the capital constraint non-binding in every scenario without anyone noticing.

Starting the economy away from its own steady state is allowed and sometimes
interesting, but the warm-up period must then be long enough for the transient to decay, and
the run log records whether it did.

---

## 4. Goods

Every good carries five properties that together determine how credit reaches it.

- **Durability** — how many ticks it lasts before it must be replaced. `1` means non-storable.
- **Status weight** — how strongly owning it raises social standing. Status is *relative*
  (§6.3), so this is a coefficient, not a score.
- **Joy** — direct contribution to wellbeing / stress reduction per tick of ownership or at
  the moment of consumption.
- **Financeable** — whether the bank will lend against it, and on what terms.
- **Supply elasticity** — how fast the producing sector can expand output.

| Sector | Durability | Status | Joy | Financeable | Supply | Note |
|---|---|---|---|---|---|---|
| Food | 1 | 0.0 | subsistence | no | high | Fixed minimum quantity per household per tick |
| Clothing | 18 | 0.4 | low | no | high | Replaced early for status, not wear |
| Smartphones / electronics | **24** | 1.0 | medium | yes, 24 mo | high | Real replacement cycle ~2 years |
| Furniture | 120 | 0.6 | low | yes, 36 mo | medium | |
| Bicycles | 96 | 0.5 | medium | yes, 24 mo | medium | |
| Cars | **60** | 1.3 | medium | yes, 60 mo | medium | Real replacement cycle ~5 years. Second-largest financed purchase after housing. Carries a **running cost** each tick — see §4.1 |
| Public transport | 1 | −0.1 | low | no | **capacity-limited** | Non-storable service, flat fare, network capacity expandable only by investment |
| Restaurants | 1 | 0.5 | high | no | medium | Non-storable service |
| Personal services (hairdresser) | 1 | 0.1 | low | no | medium | Non-storable service |
| Leisure / holidays | 1 | 0.9 | high | yes, 12 mo | medium | Consumed at once, high status |
| Housing | **360** | 1.5 | high | yes, 300 mo | **near-zero** | Fixed stock; expanded only by construction. The 30 years is the **ownership horizon** — how long before a household moves or substantially renovates — not the physical life of the building, which persists in the stock indefinitely |
| Construction | — | — | — | — | low | Produces dwellings; slow, capital-intensive |

The table is deliberately spread across the elasticity × financeability grid. That grid is the
experiment:

|  | **Not financeable** | **Financeable** |
|---|---|---|
| **Elastic supply** | Food, clothing, personal services | Electronics, furniture, bicycles, **leisure/holidays** |
| **Inelastic supply** | Public transport, restaurants | **Housing, cars** |

Cell membership follows the numeric `elasticity_g` of §13.4, not the "high/medium/low" wording of
the table above — which could not produce this grid, since restaurants and furniture were both
"medium" and belong in opposite cells. **Leisure/holidays was missing from the grid entirely**
despite §1.3 calling its price series the most informative one in the model; it is the only
financeable good with durability 1, which is precisely why it breaks the collinearity.

If the thesis is right, the price response to a credit expansion should be ordered:
housing ≫ financeable durables > services > food. If prices move together, C2 is refuted.

### 4.1 Transport: the substitution test

Transport is the only need in the model that can be met in three different ways, and that makes
it the cleanest test of the mechanism outside housing. Every household requires
`mobility_need` units per tick, and may satisfy them by:

| Means | Cost structure | Status | Credit |
|---|---|---|---|
| **Car** | Large lump at purchase + `car_running_cost` every tick thereafter (fuel, maintenance, insurance, all recurring and unavoidable while the car is owned) | 1.3 | Yes, 60 months |
| **Public transport** | Pure flow. A fare per tick, no capital outlay, cancellable at any time | −0.1 | No |
| **Bicycle** | Small lump, negligible running cost, limited range | 0.5 | Yes, but rarely needed |

#### How this fits the one decision rule (§6.2)

Transport was the one case that did not fit the marginal-unit formalism, because it is a *need*
met three ways rather than a good that is owned or not. The resolution is to make the unit
**mobility**, not the vehicle:

- Each household requires `mobility_need` units of mobility per tick.
- Each means *supplies* mobility units at its own cost structure:

| Means | Mobility supplied | Cost of supplying it |
|---|---|---|
| Car | `car_capacity` units per tick, for `durability` ticks | Purchase price (financeable) **+ `car_running_cost` every tick owned** |
| Public transport | Bought per tick, as many units as paid for | `fare` per unit, nothing up front, cancellable |
| Bicycle | `bike_capacity` units per tick, limited range | Small purchase, negligible running cost |

§6.2 then ranks them without any special case, because everything there is already per tick
(§6.2, *Everything is measured per tick*) and everything there is already in euros. A car's
`value` is `mobility_value = min(car_capacity, mobility_need) · fare` — the fares it replaces —
plus its own joy and status; its `user_cost` is depreciation plus financing plus
`car_running_cost`. A tick of public transport has `value` equal to the fares its mobility
replaces and `user_cost` equal to the fares paid, so its `score` is exactly 1 before status: it is
the **numéraire of the transport decision**, and every other means is judged against it. That is
what makes the comparison meaningful rather than a contest between two invented scales.

**The fare and the need must be calibrated together or the test is dead.** With
`mobility_need = 1.0` and `fare = 3.0` an earlier draft priced a household's entire monthly
mobility at €3.00 against a car's €180 running cost alone, so no household would ever buy a car on
cost grounds and only `σ` could move the decision — killing the substitution this section calls
the cleanest test in the model, and making the 6–18% transport budget share of §13.3 unreachable
by construction. `mobility_need` is therefore **trips per month**, not journeys per lifetime: 20
units against a €3 fare is €60 a month of public transport, against roughly €480 for a car once
depreciation and running cost are counted. A car then has to earn its €420 premium from joy,
status and the convenience the fare cannot supply — which is a real question with a real answer,
and is what the sweep is for.

**The substitution therefore falls out of the general rule rather than being coded as a special
case**, which is what makes it evidence. A household buys the car when the mobility it supplies
beats the fares it replaces, at that household's own `σ` and given what credit makes reachable.
Note what the running cost does: it enters `unit_cost` but **not** the instalment the bank tests,
which is precisely why cars are the commonest route into overextension (§6.8, route 4).

Why this matters:

- **It is a genuine substitution.** Everything else in the model is a want that is either
  satisfied or not. Here the *same need* has a cheap flow option and an expensive financed
  option, so a household choosing the car is choosing to convert a running cost into a debt plus
  a larger running cost. If credit availability shifts that choice, the effect is unambiguous and
  needs no interpretation.
- **The running cost is the trap.** A car is not affordable because its instalment is
  affordable. `car_running_cost` continues every tick regardless of income, and it is the most
  common route into route-4 overextension in §6.8. Leaving running costs out would make cars a
  one-off decision and lose the mechanism entirely.
- **Public transport is capacity-limited, not elastic.** Its supply expands only by investment
  in the network, with a long lag. So a town that shifts to cars lets the network wither, which
  raises the cost of *not* owning a car for everyone remaining — a lock-in effect that operates
  through no one's intention. This is the selection argument from the accompanying essay,
  appearing here as an infrastructure result rather than a claim.
- **The status ordering is deliberate.** Public transport is given a slightly negative status
  weight, which is an assumption about social meaning rather than an economic fact. It is
  switchable (`transport_status_asymmetry`), and any result that depends on it must say so.

### 4.2 Obsolescence

Two mechanisms, both optional switches, both drawn from the accompanying essay:

- **Physical**: the good stops working after `durability` ticks.
- **Perceived**: the good's status contribution decays at rate `status_decay` per tick from the
  date the *newest generation* appeared, independent of whether it still works.

With perceived obsolescence off, replacement is driven purely by wear. The difference between
the two runs isolates how much of the demand is manufactured.

---

## 5. Agents

### 5.1 Household

State:

| Field | Meaning |
|---|---|
| `cash` | Physical currency held. Part of `M0` |
| `deposits` | Bank balance. Can not go negative |
| `loans[]` | Outstanding loans: principal, rate, remaining term, collateral, type |
| `employer` | Firm id and wage tier. Never empty — there is no unemployment (§5.2) |
| `wage` | Nominal wage per tick |
| `owned[]` | Goods held, with purchase tick and condition |
| `dwelling` | Owned, rented, or none |
| `credit_appetite θ` | ∈ [0,1] — willingness to finance a purchase rather than wait |
| `savings_propensity φ` | Target financial buffer, in months of income. Governs how much income is withheld from consumption |
| `cash_preference κ` | ∈ [0,1] — share of financial assets held as cash rather than deposits |
| `budget_shares{}` | Reference share of income per category, drawn at t=0 — see §5.1.1 |
| `payment_priority π` | ∈ [0,1] — how strongly debt service is protected against consumption. Low `π` defaults rather than goes without |
| `credit_record` | Arrears history and ticks since last default. Governs the bank's risk premium and denial (§5.3) |
| `status_sensitivity σ` | ∈ [0,1] — how much relative standing enters the purchase decision |
| `stress` | ∈ [0,1] — see §6.4 |
| — | *(There is no separate `bank_shares` field. The bank is share number 79 in `shares{}` above, valued and allocated like any firm — §5.1.3)* |

`θ`, `φ`, `κ` and `σ` are drawn per household from distributions set by the scenario. The
high-credit / low-credit comparison shifts the **configured** distribution of `θ` only; every other
configured parameter is held identical, same seed, same shocks.

**It does not follow that nothing else changes**, and the spec previously claimed it did. Because
`θ` and `φ` are drawn jointly at `rho_theta_phi` (below), moving `θ`'s distribution also moves the
realised distribution of `φ`. The honest statement is that **one configured input changes and the
realised joint distribution moves with it** — which is why `rho_theta_phi = 0` is run as a control,
and why the realised means of every drawn parameter are reported per scenario rather than assumed
to match their configuration. `φ` gets its own comparison
(`high_saving` / `low_saving`) because it moves the economy through two separate channels —
see §6.6.

`θ` and `φ` are drawn jointly with correlation `rho_theta_phi`, default −0.3: a household eager
to borrow tends to be less eager to save. Setting it to 0 is a control run, because a negative
correlation on its own amplifies whatever credit does, and that amplification should be
attributable rather than baked in.

### 5.1.1 Budget shares

Each household is initialised with a share of income per spending category, drawn from a
configurable range. These shares determine the **opening state**: what the household owns at
tick 0 and what it is contractually committed to.

They are **not** a decision rule. Nothing consults them once the run starts, and a household may
leave its initial range permanently in either direction. See §6.7 for why that is not a
simplification but a requirement — a rule that held households near these shares would assume
away the very thing the model exists to measure.

```toml
[budget_shares]
# min and max share of income; drawn per household at t = 0
housing        = { min = 0.30, max = 0.50, binding = "contractual" }
food           = { min = 0.08, max = 0.16, binding = "subsistence" }
clothing       = { min = 0.03, max = 0.07 }
electronics    = { min = 0.01, max = 0.04 }
furniture      = { min = 0.01, max = 0.05 }
transport      = { min = 0.06, max = 0.18 }   # one need, three means (§4.1)
restaurants    = { min = 0.02, max = 0.07 }
services       = { min = 0.01, max = 0.03 }
leisure        = { min = 0.04, max = 0.12 }
```

**Only two categories carry a `binding` flag, and only these two constrain anything during the
run:**

| Flag | Meaning |
|---|---|
| `contractual` | A lease or mortgage schedule. A real obligation, paid before discretionary spending, changeable only by moving or refinancing. Car running costs inherit this while the car is owned |
| `subsistence` | A quantity floor, bought before anything else. If food prices rise the share rises; it cannot fall below the physical minimum |

Everything else is discretionary and handled entirely by §6.2's marginal rule.

**Engel and Schwabe effects.** A household's position *within* each range is tied to its income
decile rather than drawn independently: poorer households sit near the top of the housing and
food ranges and near the bottom of the leisure range. Two distinct regularities are being invoked
and they do not carry equal weight:

- **Engel's law** — the food share falls as income rises. Among the most robust empirical
  regularities in economics, and safe to rely on.
- **Schwabe's law** — the housing share falls as income rises. A separate and considerably weaker
  regularity, contested in modern data and sensitive to tenure.

The food gradient can be asserted; the housing gradient is an assumption and is swept
(`schwabe_gradient`, including zero). Conflating them under "Engel's law" would borrow the
robustness of the first to cover the second.

**Normalisation.** The drawn shares plus the household's `φ` buffer target (§6.6) are scaled to
sum to 1 at t=0. The loader reports the mean scaling factor; far from 1.0 means the configured
ranges are mutually inconsistent and should be fixed rather than absorbed.

**Calibration note.** Two errors are easy to make here and both would be caught by a reader:

- Published Statistisches Bundesamt EVS / LWR figures are shares of **consumption expenditure**,
  not of income, so they cannot be used unchanged. In v1 there is no state and no tax (§5.4), so
  gross income = net income and the only wedge between the denominators is **saving** — at a 10%
  savings rate, a consumption share converts to roughly 0.9× as an income share. When the state
  arrives in phase 5, tax becomes a second wedge and these ranges must be revisited.
- Reference magnitudes, all as shares of **consumption expenditure**: housing including energy
  roughly 35%; net cold rent alone closer to 25%; food, drink and tobacco roughly 14–15% —
  noticeably more than the 5–10% often quoted, which is closer to a US figure. Food *alone*,
  excluding drink and tobacco, is nearer 11%.
- The 30–50% housing range is **well above** the data, not merely at its high end: at a 10%
  savings rate it corresponds to roughly 33–56% of consumption expenditure against a German
  figure near 35%, so the top is about 1.5× the national average. Defensible for a model about
  credit-stressed households, but a large departure, and swept rather than defended.
- **Keep one denominator per statement.** The *Mietbelastungsquote* — around 28% on average,
  40%+ in the bottom quintile — is a share of **net income**, not of consumption expenditure.
  Mixing the two silently shifts every comparison by the savings rate.

### 5.1.2 Initial housing tenure

Every household starts as an outright owner, a mortgaged owner, or a renter. The split is
configuration, and it matters more than most starting values because it decides how much of the
town is already exposed to credit before tick 0.

```toml
[housing.initial_tenure]     # profile = "small_town" (default)
owner_outright  = 0.30
owner_mortgaged = 0.30
renter          = 0.40

[housing.initial_tenure.profiles.german_average]
owner_outright  = 0.22
owner_mortgaged = 0.21
renter          = 0.57
```

**Why the default is not the German average.** Germany has the lowest home-ownership rate in
the EU, so a clear majority rent. Quote the measure with the number: the **household**-based rate
is in the low 40s, while Eurostat's **population**-based figure runs to about 47%. Note also that
mortgaged owners *outnumber* outright owners in Germany — Eurostat/EU-SILC puts owners with a
mortgage near 26% and owners without near 22% of the population — which is why the profile above
is 20/23 and not the reverse. That national figure is dragged down heavily by the large cities —
Berlin is under 20% — while
small towns and rural areas run around 55–65%. This model is a *Kleinstadt*, so the small-town
profile is the honest default and the German average is provided for comparison. Both should be
run: the tenure mix is one of the strongest determinants of how a credit expansion distributes,
because owners gain from rising house prices and renters only pay for them.

**Split between outright and mortgaged.** Roughly 46% of German owner-occupiers have no
outstanding mortgage — slightly under half, not over. In reality this tracks age — outright ownership is overwhelmingly older
households — and the model has no demography (§3). Outright ownership is therefore assigned by
*wealth decile* instead, which reproduces the correlation with accumulated assets but loses the
life-cycle story. This substitution is a known distortion: it makes outright owners richer than
they are in reality, which slightly overstates the wealth gap the model reports.

**The ordering of initialisation must be stated, because it is circular as written.** Outright
ownership and the landlord pool are assigned by *wealth decile*, but wealth at t = 0 includes
dwellings — so the assignment needs the answer it is producing. The loader therefore ranks on
**financial** wealth alone (`household_assets_0`, §13.11), assigns tenure and the landlord pool on
that ranking, and only then computes full net worth including property. A second, blunter problem
sits underneath it: at the default `landlord_pool_deciles = 3` the top three wealth deciles are
240 households, which is *also* exactly the number of outright owners — so the two concentration
channels are perfectly rank-correlated by construction, and the model would report an ownership
concentration it had itself assumed. The pool is therefore drawn as a **weighted sample** across
the top deciles rather than as their entirety, with the weighting swept, and the realised overlap
between outright owners and landlords is reported as a diagnostic.

**Existing mortgages must not all start at month one.** A mortgaged owner is initialised with a
remaining term drawn from `initial_remaining_term` and a loan-to-value drawn from
`initial_ltv`, both configurable. Without this the whole town amortises in lockstep and the run
develops a synchronised repayment wave that is a pure artefact of initialisation.

```toml
initial_ltv             = { min = 0.15, max = 0.85 }
initial_remaining_term  = { min = 12,   max = 300  }   # ticks
```

**Who owns the rented dwellings.** This closes an item previously left open. Rented stock is
split between other *households* acting as private landlords and a *rental firm*:

```toml
[housing.landlords]
private_households = 0.65
rental_firm        = 0.35
```

Roughly two-thirds of German rental dwellings are held by private individuals rather than
companies, and the private-landlord case is the more interesting one here: rent flows from tenant
households to landlord households, so a credit-driven rise in house prices redistributes *within*
the population rather than out of it. The rental firm behaves as an ordinary firm (§5.2) with
dwellings as its capital stock.

**Assignment is arithmetically tight and is specified rather than left to the implementation.** At
the defaults, 320 renter households need 320 rented dwellings, of which 208 are privately held. The
rules:

- **Landlords are themselves owner-occupiers.** They must be, or the 30/30/40 tenure split does not
  add up: a landlord household lives in its own dwelling and holds rental dwellings *in addition*.
- Landlords are drawn from the **top `landlord_pool_deciles`** (default 3, i.e. ~240 households) —
  not the top two, which cannot hold 208 dwellings without every one of them being a landlord.
- Dwellings per landlord follow a **Pareto draw** with exponent `landlord_concentration`
  (default 1.6), normalised so the total is exactly the private rental stock. Most landlords hold
  one; a few hold several. This matches the German *Amateurvermieter* pattern and, unlike an even
  split, it makes rental income genuinely concentrated — which is the point, since rental income is
  a channel by which house price rises reach household balance sheets.
- Landlords' rental dwellings may be mortgaged, drawn on the same `initial_ltv` distribution.
- The total dwelling count must equal `n_dwellings`: owner-occupied + rented + vacant. The loader
  asserts this and reports the vacancy rate it had to produce.

### 5.1.3 Share ownership

Households may hold shares in firms, which is how the profit distributed in §5.2.2 reaches them.

| Field | Meaning |
|---|---|
| `shares{firm_id: fraction}` | Ownership stakes held |
| `equity_appetite ε` | ∈ [0,1] — likelihood of directing savings into shares rather than deposits |

**The bank is owned the same way.** Its shares sit in the same `shares{}` field, are valued on the
same `earnings_multiple`, and are allocated by the same concentration parameter. An earlier draft
had a separate `bank_shares` field with no valuation rule and no allocation parameter, which left
the town's single most concentrated asset outside the net-worth definition of §9 — and therefore
outside the wealth Gini, which is C4. There is one share register.

**Initial allocation.** Total shares in each firm are distributed at t=0 with concentration set
by `firm_ownership_concentration`, drawn against the **financial** wealth distribution (§5.1.2). A configurable fraction
of firms may be wholly owner-managed (a single household), which is the realistic small-town case.

**Acquisition.** A household with spare buffer above its `φ` target directs a fraction rising in
`ε` toward buying shares. Purchases are matched against households wishing to sell — those below
their buffer target, under the squeeze order (§6.7), or in arrears. **Shares are therefore a
store of wealth that can be liquidated under pressure**, which matters: a credit-stressed
household sells its stake to a household with surplus, transferring future profit income upward.
That is a distributional channel with no price movement required at all.

**Valuation in v1 is a rule, not a market.**

```
share_value = trailing_earnings × earnings_multiple
```

with `earnings_multiple` a configured constant. There is deliberately **no endogenous share
price** in v1. A traded equity price would be a second asset market that credit can bid up, with
its own bubble dynamics, and it would very likely dominate the housing result and make attribution
impossible. Endogenous share prices are deferred to a later phase, and until then the model
cannot say anything about asset-price inflation in equities — only about who receives the profit
stream.

**Accounting.** A share purchase between households transfers deposits and leaves reserves and
`M` unchanged. Newly issued shares — a firm raising equity instead of borrowing — move deposits
from household to firm, and are the one route by which investment can be financed without credit.
This gives the model an equity-versus-debt margin it otherwise lacks, and it is a genuine
alternative channel worth reporting alongside the credit ones.

### 5.2 Firm

State: sector, capacity, capital stock, inventory, price, workforce, wage bill, **cash**,
**deposits**, loans, markup, owner households.

Firms buy **labour only** — there are no intermediate goods and no supply chain in this model, by
decision. Wages are therefore the whole of a firm's marginal cost.

Firms hold money in both forms, on the same terms as households: cash is part of `M0` and
therefore withdraws reserves from the bank, deposits do not. A firm keeps a working-capital
buffer of `firm_buffer_months` of its wage bill and holds `firm_cash_preference` of that buffer
as cash.

#### 5.2.1 Starting capital

Every firm is initialised by random draw from per-sector ranges, so no two firms begin alike.
Three distinct things are configured, and conflating them is the usual mistake — "capital" in
everyday usage means the money, but what determines how much a firm can produce is the plant:

| Field | What it is |
|---|---|
| `capital_stock` | **Real** productive capacity: machines, premises, vehicles. Not money. Sets maximum output |
| `cash` + `deposits` | **Financial** working capital. Pays wages before revenue arrives. Cash counts against `M0` (§3.1) |
| `initial_debt` | Pre-existing loans, with drawn remaining term and rate. A town where every firm starts debt-free is not a neutral starting point — it is the most favourable one possible for the credit thesis |

```toml
[firms.food]
count         = 4
capital_stock = { min =  80_000, max =  260_000 }
cash          = { min =   4_000, max =   18_000 }
deposits      = { min =  12_000, max =   60_000 }
initial_debt  = { min =       0, max =  120_000 }

[firms.construction]
count         = 2
capital_stock = { min = 400_000, max = 1_200_000 }
cash          = { min =   8_000, max =    30_000 }
deposits      = { min =  30_000, max =   150_000 }
initial_debt  = { min = 100_000, max =   600_000 }
```

Equity follows as the residual: `capital_stock + cash + deposits − initial_debt`. The loader
rejects a draw that produces negative equity and redraws, and reports how often it had to.

**Why the ranges matter.** Identical firms produce degenerate behaviour: they set the same
price, hit capacity together, and reach the wage-cut and capital-call rungs of §5.2 in the same
tick, so the sector never shows any dispersion at all. Dispersion in starting capital is what lets
better- and worse-placed firms diverge — and since firms do not fail (D22), that divergence shows
up where this model can actually use it: in **wages, dividends and capital calls**, which is to
say in household income and its distribution, rather than in an exit event the model does not
have. The **width** of each range is therefore a parameter worth sweeping in its own right, not
just its midpoint.

**Draws are seeded** with the run seed, so a scenario re-run reproduces the same firm population
exactly. Comparing scenarios with different firm draws would confound the comparison with pure
initialisation noise.

Rules:
- **Pricing**: markup over unit cost, adjusted by inventory and capacity utilisation. If
  utilisation exceeds `target_util` and inventory is falling, raise price by up to
  `price_step`; if inventory accumulates, cut. No firm knows the demand curve.
- **Production**: output limited by capacity; unit cost rises above `soft_capacity` as
  utilisation approaches the limit.
- **Investment**: if utilisation stays above target for `n` ticks, invest in capacity —
  financed from retained profit first, then credit. This is the loop back to the growth
  argument: capacity built on credit must be serviced, which requires demand to persist.

  **Investment is bought from the construction sector**, which is its counterparty. This has to be
  said: firms buy labour only, no sector produces capital goods, and an earlier draft therefore had
  investment spending leave a firm's balance sheet and arrive nowhere — an open money leak that
  §10's first consistency check would have aborted the run on, correctly, at the first investment.
  Construction has **two products**: dwellings (§6.5) and productive capacity for other firms. The
  money flows investing firm → construction firm → construction wages → households, with
  `investment_lag` ticks between payment and the capacity appearing. Construction's own capacity
  therefore constrains the whole town's investment, which is realistic and is reported.
- **Employment**: **there is no unemployment in this model.** Every household holds a position at
  all times. Firms expand and contract by *reallocating* positions, not by creating idle
  households.

  **The reallocation rule, stated in full**, because "hire or fire" under a fixed labour force is
  not self-explanatory and demotion is — under D20 — the model's only income shock:

  1. Each firm computes a **desired headcount** from its capacity and utilisation.
  2. Firms wanting fewer positions **release** their lowest-tier positions first, into a pool.
  3. Firms wanting more **claim** from the pool, in descending order of unfilled desire, and
     assign the claimed household to whichever tier is open — which may be lower than the one it
     left. Ties are broken by the tick's seeded shuffle, never by firm index.
  4. **The pool must be empty at the end of the step.** If desired total headcount falls short of
     `n_households`, the residual households are assigned to the firms with the largest gap
     between desired and actual headcount, at `basic` tier. If it exceeds `n_households`, the
     shortfall is simply unfilled desire and firms produce below plan.

  Step 4 is what makes full employment hold as an identity rather than an aspiration. It is also
  an admission: the labour market does not clear on a wage here, it clears by assumption, and the
  wage does the adjusting only through the annual profit review (§5.2.3). Capacity investment
  therefore raises a firm's *desired* headcount and its claim on the pool — it cannot add
  positions to the town, whose total is pinned at `n_households` for the whole run.

  **Why this is an acceptable simplification and what it costs.** Unemployment would be a second
  large shock channel competing with credit for explanatory power, and with no state and no
  benefits (§5.4) an unemployed household would have literally zero income, which would manufacture
  destitution and default at rates that say more about the missing safety net than about credit.
  Excluding it keeps the model's attention on the mechanism under test.

  The cost is that the income-fall route into overextension (§6.8, route 2) now runs entirely
  through **demotion** — a household moved from a management to a basic position takes the
  corresponding income fall while its debts do not change. That is a real mechanism and a
  sufficient one, but it is milder than job loss, so **the model's default rates are conservative
  and cannot be compared with observed data.** State this wherever default figures are reported.
- **Firms do not fail.** This model is about what happens to prices and to consumers; firm
  survival is taken as given. A firm short of money covers the gap in order:

  1. **Wages fall** at the annual review, up to `wage_cut_max`.
  2. **Capital call** — shareholders inject equity from their deposits, pro rata by holding.
  3. If shareholders cannot cover it, a **bank loan at the going rate**, subject to the same
     §7 capacity constraints as any other lending — but **not** to the same standards, which are
     defined for households and would be meaningless here. A firm has no wage income to test DSTI
     against and no dwelling to test LTV against. Its two tests are stated instead:

     - **Coverage:** projected annual operating surplus ÷ annual debt service ≥ `firm_icr_min`.
     - **Gearing:** total debt ÷ `capital_stock` ≤ `firm_gearing_max`.

     A firm failing both is refused, and since it cannot fail (D22) it cuts wages further and
     runs its capital call again — which is the pressure route by which a firm's difficulty
     reaches household income, and the only one this model has.

  Every route is accounted for: the money comes from a household or from the bank, never from
  nowhere. The capital call is the interesting one — it turns a firm's loss into a direct reduction
  of its owners' deposits, a real distributional channel rather than a bookkeeping fiction.

  **What this costs, stated because it is not free.** Removing firm failure removes the channel by
  which a credit boom becomes a bust on the supply side: no insolvency wave, no fire sale of
  productive assets, no cascade. Combined with the exclusion of unemployment (above), this town's
  supply side is close to frictionless. That makes the model **conservative about credit's harms**
  — it cannot produce a crash — so its results are a lower bound on disruption, and any claim about
  instability is outside what it can support. The direction of the bias is acceptable; the fact of
  it must be reported.

#### 5.2.2 Profit distribution

**Firms must pay out, or the model leaks.** In a closed town with no state, household income is
wages only. If firms retain revenue minus wages indefinitely, money accumulates on firm balance
sheets monotonically over 480 ticks, demand falls, and the run deflates — an artefact that would
be attributed to whatever scenario parameter happened to be under test. This is not an
omission to note; it is a hole that has to be closed.

Each tick, after investment is decided, a firm splits its profit two ways:

1. **Retention** — up to `firm_buffer_months` of its wage bill as working capital, plus whatever
   its capacity investment plan requires (§5.2).
2. **Distribution** — a share `shareholder_profit_share`, drawn per firm from a configurable
   range, paid to shareholders pro rata by holding (§5.1.3). The remainder is retained.

```toml
[firms.profit_split]
shareholder_profit_share = { min = 0.40, max = 0.80 }
```

Profit does **not** go to employees. Employees are paid through the wage structure (§5.2.3), which
is where the income distribution inside a firm lives. Profit-sharing with staff
(*Erfolgsbeteiligung*) is uncommon in Germany, so routing profit that way would be both
unrealistic and quietly favourable to the thesis — it would return profit to the households that
spend most of it and suppress the concentration the model is trying to measure.

A firm making losses distributes nothing and runs its buffer down; when the buffer is exhausted it
cuts wages, then calls capital from its shareholders, then borrows (§5.2). **It does not fail**
(D22) — the ladder has no last rung, by design, because this model is about what happens to prices
and consumers, not about firm survival.

**Metrics:** firm profit split retained versus distributed, per sector, every tick.

#### 5.2.3 Workforce and wage structure

Each firm has a headcount and an internal wage hierarchy. This is where most income inequality in
the model originates — more of it than ownership produces, and considerably more than
profit-sharing would.

```toml
[firms.food]
headcount = { min = 3, max = 14 }

[wage_tiers]
management = { share = 0.08, multiple = { min = 2.5, max = 6.0 } }
skilled    = { share = 0.32, multiple = { min = 1.2, max = 1.8 } }
basic      = { share = 0.60, multiple = { min = 0.7, max = 1.0 } }
```

`share` is the fraction of the firm's headcount in that tier; `multiple` is drawn per position
against the firm's **base wage** (`base_wage`, §13.11), which is a configured nominal anchor at
t = 0 and thereafter moves **only** by the annual profit review below. Earlier drafts also had it
move with "sector labour scarcity"; under D20's exact full employment labour scarcity is a
constant, so that clause was a leftover that would have had an implementer building a term that
either does nothing or — worse — does something. It is deleted. A firm's wage
bill is the sum over its positions, so a firm with a top-heavy structure carries a higher cost per
head — which affects its pricing and how early it reaches the wage-cut and capital-call rungs of
§5.2, not only its employees' incomes.

**Calibration.** The multiples above give a top-to-bottom ratio inside a firm of roughly 3–8×,
which is the right order for a small-town business. Large-listed-company ratios (50× and up) are a
different phenomenon and do not belong in a *Kleinstadt* of eighty firms. The tier shares produce
roughly the lognormal shape German earnings data show, with a thin high tail. Both are swept.

**Firm counts and headcounts.** These must reconcile with the population, and an earlier draft's
illustrative `{min 3, max 14}` across "thirty firms" gave about 255 positions against the 800
required — a loader scaling factor of 3.1, which §5.2.3 itself says should be fixed rather than
absorbed. The deeper reason is easy to miss: **this town has no imports**, so it must produce
everything it consumes. Its clothing, electronics, furniture and car "firms" are makers, not
shops, and are correspondingly large. That is not a small-town retail structure, and pretending
otherwise is how the headcounts went wrong.

| Sector | Firms | `headcount` | Positions |
|---|---|---|---|
| Food | 12 | {5, 15} | ~120 |
| Clothing | 6 | {8, 20} | ~84 |
| Electronics | 4 | {10, 30} | ~80 |
| Furniture | 4 | {8, 20} | ~56 |
| Bicycles | 3 | {2, 4} | ~9 |
| Cars | 4 | {15, 40} | ~110 |
| Public transport | 1 | {20, 30} | ~25 |
| Restaurants | 16 | {3, 12} | ~120 |
| Personal services | 14 | {1, 5} | ~42 |
| Leisure | 7 | {2, 8} | ~35 |
| Rental firm | 1 | {2, 6} | ~4 |
| Construction | 6 | {8, 25} | ~99 |
| **Bank** | 1 | 12 fixed | 12 |
| **Total** | **79** | | **~796** |

The **rental firm** and the **public transport operator** are firms like any other — sector row,
headcount, starting capital, owners and profit distribution — and were previously load-bearing
while having none of these. The bank is the 79th employer (§5.3).

**Headcount must reconcile with the population.** Total positions across all firms must
approximate the labour force, and the loader checks this explicitly:

Since there is no unemployment (§5.2), positions and households must match **exactly**: every
household holds exactly one position and every position is filled. The loader scales sector
headcounts to reach that total and reports the scaling factor applied. A factor far from 1.0 means
the configured per-sector ranges are inconsistent with the town's size and should be fixed rather
than absorbed.

#### Annual wage review — and what pins the price level

Every 12 ticks each firm reviews wages against the year's profit:

| Year | Wages | Distribution | Retention |
|---|---|---|---|
| Profitable | Rise by `wage_increment` (default 0.02, scaled by profit relative to the wage bill) | Dividend per §5.2.2 | Remainder to cash |
| Loss-making | Fall by up to `wage_cut_max` (default 0.03) | None | — |

Wages move once a year, not continuously, and they lag: a firm raises pay on *last* year's result.
The lag is deliberate — it is how pay actually behaves, and it means real wages can fall for a year
or more before recovering, without falling forever.

**This closes the wage-indexation gap.** Previously wages responded only to labour scarcity, so
under any positive inflation real wages fell monotonically across 480 ticks and debt burdens rose
mechanically — manufacturing a falling-consumption result out of an omitted channel rather than out
of credit. Now a general price rise raises nominal revenue against a nominal wage bill, profit
rises, and wages follow a year later.

**What actually anchors the nominal price level.** Worth stating plainly, because the spec left it
implicit and the validation gates depend on it. It is **not** the markup rule — markup over cost is
homogeneous of degree one in the price level and pins nothing on its own. The anchor is the
**quantity of money**. In a closed town, if every price doubled while the money stock did not,
households could no longer clear the market at those prices: inventory accumulates, §5.2's pricing
rule cuts, prices fall back. The level is whatever makes the existing money stock sufficient to buy
the output.

Two consequences, both material:

- At `reserve_ratio = 1.0` with no state, broad money is bounded by `M0`, so the price level is
  genuinely anchored and the neutrality check (§10, item 4) is meaningful.
- Under credit creation, broad money moves with the loan book, so the price level moves with it.
  **This is not a defect — it is the mechanism under test.** But it means C1 (the general price
  level) is close to a restatement of the money supply, which is a further reason it stays a
  secondary claim behind the abstainer cohort.

The tier *mix* still matters even at full employment: when a firm contracts, its released
households are reassigned to whatever tier is open elsewhere, so a town whose management positions
are scarce concentrates demotions — and demotion is the model's only income-fall shock (§6.8).

**Assignment.** Households are assigned to positions at t=0 by matching their income decile to the
wage tier, so the initial income distribution follows from the wage structure rather than being
configured separately and then contradicting it. Hiring and firing during the run (§5.2) operate on
positions, and a household that loses a management position and is rehired into a basic one takes
the corresponding income fall — which is a realistic and under-modelled route into overextension
(§6.8, route 2).

### 5.3 Bank

The only lender. Assets: loans, repossessed collateral, reserves (base money in the vault).
Liabilities: household and firm deposits. Equity: the residual. There is no central bank, so
nobody can supply the bank with reserves — see §7.

- **Lending capacity**: bounded by the reserve requirement and, optionally, by capital. Both
  are specified in §7, which is the heart of the model.
- **Lending standards**: a loan is granted if debt-service-to-income stays under `dsti_max`
  and, for secured loans, loan-to-value stays under `ltv_max`. These are the *policy* levers —
  distinct from `θ`, which is the household's *willingness*. Both are needed: credit requires a
  willing borrower and a willing lender, and separating them lets us ask which side drives the
  cycle.
- **Rates**: endogenous, and driven by reserve scarcity (§7.3). The loan rate rises as the bank
  approaches its lending limit; the deposit rate rises with it, because the only way the bank
  can obtain more reserves is to persuade the public to hold less cash.
- **Profit**: interest received − interest paid − write-offs − its own wage bill. Distributed to
  its shareholders in `shares{}`, or retained to equity, per scenario. **Who receives it is a distributional choice
  with large consequences and must be reported, never buried.**
- **The bank is also an employer.** It has a headcount and the same three-tier wage structure as
  any firm (§5.2.3), reviews wages annually against its own profit, and distributes to
  shareholders on the same rule (§5.2.2). Its staff are ordinary households whose income rises
  with the bank's profitability — which means **part of the town's income depends directly on the
  size of the loan book.** That is not an artefact to be apologised for; it is a small, concrete
  instance of the argument the essay makes, and it should be reported: the share of household
  income originating in the bank, as wages and as dividends, across the credit-appetite scenarios.

- **Default**: missed payments trigger forbearance, then repossession of collateral, then
  write-off.

#### 5.3.1 Repossessed goods have a market

Collateral the bank takes has to leave the balance sheet somewhere, or bank equity silently
accumulates in phones and the write-off loop that D6 depends on never closes.

- **Dwellings** re-enter the housing auction (§6.5) as supply, on the same terms as any other
  seller, with the bank's reserve price set at `(1 − liquidation_haircut)` of the last market
  price. In a downturn this makes the bank a forced seller into a falling market, which is
  realistic and which feeds the loss straight back into its own equity and therefore its lending
  capacity.

- **Cars and durables** are offered to households as **second-hand units**, ranked by §6.2 exactly
  like new ones. A used unit is defined by three differences and needs no separate machinery:

  | | Second-hand unit |
  |---|---|
  | Price | Cleared, not fixed. See below |
  | Remaining life | `durability` minus age, so its per-tick `user_cost` is computed over what is left |
  | Status | `status_weight × secondhand_status_factor` (default 0.5) — a used car confers less standing than a new one |
  | Finance | Available on the remaining life, never longer |

- **Unsold after `liquidation_ticks`** the item is written off at zero against equity.

**Used prices have to be able to move, or the channel below does not exist.** An earlier draft
fixed the used price at `(1 − liquidation_haircut)` × the current new price, which makes it a
constant fraction of a price set in the new-goods market and unable to respond to used supply at
all — while the paragraph after it claimed a falling-used-price feedback. The rule is therefore:
`(1 − liquidation_haircut)` is the bank's **opening ask**, marked down by `used_markdown_step` each
tick the unit goes unsold, and the realised price is what a household's §6.2 ranking accepts. Ten
repossessed cars against three interested buyers clear low; one car against ten clears near the
ask. The used price is an **output**.

**This is a genuinely useful addition rather than plumbing.** A second-hand market is the
abstainer's substitute: a household that will not borrow can still obtain mobility by buying used,
at a lower per-tick user cost and a status penalty. So the model can now show whether credit
pushes the cash-paying households *down into the used market* rather than merely making them pay
more — which is a sharper and more concrete version of the primary claim than a price index, and
directly observable in the real world.

**Consequence to watch:** repossessions raise second-hand supply, which lowers used prices, which
lowers the collateral value of everyone else's durables and therefore what the bank will lend
against them. That is a real procyclical channel and — given a clearing used price — it emerges
rather than being coded.

#### 5.3.2 Bank runs — a terminating condition

At any `reserve_ratio < 1` the bank cannot honour all deposits simultaneously. That is not a flaw
in the model; it is the definition of fractional reserve. So the run condition is not "deposits
exceed reserves" — that is true by construction — but **realised withdrawal demand in a tick
exceeding reserves on hand**:

```
cash withdrawals demanded this tick  >  bank reserves
```

When it occurs the run is recorded and **the simulation halts**. It is a terminal event, not
something the model trades through, because everything that follows a run — deposit insurance,
central-bank support, orderly resolution, panic — is outside this model and would have to be
invented.

**A halt is a result, not a failure.** The reported quantity is the tick at which it happened and
the conditions that produced it: *at `reserve_ratio = r` and credit appetite `θ`, this town became
unable to meet withdrawals after N years.* Across the reserve sweep (§8.1) that yields a frontier —
the settings at which the arrangement survives 40 years and those at which it does not — which is
arguably among the more interesting things the project can produce, and it costs nothing beyond
recording the event.

Runs are **emergent, not modelled**: there is no panic dynamic, no contagion, no confidence
variable. Withdrawal demand comes only from households and firms doing what §6.6 already has them
do — moving between cash and deposits as the deposit rate and their buffers dictate. A run here is
therefore an ordinary liquidity failure rather than a psychological event, which makes it a *weak*
test: real runs are driven by exactly the panic this model omits, so **the absence of a run proves
nothing about safety**, while its presence is meaningful. Write-offs hit equity, which under an active capital constraint directly reduces
  lending capacity — the mechanism that turns a wave of defaults into a credit crunch.

### 5.4 State (phase 5, absent in v1)

Taxes income and profit, pays wages to public employees and transfers to households, runs a
deficit financed by bonds. Bonds bought by the bank are subject to the same reserve and capital
constraints as any other asset (§7), so state borrowing competes with private borrowing for the
same finite lending capacity — the crowding-out channel. A separate switch
`state_money_creation` allows the state to issue base money directly, which is the only way
`M0` can change in the entire model and must therefore be reported prominently whenever it is on.

---

## 6. Behaviour

### 6.1 Tick sequence

The order follows a **plan → arbitrate → settle** structure. Nothing is paid until every claim on
the household's income is known, because the choice between paying debt service and consuming
(§6.8) cannot be made before both amounts exist.

**Phase 1 — publish and produce**

1. Bank publishes loan and deposit rates for the tick, from *last* tick's reserve headroom (§7.3).
   At tick 0 it uses the opening balance sheet, which the loader has already validated (§10).
2. Firms set prices and production plans.
3. Firms pay wages; the state, if present, pays transfers.

**Phase 2 — plan (no money moves)**

4. **The rental market clears** (§6.5, *Rent*). Landlords list vacant dwellings at their target
   yield adjusted for vacancy, tenants bid what their §6.2 valuation supports, and the tick's
   rents are set. This step exists because §6.5 replaced the fixed-yield rule with a clearing
   market and §6.1 was never given a place to run it — §6.5 and §6.1 each assumed the other
   handled rent formation, with 40% of the town renting. Sitting tenants' rents follow the
   `sitting_tenant_lag` rule of §6.5 rather than re-clearing every tick.
5. Households compute obligations: debt service due, rent due, subsistence food. Rent is the rent
   set in step 4 — it is **not** derived from any housing price, this tick's or the last, since
   D19 removed the fixed-yield rule.
6. Households form a consumption plan (§6.2) and a savings target (§6.6).

**Phase 3 — arbitrate**

7. Each household resolves obligations against plan (§6.8). If both cannot be met, the
   `π > pressure` comparison decides whether debt service is paid or consumption is protected,
   and the squeeze order (§6.7) determines what is cut. This step produces the
   **strategic-versus-involuntary** classification, which is only possible here because both
   quantities are now known. Saving is a **residual claim**, not a pre-commitment: it is whatever
   survives this step.

**Phase 4 — settle (money moves)**

8. Debt service, rent and subsistence food are paid.
9. Credit applications are submitted — **consumer credit and mortgages together, in one queue**,
   not consumer first. Ordering them would let consumer loans systematically crowd out mortgages
   whenever capacity is scarce, and since housing dominates the price result that ordering would
   silently move the headline number. The bank processes the queue in a **per-tick seeded random
   order** (`application_order_seed`), never a fixed agent order, because under a binding
   constraint the processing order decides who gets credit. The rule is swept: fixed-order runs
   are compared against randomised ones to confirm the result is not an artefact of queueing.
10. Discretionary purchases settle.
11. Housing market clears by auction among movers (§6.5).

**Phase 5 — close the books**

12. Firms observe sales, update inventory, invest or disinvest, and **reallocate positions**
    (§5.2, *Employment*). Firms that cannot pay wages run the §5.2 ladder here: wage cut, then
    **capital call** on shareholders' deposits, then a loan. The capital call is placed at this
    step, after wages have been paid from the buffer, so that a shareholder who is also the firm's
    employee is never asked to fund the wage he has not yet received.
13. Firms distribute profit to owner households (§5.2.2).
14. Households and firms rebalance across cash, demand deposits and time deposits (§6.6). Bank
    reserves move accordingly.
15. Bank **accrues** interest on outstanding balances, processes arrears and defaults, and
    distributes or retains profit. Interest accrual here is on balances *after* step 8's payments;
    the payment and the accrual are separate operations on the same loan and must not both be
    treated as interest, which is the standard double-counting error.
16. Stress, status ranks, and expectations update.
17. **Consistency check** (§10). Abort on violation.

Step 14 sits after all spending deliberately: the bank discovers its reserve position only once
the tick's transactions have settled, which is why rate-setting in step 1 uses last tick's
headroom. The bank cannot see the future, and neither should the code.

### 6.2 The purchase decision

**There is one decision rule and it determines both which goods are bought and how many.** An
earlier draft had two — a per-good ranking here and a per-category euro budget in §6.7 — which
were not derivable from one another and left the actual mechanism undefined. The category budget
is gone. Budget shares are now an *output* (§6.7), not an input.

#### The unit, not the good

The household ranks **candidate units**, not goods. A unit is one dwelling, one car, one phone,
one haircut, one restaurant meal, one week of groceries above subsistence. A durable offers
exactly one useful unit; a service offers many.

#### Everything is measured per tick

**Both sides of the ratio are flows.** This is not a detail: a car delivers its joy over 60 ticks
and a restaurant meal over one, so dividing either by a *total* price makes them incomparable and
loads an arbitrary bias between durables and services into the ranking. Since the financeable
goods are almost all durables, that bias would land squarely on the primary result.

**Value per tick, in euros:**

```
value(g, n) = (joy_g(t) + mobility_value_g(t) + σ · status_gain(g, n)) · n^(−α_g)
```

**Units matter here more than anywhere else in the model.** `value` is denominated in **euros per
tick**, not in utils. An earlier draft had `joy_g` in utils against a `user_cost` in euros, which
made `score` a utils-per-euro quantity, left `λ` with no stateable unit, and — far worse — pinned
the real price level to the joy scale rather than to the money stock. With a fixed utils-valued
`joy` and a threshold in utils per euro, doubling every price halves every score while `λ` does
not move, so demand collapses: a nominal illusion sitting inside the decision rule, which would
have failed §10's neutrality test for a reason having nothing to do with credit.

So `joy_g` is a household's **reservation value in euros per tick at the t = 0 price level**, and
it is **indexed**:

```
joy_g(t) = joy_g(0) · P(t) / P(0)              P = CPI (§9)
```

The same indexation applies to `mobility_value_g` and to the status term, whose weights `w_g` are
likewise euros per tick at t = 0. Every term in `score` is then homogeneous of degree one in the
price level, `score` is dimensionless, and money is neutral by construction — which is what makes
§10's neutrality check a real test of the *implementation* rather than a test of this formula.

Indexation is to the **lagged, town-wide** CPI, not to the price of the good in question. A
household notices that money buys less in general; it does not revalue its taste for phones in
proportion to the price of phones, which would make demand for every good perfectly inelastic and
destroy the experiment.

`mobility_value_g` is non-zero only for transport (§4.1): the fares a means of transport replaces,
`min(capacity_g, mobility_need) · fare`. Mobility supplied beyond the household's need is worth
nothing — it is capacity, not consumption.

`α_g > 0` is configured per sector and determines **quantity**: a household buys restaurant meals
until the next one is not worth its price, not until its money runs out. Firms then have a real
demand curve to observe, which §5.2's pricing rule requires and previously did not have.

**Durables are indivisible and `α` does not apply to them.** An earlier draft claimed the formula
handled this on its own — that "the second identical phone has near-zero marginal value, which
falls out of the formula". It does not: at `α = 0.9` the formula gives the second phone 54% of the
first, and §13.4 lists `α` as *n/a* for durables, so the rule was undefined exactly where the text
said it worked. The stated rule is instead: **a household holds at most one unit per durable
sector**, so `n ∈ {0, 1}` and the exponent is never evaluated. The single exception is dwellings,
which a landlord may hold in quantity; those are valued as an investment (§6.5), not by this
formula. This is a special case, it is small, and stating it is better than a formula that
silently misbehaves.

**Cost per tick — the user cost of the good:**

```
user_cost(g) = depreciation + financing cost + running cost
             = price_g / durability_g
             + (r_l/12 · outstanding)          if financed
               (r_d/12 · price_g)              if bought outright — interest forgone
             + running_cost_g                  per tick, e.g. a car
```

For a non-storable service `durability = 1` and there is no financing or running cost, so
`user_cost` collapses to the price. The same formula therefore covers a dwelling, a car and a
haircut without special cases.

This is the standard user-cost-of-capital construction, and it earns its place here for a specific
reason: **it puts the interest rate inside the comparison between durables and services.** When
credit is cheap, durables become cheaper per tick relative to services and the mix shifts toward
them; when it is dear, the reverse. That channel is real, it is central to what the model is
testing, and the previous total-cost formulation could not express it at all.

#### Settlement: cash first, then deposits

Every payment is made **from cash first**; if cash is insufficient, the remainder is drawn from
deposits. This is how people actually pay, and it is not cosmetic — it determines where base money
sits, which determines the bank's reserves, which determines lending capacity (§7).

The circulation that results: households pay firms in cash, firms keep `firm_cash_preference` of
their buffer as cash and deposit the rest, returning those reserves to the bank. A town that
transacts heavily in cash keeps base money out of the vault for longer and tightens credit —
purely through settlement habits, with no change in anyone's saving. `κ` and
`firm_cash_preference` together govern this and are both swept.

#### The rule

Every candidate unit is scored:

```
score(g, n) = value(g, n) / user_cost(g)
```

Both per tick. Financing enters `user_cost` as `r_l/12 · outstanding` against `r_d/12 · price` for
a cash purchase, and since `r_l > r_d` always, **financing always worsens a unit's score** — which
is correct, since it costs more.

The household buys units in descending order of `score` while two tests pass:

1. **The reservation test.** `score` must exceed `λ`, the household's marginal value of holding
   money. Since `score` is dimensionless, so is `λ`, and it has an interpretation: **`λ = 1` means
   the household will buy anything worth at least what it costs.** Above 1 it demands a margin.
   It is derived, not configured:

   ```
   λ = λ_base · (1 + λ_gap · max(0, (φ − buffer_months) / φ))
   ```

   where `buffer_months` is current financial assets ÷ monthly income. At or above the buffer
   target `λ = λ_base`; a household with an empty buffer demands `λ_base · (1 + λ_gap)`. Being
   scale-free, `λ` needs no indexation — which is the point of having put the price level into
   `joy` instead. This is how saving enters: not as a pre-committed set-aside, but as a
   *reservation price on money itself*.
2. **The affordability test.** A cash purchase must fit available funds now. A financed purchase
   must fit the monthly residual for the term of the loan, and pass the bank's standards (§5.3).

When affordability fails and the unit is financeable, the household takes credit with probability
rising in `θ` and in the gap between desired and affordable consumption.

#### Why this can still refute the thesis

Credit never makes anything look cheaper — financing strictly lowers a unit's score. What it does
is widen the *choice set*: a unit whose lump sum the household cannot cover but whose instalment
it can becomes reachable. A household that would never accumulate €900 can carry €38 a month.

So a liquid household systematically prefers to pay cash, and if the model shows credit changing
little, that is a genuine finding rather than a suppressed one. The mechanism is built to be able
to fail.

### 6.3 Relative status

A household's standing in a good depends on how its holding compares with the town:

```
status_level(g, h) = status_scale(t) · w_g · (rank of h's holding of g within the population − 0.5)
```

`w_g` is a **dimensionless relative weight** — the §4 table's 1.5 for housing against 0.1 for a
haircut says only that housing carries fifteen times the standing, not how much either is worth.
`status_scale` supplies the euros, and it is indexed to CPI like `joy_g` (§6.2). Splitting them
this way is deliberate: the *ordering* of sectors by social visibility is something one can argue
about from observation, while the *level* at which status competes with rent and groceries is a
free parameter that nobody can observe. Keeping them in one number would have hidden the second
inside the first. Since `rank ∈ [0,1]`, a purchase moving a household the whole way up a sector's
distribution is worth at most `status_scale · w_g` per tick, which is the quantity to sweep and
report.

The purchase decision needs the **marginal** quantity, not the level — what the purchase would
*add*:

```
status_gain(g, h) = status_level(g, holding after purchase) − status_level(g, holding now)
```

This distinction is not cosmetic. §6.2 evaluates goods the household does *not* currently own,
and a non-owner sits at the bottom of the rank distribution, so `status_level` there is
**negative**. Using the level would make status subtract from the value of every purchase and
the treadmill would never fire at all.

The marginal form is positive for each household individually while summing to zero across the
population, which is exactly the treadmill: the same phone confers less standing as more people
own one, an older generation loses standing when a newer one is adopted, and **aggregate status
cannot be raised by aggregate spending**. Individually rational purchases therefore produce
demand growth without anyone ending up better off.

**`holding` must be defined per good**, since ownership is binary for most goods and would leave
the ranks massively tied. It is the *generation index* for goods with perceived obsolescence
(§4.2), the assessed value for housing and cars, the count for clothing, and the per-tick
expenditure for services. This is stated per sector in the goods configuration.

Switch `status_relative = false` makes status an absolute constant. The comparison between the
two settings isolates how much of the credit demand is positional.

### 6.4 Stress

```
stress ← clamp( stress + a · debt_service_ratio − b · joy_consumed_this_tick + c · (rank drop) )
```

High stress raises the weight on immediate joy in §6.2 and slightly raises `θ`. This is the
debt→stress→consumption→debt loop. It is a strong assumption: it is switchable
(`stress_feedback`), and any result that depends on it being on must say so.

### 6.5 Housing

The stock is near-fixed, which makes housing the clearest test of credit's effect on price. It is
also the place where a careless auction rule would define the answer instead of deriving it.

#### What a bidder is willing to pay

**A bid is `min(credit_limit, own valuation)` — never the credit limit alone.**

**`credit_limit` is defined here**, since it appears nowhere else and an implementer would
otherwise have to invent it:

```
credit_limit = min( dsti_max · income / instalment_per_euro(term, r_l),
                    ltv_max  · assessed_value )
```

`instalment_per_euro` is the monthly annuity on one euro over the mortgage term at the offered
rate, so the first term is the largest loan whose instalment passes DSTI. The LTV term is
circular as stated — the loan is capped at a fraction of a price the auction has not yet
discovered — and the circularity is broken by a lag: `assessed_value` is the **previous tick's**
clearing price for that quality tier, which is also what a real valuer uses. At t = 0 it is the
tier's configured opening price (§13.11).

If every bidder simply bid their maximum loan, the clearing price would *be* the second-highest
credit limit, and house prices would become a deterministic function of `dsti_max` and `ltv_max`.
Since housing dominates the price result, the model's headline finding would then be defined by
two policy parameters rather than emerging from anything. The credit limit must be a *constraint*
on willingness to pay, not a substitute for it.

Valuation comes from what the dwelling is actually worth to that household:

```
valuation = value_housing(d) · capitalisation_factor
          = (rent_avoided + joy_d + σ · status_gain(d)) · capitalisation_factor
```

**This is the §6.2 `value` function, capitalised — not a second rule.** Earlier drafts had three
rival housing valuations: the §6.2 score, this formula, and a separate renter's
"instalment-plus-running-costs against rent" comparison, with nothing saying which drove the
ranking, which drove the bid, and which drove rent-versus-buy. There is one rule, applied at two
horizons:

- **Per tick, §6.2** decides whether the household wants the dwelling at all, and settles
  rent-versus-buy directly: renting and buying are simply two candidate units for the same need,
  scored the same way, and the higher `score` wins. The separate rent-versus-buy comparison is
  deleted — it was the general rule restated as a special case, and it could disagree with it.
- **As a stock, here**, the same per-tick `value` is capitalised into a bid ceiling. That the
  numerator is *the same object* in both places is a requirement on the implementation, not a
  coincidence: `value_housing` is computed once and used twice.

`rent_avoided` is the prevailing rent for a comparable dwelling — the alternative the household
would otherwise pay — and it is in euros per tick, as is `joy_d`, following §6.2's units.
`capitalisation_factor` reflects the expected holding period and the household's discount rate.

**Three horizons coexist and are not the same quantity**, which is worth stating because they are
easily conflated: `durability_housing = 360` is the **ownership horizon** (how long before a
household moves or substantially renovates), `term_housing = 300` is the **mortgage term**, and
`capitalisation_factor = 240` is the **valuation horizon** net of discounting. Nothing requires
them to be equal and they are not.

**Housing depreciation is physical, not the ownership horizon.** §6.2's generic
`depreciation = price / durability` would charge a €180,000 dwelling €500 a month, which is not
depreciation but amortisation of a holding period — and §4 says explicitly that the 360 ticks are
not the physical life of the building. Housing is the one exception to the generic rule:

```
depreciation_housing = price · housing_depreciation_rate / 12
```

at roughly 1% a year, the maintenance-and-wear rate for a building whose structure persists in the
stock indefinitely. Every other sector uses `price / durability` unchanged.

**Which side binds is a reported output.** If the credit limit binds in nearly every transaction,
that is a genuine and striking finding — credit really is setting house prices. If valuations bind,
prices are demand-driven and credit is permissive rather than causal. The point is that the model
now *measures* this instead of assuming it.

#### Sellers and the option to do nothing

- Sellers have a **reserve price**, their own valuation of continuing to live there, and withdraw
  below it. A seller is not obliged to accept whatever the market offers.
- A household that wants to move may **stay put** if nothing clears above its valuation. Moving is
  a choice, not an event imposed by the model.
- Renters enter the auction only if buying beat renting **in the §6.2 ranking** — the two are
  competing candidate units for the same need, and no separate comparison is needed or permitted.
  With 40% of the town renting, the absence of this decision was a significant gap; making it a
  third formalism would have been a different one.

#### Clearing, and the chain problem

A mover must sell in order to buy, and buy in order to have sold. This is resolved by making the
market clear **simultaneously**, not sequentially:

1. Households decide whether they wish to move.
2. Supply for the tick = dwellings of all willing movers + newly completed construction +
   repossessed stock held by the bank (§5.3).
3. Demand = movers + renters for whom buying beat renting.
4. A single clearing round per quality tier, ascending auction, bids capped at
   `min(credit limit, valuation)`, sellers' reserves enforced.
5. **Settlement is simultaneous.** All transfers execute together, so no bridging finance is
   needed and no household is momentarily homeless or doubly mortgaged. Where a chain cannot
   close, the whole chain fails and those households remain where they are — recorded, since chain
   failure is a real feature of housing markets.

The outgoing mortgage is repaid from the sale proceeds at settlement; any shortfall stays with the
seller as unsecured debt.

#### Rent

Rent is **not** a fixed yield on the sale price. A fixed yield would pass every credit-driven
price rise straight into rents, making renters worse off by definition and largely determining the
distributional result before any agent acted. Real yields compress in booms precisely because
rents do not track prices one-for-one.

Instead the rental market clears between tenant demand and landlord supply (§5.1.2) at step 4 of
each tick (§6.1), and the resulting yield is an *output*. Landlords list **vacant** dwellings at
`rent_target_yield` against the dwelling's assessed value, adjusted down toward vacancy and up
when nothing is vacant; tenants bid what their §6.2 valuation supports; the tier clears.

**Sitting tenants are not re-priced every tick.** A sitting tenant's rent moves toward the
market rent by at most `sitting_tenant_lag` per year, which is the German *Kappungsgrenze* in
model form. This is not decoration: with 40% of the town renting, whether a credit-driven price
rise reaches existing tenants this year or over a decade is one of the larger distributional
questions the model can answer, and making all rents re-clear instantly would answer it by
assumption. Realised yield, the vacancy rate, and the gap between sitting and market rents are
reported.

*Not modelled:* roughly a tenth of German rental stock is cooperative or municipal and priced on a
cost basis rather than a market one. The model has no such landlord, which makes rents more
price-responsive than reality.

#### Construction

Construction adds dwellings with a long lag and only if expected price exceeds build cost.

*Not modelled, and biasing against the thesis:* bidders have no expectation of capital gains.
Extrapolative price expectations are the canonical amplifier in credit–housing models, so omitting
them makes the model conservative. Stated rather than silently relied upon.

### 6.6 Saving, and the cash-versus-deposit choice

Saving enters through two separate channels that must not be conflated, because they push in
opposite directions.

**Channel 1 — withheld consumption (`savings_propensity φ`).** The household targets a
financial buffer of `φ` months of income. Below target it withholds a fraction of income from
discretionary spending; above target it spends more freely and may reduce `φ`-driven restraint
entirely. Higher `φ` therefore *lowers* demand and, other things equal, lowers prices. It also
reduces the need to borrow, since a household with a buffer can pay cash for a good that an
unbuffered household would finance.

`φ` responds to conditions: it rises with the deposit rate `r_d`, with `stress`, and with
recent demotions in the town. It falls when status pressure is high. This makes thrift
partly endogenous rather than a fixed personality trait.

**Channel 1b — the term of the buffer (`τ` `time_deposit_propensity`).** §7.1.1 makes time
deposits the instrument through which lending happens at all under a full reserve, and then says
households move into them "as part of the §6.6 buffer decision" — a decision this section did not
contain. It does now, because without it the decisive `high_credit_gold` control has no loanable
funds and is determined by its own initialisation.

A household splits its buffer between **demand deposits** and **time deposits**. The share placed
on term rises in `τ`, in the premium `r_t − r_d`, and in the amount by which the buffer exceeds
`φ` — money a household expects to need soon is not tied up. Only the excess over `φ` is eligible;
the buffer proper stays liquid.

Four consequences that two implementations would otherwise resolve differently, so they are fixed
here:

- **`deposits` in the §7.1 reserve inequality and in `h_reserve` (§7.3) means demand deposits
  only.** Time deposits carry no reserve requirement — that is the whole point of them.
- A household may **break** a time deposit early, forfeiting accrued interest and paying
  `time_deposit_break_penalty`. Breaking is available in the squeeze order (§6.7) and in arrears,
  after selling shares and before default.
- In the bank-run test (§5.3.2), **unmatured time deposits are not withdrawal demand.** They are
  precisely the liabilities that cannot run, which is why a full-reserve bank funded by them is
  hard to break. A run there requires demand deposits alone to exceed reserves.
- Household state gains a `time_deposits` field alongside `cash` and `deposits`, and step 14 of
  §6.1 rebalances across all three.

**Channel 2 — the form the buffer takes (`cash_preference κ`).** A buffer held as cash removes
base money from the bank's vault and shrinks its lending capacity. The same buffer held as a
deposit leaves the reserves in place. So a town of savers who stuff banknotes under the
mattress and a town of savers who use the bank produce *opposite* effects on credit supply,
from identical saving behaviour.

`κ` falls as the deposit rate rises — the household is paid to give up cash — and rises with
distrust after bank losses. The bank's only lever over its own reserves runs through `κ`: it
cannot create base money, it can only bid for the base money the public is sitting on.

This is the loop that makes §7 self-correcting rather than a hard wall:

```
lending grows  →  reserve headroom falls  →  r_d rises  →  κ falls
               →  reserves rise  →  headroom recovers
```

Whether that loop stabilises the credit cycle or merely delays the crunch is an empirical
question for the model to answer, not something the specification should decide.

**Both propensities are compared, not assumed.** `φ` gets `high_saving` / `low_saving` runs in
the same way `θ` gets `high_credit` / `low_credit`, and the 2×2 of the two is a headline grid:
it separates "people borrow more" from "people save less", which are usually spoken of as one
thing and are not.

### 6.7 Budget shares are an output

The shares drawn in §5.1.1 do two things and only two things:

1. **They set the opening state.** What each household owns at tick 0, what it is contractually
   committed to (rent or mortgage), and therefore where it starts.
2. **They are a reference line for reporting.** Realised share minus initial share, per category,
   is a headline output.

They are **not** consulted when the household decides what to buy. That is §6.2's marginal rule,
and nothing else. Spending on a category is whatever falls out of it.

**This is deliberate and it is what makes the primary claim testable.** A rule that holds
spending at a fixed fraction of income is Cobb-Douglas demand: the housing share of income would
then be constant by construction, whatever happened to house prices — and a rising housing share
is precisely what a credit boom is supposed to produce. Any budget rule strong enough to keep
households near their initial shares would assume away the phenomenon the model exists to observe.

So households **may leave their initial ranges, in either direction, permanently.** A household
that starts at 35% housing may end at 55% because prices rose, or at 25% because it was demoted
and moved. Nothing pulls it back. The divergence is the model's answer to *what did credit do to
how people live*, stated in units a reader recognises from their own bank statement.

What survives from §5.1.1 is only what is genuinely binding:

- **Contractual** commitments — a lease or a mortgage schedule — are real obligations, paid
  before discretionary spending and changeable only by moving or refinancing.
- **Subsistence** food is a quantity floor, bought before anything else. If food prices rise, its
  share rises; it cannot fall below the physical minimum.

The `habitual` and `lumpy` classes are no longer decision rules. Lumpiness remains a *fact* about
durables — a phone is one indivisible unit — and §6.2 handles it without a special class, because
an indivisible unit is simply a unit whose second copy has near-zero marginal value.

**The squeeze order.** When contractual obligations plus debt service leave too little, the
household cuts in this order:

1. Saving — `λ` is not allowed to block spending on necessities.
2. Discretionary units, in ascending order of `score` (§6.2). This is automatic: the marginal
   rule already ranks them, so the cheapest-to-lose units go first without a separate mechanism.
3. Food, down to the subsistence floor and no further.
4. Durable replacements are postponed — goods are kept past their intended replacement date,
   which is recorded, since it is one of the clearest observable signs of stress.
5. Below that, the household defaults on debt service (§6.8).

**The order is not absolute.** Steps 2 and 5 are permeable: a household with low
`payment_priority π` may stop servicing debt while still consuming, rather than cut to the bone
first. That is §6.8.

### 6.8 Overextension and the choice to default

Households are not held to their reference budget shares in either direction. They may
overspend by choice, and default is one of the things that choice can lead to. Default is
therefore **behavioural, not only circumstantial** — a consequence of who the household is, not
merely of what happened to it.

**How a household overextends.** The bank's `dsti_max` and `ltv_max` (§5.3) are tested *at
origination, against income*. Four routes get past them, all of them real:

1. **Stacking.** Each loan passes on its own; together, after the earlier ones are already
   running, they do not. The bank sees the ratio it computes, not the household's future.
2. **Income that later falls.** Demotion to a lower wage tier when a firm contracts and
   the household is reassigned (§5.2). There is no unemployment in the model, so this is milder
   than the real-world equivalent — the loan was prudent when granted, and income fell anyway.
3. **Prices that later rise.** A DSTI ratio says nothing about the cost of living. Rent or food
   inflation can make a compliant loan unaffordable without the loan changing at all.
4. **The residual is not protected.** Passing DSTI leaves a remainder, and nothing obliges the
   household to keep any of it. A household with high `σ` and low `φ` spends the remainder on
   status goods and keeps no buffer at all, so the first small shock is terminal.

Route 4 is the one that matters for the thesis, because it is the route where consumption itself
does the damage. It should be reported separately.

**The decision to stop paying.** Each tick, a household unable to cover both debt service and its
intended consumption compares them:

```
pay debt service   if   π  >  pressure
```

where `pressure` rises with `stress`, with `σ · status_gain` of the purchases that would be
given up, and with how far consumption has already been cut below the reference shares. High `π`
households cut consumption to the bone and keep paying. Low `π` households keep consuming and
fall into arrears. **Both are in the population, and the mix is a scenario parameter.**

**Consequences, without which the model degenerates.** If default is costless every household
overspends and defaults permanently, so the discipline has to be real:

| Consequence | Effect |
|---|---|
| Arrears | Missed payments accrue with penalty interest. Position worsens rather than resets |
| Repossession | After `forbearance_ticks`, secured collateral is taken — the dwelling, the vehicle, the financed durable |
| Credit denial | No new lending for `credit_denial_ticks` after a default |
| Risk premium | `risk_premium` in §7.3 rises with `credit_record` and decays slowly afterwards |
| Forced downgrade | A repossessed dwelling means moving to a cheaper one, or to the rental market at the prevailing price — which in a boom is worse than what was left behind |

There is no debt discharge and no personal insolvency procedure in v1. That is a simplification
and it makes default *more* punishing than reality, which is worth stating: it biases the model
toward households servicing debt, and therefore **against** the thesis. A conservative bias in
that direction is acceptable; the opposite would not be.

**Two kinds of default, reported separately.** The distinction matters more for the essay than
for the model:

- **Involuntary** — the household ran the full squeeze order to the floor and still could not pay.
- **Strategic** — the household had the funds and chose consumption.

"Credit crushed them" and "they chose the phone over the payment" are different claims with
different implications, and an honest piece of writing should be able to say which one the
simulation actually produced. If the model turns out to generate mostly strategic defaults, that
is an argument *against* reading the result as structural coercion, and it gets reported as such.

**The bank learns.** `credit_record` feeds back into origination: a household with past arrears
faces a lower `dsti_max` and a higher rate. The town's default history therefore tightens its own
credit supply, which is a second procyclical channel alongside the capital constraint (§7.2).

---

## 7. Bank lending capacity

The most important setting in the model. It is a **continuous dial**, not a set of named
regimes, and the dial is the reserve requirement.

### 7.1 The reserve requirement — `reserve_ratio` ∈ (0, 1]

The bank must hold base money equal to at least `reserve_ratio` of its deposit liabilities:

```
reserves ≥ reserve_ratio · deposits
```

Granting a loan creates a deposit, which raises the required reserves; the bank may lend only
while the inequality still holds afterwards. Repayment destroys the deposit and releases the
requirement.

**It is a lending rule, not a balance-sheet rule.** The distinction is not pedantic. Three
non-lending operations also raise deposits without raising reserves — paying deposit interest,
crediting bank profit to shareholders' accounts, and **paying the bank's own staff** (§5.3, added
later than this paragraph and just as much an exception) — and at `reserve_ratio = 1.0` any of
them would breach the inequality on its own. Worse, §7.3 pins `r_d` at its maximum exactly when headroom is
lowest, so the breach would be largest precisely where the constraint is meant to bite hardest.
The rule therefore constrains **new lending only**. Interest and profit distribution are always
executed, and if they push the bank below its requirement it enters a **shortfall state**: no new
lending at all until the ratio is restored, and `r_d` at maximum to attract cash. Ticks spent in
shortfall are reported. The maximum broad money the town can support is the classic multiplier

```
M_max = M0 · (1 + c̄) / (c̄ + reserve_ratio)
```

where **`c̄` is the public's aggregate cash-to-deposit ratio, `C/D`**. Note that this is *not*
the household parameter `κ` of §5.1, which is the cash share of financial assets, `C/(C+D)`.
They are different quantities and the formulae differ:

```
c = C/D      →   M = M0 · (1 + c) / (c + reserve_ratio)
κ = C/(C+D)  →   M = M0 / (κ + reserve_ratio·(1 − κ))
```

Substituting an average of the household `κ` values into the first formula gives the wrong answer
at every setting except `reserve_ratio = 1.0`. Convert explicitly: `c = κ/(1 − κ)`.

`M_max` is **illustrative only** — `c̄` is endogenous (§6.6 makes it respond to `r_d`), so it is a
moving target, not a bound the code enforces. What the code enforces is the per-tick inequality
above. Two ends of the dial:

| `reserve_ratio` | Meaning |
|---|---|
| `1.0` | **Full reserve.** Every demand deposit is backed one-for-one by base money in the vault. The bank cannot create money; lending is possible only out of **time deposits** (§7.1.1). This is the commodity-money / gold case |
| `0.20` | Heavy fractional reserve. Deposits up to five times reserves |
| `0.01` | Light fractional reserve — the ECB's actual minimum since January 2012 |

Everything between is available. Sweeping this parameter and plotting the resulting price
paths is one of the headline outputs, because it makes the relationship between money creation
and prices visible as a curve rather than a claim.

#### 7.1.1 Time deposits — how intermediation actually happens

At `reserve_ratio = 1.0` the phrase "lending requires a saver to give up the use of the money"
needs an instrument, or it is not implemented at all. Demand deposits (§3.1) are spendable at
any moment and cannot be lent against under a full reserve rule.

A **time deposit** is therefore available: the household surrenders access for
`time_deposit_term` ticks in exchange for `r_t > r_d`. Time deposits are **not** subject to the
reserve requirement, because the saver has genuinely given up the use of the money — which is
what makes them loanable funds in the strict sense. Households move funds into them as part of
the §6.6 buffer decision, with the share rising in `r_t` and in `φ`.

**Why this is load-bearing.** Without it, `reserve_ratio = 1.0` is not "intermediation" but
"almost no lending at all", because the self-correcting loop of §6.6 is *exactly inoperative*
there: converting cash into a demand deposit raises reserves by X and deposits by X, so headroom
gains `X · (1 − reserve_ratio)`, which is **zero** at full reserve. The loop this document
presents as making §7 self-correcting degenerates precisely at the setting §8 calls decisive.

Without time deposits, lending at full reserve is pinned to the flow of principal repayments,
which is set by the *initialisation* of the loan book (§5.1.2, §5.2.1). The decisive control run
`high_credit_gold` would then be determined by a starting value rather than by `θ` — a
result-predetermining choice hiding in the setup. **The initial loan book size is consequently
added to the Grid C sweep** (§8.3) whether or not time deposits are enabled.

### 7.2 The capital constraint — `capital_constraint` (switch, default on)

A second, independent limit. The bank must hold equity of at least `capital_ratio_min` of its
**risk-weighted** assets:

```
equity ≥ capital_ratio_min · Σ (loan_principal × risk_weight[loan_type])
```

Risk weights follow the EU standardised approach **as amended by CRR3, in force since 1 January
2025**: residential mortgages are no longer a flat 35% but an **LTV-graduated schedule**, roughly
20% at LTV ≤ 50% rising past 70% at high LTV. Consumer loans ("other retail") `0.75`, unrated
corporate `1.0`, repossessed collateral `1.0`.

The LTV grading is not merely more current — it is **strictly better for this model**, because
required capital then falls as house prices rise and rises as they fall. That gives §7.2 the
procyclical amplifier D6 wants, endogenously, rather than relying on write-offs alone.

`capital_ratio_min` must match what `equity` means:

| Interpretation | Threshold | |
|---|---|---|
| `equity` = CET1 — the model's case, since this bank issues no subordinated debt | **0.07** | 4.5% minimum + 2.5% conservation buffer |
| `equity` = total capital | 0.105 | 8% + 2.5%, but includes Tier 2 instruments this bank does not have |

Default is therefore `capital_ratio_min = 0.07`, not 0.105.

**A third constraint, often the binding one.** The Basel **leverage ratio** requires Tier 1
capital ≥ 3% of *unweighted* exposures. For a mortgage-heavy bank at low risk weights this
frequently binds before the risk-weighted ratio does — directly relevant to §7.2's claim that the
model reports which constraint binds. Implemented as `leverage_ratio_min = 0.03` against total
assets, entering the same `min()` in §7.3.

**Repossessed collateral is included in RWA at 100%.** Otherwise foreclosure would *loosen* the
capital constraint — converting a 35%-weighted mortgage into an unweighted asset — inverting the
procyclical feedback this constraint exists to provide. Repossessed assets are marked to the
last observed market price for their type and liquidated over `liquidation_ticks` at a discount
`liquidation_haircut`.

**The loss is marked in two steps, not one.** At **repossession** a loan of principal `P` leaves
the balance sheet and an asset valued `V` arrives; equity moves by `min(P, V) − P` immediately,
which is zero when the collateral still covers the debt and negative when it does not. At **sale**
the residual difference between the carrying value and the realised price is taken. An earlier
draft took the whole loss "on sale", which left equity overstated for up to `liquidation_ticks`
ticks — and since equity drives `h_capital` (§7.3), that overstatement would have let the bank
keep lending through exactly the episode the capital constraint exists to catch.

**If equity goes negative the run is over.** Firms cannot fail (D22) but nothing said the bank
could not, and a negative `h_capital` would stop lending permanently while the simulation carried
on for another two hundred ticks reporting numbers from an economy with no credit — a silent
termination that would not have tripped the bank-run halt of §5.3.2. There is no recapitalisation
route: `bank_equity ≤ 0` **halts the run and is reported as a distinct terminating condition
alongside the bank run**, since it is the capital constraint's version of the same event.

Whichever of §7.1 and §7.2 binds first is the operative limit, and **which one binds is itself
a result the model should report every tick.** This matters beyond the simulation: at a 1%
reserve requirement the reserve constraint is essentially never the binding one in reality, and
EU bank lending is limited by capital instead. Modelling both lets the town be run either as a
textbook multiplier economy or as something closer to a modern banking system, and lets the two
be compared directly.

Note the difference in *speed*, which is what matters — not, as an earlier draft of this document
claimed, that only one of them responds to defaults. Both do. A write-off reduces equity at once,
so the capital constraint contracts lending **immediately**. The reserve constraint contracts it
**cumulatively**: a written-off loan leaves the deposit it created permanently outstanding,
whereas repayment would have destroyed that deposit and released headroom. Defaults therefore
consume reserve capacity too, just slowly and irreversibly.

### 7.3 Price of credit under scarcity

**All interest rates in this specification are annual nominal rates.** `r_l`, `r_d`, `r_t`,
`r_base`, `risk_premium` and every scarcity premium are quoted per year. A tick is one
month, so the per-tick rate applied to balances is

```
r_tick = r_percent / 1200          # r_percent is the annual rate in PER CENT, e.g. 3.0
```

Simple division, not `(1 + r)^(1/12) − 1`, matching how instalment loans are actually quoted and
amortised. This is stated because the omission is a silent 12× error, and because it must be
identical in the loan schedule, the deposit credit and the DSTI test — a mismatch between any two
of them produces a plausible-looking run that is wrong.

**The divisor is 1200, not 12**, because §13.7 quotes every rate in per cent per annum. Read
literally against a `/12` convention, `r_base = 3.0` charges 25% *per month* — the same class of
silent multiplication error this paragraph exists to prevent, reintroduced by the units column.
Rates are stored as per cent throughout and converted exactly once, at the point of application.


Credit does not simply stop at the limit; it becomes progressively more expensive as the limit
approaches. Define headroom as the fraction of lending capacity still unused, under whichever
constraint binds:

```
h_reserve  = 1 − demand_deposits / (reserves / reserve_ratio)
h_capital  = 1 − RWA             / (equity   / capital_ratio_min)
h_leverage = 1 − total_assets    / (equity   / leverage_ratio_min)
h          = min(h_reserve, h_capital, h_leverage)                     h ∈ [0, 1]
```

The denominators are stated explicitly because they differ in kind: the reserve constraint limits
**demand deposits** (time deposits are exempt, §7.1.1), the capital constraint limits
**risk-weighted assets**, and the leverage constraint limits **unweighted assets**. "Current
lending" is not a well-defined common denominator for any of them, and leaving it undefined would
let two implementations of the same spec disagree about when credit tightens.

`h_leverage` is here because §7.2 promises it — it says the leverage ratio "frequently binds
before the risk-weighted ratio does" for a mortgage-heavy bank and "enters the same `min()`" — and
an earlier draft's two-term `min()` meant the constraint could never bind at all, leaving one of
the four values of §9's *which constraint binds* metric unreachable. With mortgage risk weights of
0.20–0.70 under CRR3, a 3% leverage floor against a 7% risk-weighted floor is exactly the case
where the unweighted measure bites first, which is why Basel III added it.

Then

```
r_l = r_base + risk_premium(borrower) + k_scarcity · (1/max(h, h_floor) − 1)
r_d = r_d_base + k_deposit · (1 − h)
```

The loan rate rises steeply as `h → 0` and lending stops outright at `h ≤ 0`. The deposit rate
rises in step, because the bank's only route to more reserves is to bid base money out of the
public's cash holdings (§6.6). `h_floor` prevents a division by zero and caps the rate at a
finite maximum; it must be reported, since it silently sets how sharp a credit crunch can get.

`k_scarcity = 0` recovers a fixed-rate bank, and that is the control run: it isolates how much
of the cycle comes from quantity rationing versus price rationing of credit.

**The shape of `1/h` is violent and has to be calibrated with that in mind.** At `k_scarcity = 4`
the premium is 4 points at `h = 0.5`, 12 at `h = 0.25`, 36 at `h = 0.10` and 76 at `h = h_floor` —
rates at which nobody borrows and no market exists. Nothing in this model restrains how much the
bank *wants* to lend, since it grants any application passing DSTI and LTV, so `h` sits near zero
whenever demand is ample; and at `reserve_ratio = 1.0`, `h_reserve` collapses toward zero as soon
as the bank lends out its slack. The decisive `high_credit_gold` control would then spend most of
its life at the rate cap — failing for a reason that has nothing to do with the thesis.

`k_scarcity` is therefore defaulted to **0.25** (premia of 0.25 / 0.75 / 2.25 / 4.75 points at the
same headroom levels), and the **realised distribution of `r_l` during warm-up is a gate**: a run
whose median loan rate leaves the plausible band is rejected before its results are looked at,
not explained afterwards. A gentler functional form — a premium linear in `(1 − h)` — is available
as `scarcity_form = "linear"` and is swept against the reciprocal, because the choice of curve is
an assumption and not a small one.

### 7.4 Why this is coherent here but not a description of the eurozone

In this town there is **no central bank**. Base money is fixed, nobody can create reserves on
demand, and the reserve requirement therefore genuinely binds — the multiplier logic works.

In the real eurozone it does not work that way round. Banks lend first and obtain reserves
afterwards, and the central bank supplies them elastically; the 1% reserve requirement is not
what limits lending. Both the Bank of England (*Money creation in the modern economy*, 2014)
and the Bundesbank (*Monatsbericht*, April 2017) state this directly.

The model is internally consistent and the mechanism it demonstrates is real. **Any writing
based on it must not claim that this is how the ECB works.** The honest framing is that the
town shows what a fixed quantity of base money does to a credit-driven economy — which is a
claim about the mechanism, not about current monetary institutions.

---

## 8. Scenarios

### 8.1 Grid A — credit appetite × reserve requirement

The main experiment. All other parameters and the seed held constant.

| Scenario | `θ` mean | `reserve_ratio` | |
|---|---|---|---|
| `low_credit_gold` | 0.15 | 1.00 | Full reserve, thrifty town |
| `high_credit_gold` | 0.70 | 1.00 | Full reserve, eager borrowers — **can the thesis hold with no money creation at all?** |
| `low_credit_fractional` | 0.15 | 0.20 | |
| `high_credit_fractional` | 0.70 | 0.20 | The scenario the thesis predicts |
| `low_credit_light` | 0.15 | 0.01 | Completes the grid — without it the `θ` effect cannot be isolated at 0.01 |
| `high_credit_light` | 0.70 | 0.01 | ECB-level reserve requirement |

`high_credit_gold` is the decisive control. If prices rise there too, the mechanism is not
about money creation and the argument has to be restated around competition for a fixed money
stock instead. If they do not, the thesis is monetary and can be stated as such.

Beyond these six, `reserve_ratio` is **swept continuously** from 1.00 down to 0.01 at fixed
`θ`, producing a price-versus-reserve-requirement curve. That curve is the single most
useful chart the project can produce.

### 8.2 Grid B — borrowing × saving

Separates two things usually spoken of as one.

| | `low_saving` (`φ` ≈ 1 month) | `high_saving` (`φ` ≈ 8 months) |
|---|---|---|
| **`low_credit`** (`θ` ≈ 0.15) | `thrifty_town` | `hoarding_town` |
| **`high_credit`** (`θ` ≈ 0.70) | `leveraged_town` | `split_town` |

Run at `reserve_ratio = 0.20`. Note that high saving cuts both ways: it suppresses demand, but
if it is held as deposits it *expands* the bank's lending capacity. The `κ` sweep below
separates those.

### 8.3 Grid C — one factor at a time

- `κ` (cash preference) swept 0.02 → 0.40 at fixed `θ`, `φ`. Isolates the reserve-supply channel
  from the demand channel of saving.
- `capital_constraint` on/off; `capital_ratio_min` swept. Reports which constraint binds.
- `k_scarcity = 0` versus default. Price rationing versus quantity rationing of credit.
- Willingness versus standards: high `θ` with tight `dsti_max`/`ltv_max`, low `θ` with loose.
  Answers whether borrower or lender drives the cycle.
- `rho_theta_phi` = −0.3 versus 0.
- `α_g`, the diminishing-marginal-value exponent per sector (§6.2), swept. It governs how fast a
  price change becomes a quantity change and therefore the effective demand elasticity of the
  whole economy. Likely the most influential parameter in the model, and it has no empirical
  anchor — report its sensitivity prominently. (This replaces `habit_inertia`, which belonged to
  the removed category-budget rule.)
- Budget share ranges (§5.1.1), especially the housing range, swept around their defaults.
- `status_relative` on/off.
- `perceived_obsolescence` on/off.
- `stress_feedback` on/off.
- Phase 5: state absent / balanced budget / deficit financed by the bank.

Each scenario runs `n_seeds = 30` times; reported figures are medians with interquartile bands.
A single run proves nothing.

---

## 9. Metrics

Recorded every tick, written to CSV.

**Prices**
- CPI, all goods, expenditure-weighted
- Index of financeable goods ÷ index of non-financeable goods ← **mechanism check C2b only
  (§1.3), not a primary result.** Confounded by the collinearity of financeability with
  durability, status weight and supply inelasticity; recorded because the confound itself is
  worth seeing, never quoted on its own
- Housing price index; price-to-income ratio
- Price dispersion within each sector

**Transport**
- Modal split: share of households meeting `mobility_need` by car, public transport, bicycle
- Car ownership rate; car loans as a share of the loan book
- Public transport network capacity, utilisation, and fare
- Running cost as a share of income among car owners

**Budgets**
- Realised budget share per category, by income decile
- Households whose realised spending exceeds their reference shares, and by how much
- Realised minus reference share per category ← **how credit changed how people live**
- Share of households in each stage of the squeeze order (§6.7)
- Postponed replacements: goods held past their intended replacement date

**Real economy**
- Real consumption per household, total and by sector
- Capacity utilisation, investment, capital calls on shareholders (§5.2 — firms do not fail, D22)
- Employment and real wage

**Credit and money**
- Loan book / total income
- Household debt-service ratio, mean and 90th percentile
- Default rate, split **involuntary** versus **strategic** (§6.8)
- Overextension route taken, counted separately: stacking / income fall / price rise / no buffer
- Repossessions, by collateral type
- Households under credit denial; mean `risk_premium` by credit record
- Broad money `M` = deposits + public cash; realised multiplier `M / M0`
- Bank reserves; reserve headroom `h`; **which constraint binds** (reserve / capital / standards / demand)
- Loan rate `r_l` and deposit rate `r_d`; the scarcity premium as a separate series
- Public cash-to-deposit ratio `c̄ = C/D` (§7.1 — *not* the household parameter `κ`)
- Aggregate savings rate, and the split between the two channels of §6.6

**The abstainer cohort ← PRIMARY RESULT (§1.2)**
- **(a) Price-only counterfactual — the headline number (§1.2):** the cohort's t=0 basket
  revalued at each scenario's realised prices, cohort nominal income held on the low-credit path
- **(b) Income effect:** realised real consumption minus (a), reported as the residual
- Realised real consumption of the cohort, per household per year — the sum of the two, never
  quoted without them
- Nominal expenditure of the cohort, and the gap between the two
- Quantity the cohort goes without: purchases desired, affordable at the low-credit price, and
  not made at the realised price
- Share of cohort income absorbed by prices set at the margin by borrowers
- Cohort wealth relative to the rest of the town, over time
- **Channel decomposition** (§1.1): the share of the effect attributable to T (timing), M (money
  creation), N (never-would-have) and R (replacement cycle), identified by the switch runs

**Distribution and wealth**

Reported in **real** terms throughout — deflated by the consumption basket. Nominal wealth can
rise while real consumption falls, and that divergence is close to the thesis itself, so a
nominal-only report would obscure the finding.

*Levels*
- Median and mean real net worth; median and mean real consumption per household
- Real net worth by decile, and each decile's share of total
- Real consumption by decile
- Net worth definition, stated once and applied everywhere: deposits + cash + time deposits +
  share value (§5.1.3, **firm and bank shares alike** — the bank is valued on the same
  `earnings_multiple`, and since it is the town's most concentrated asset, omitting it would
  understate the wealth Gini, which is C4) + dwellings at last auction price + durables at
  depreciated replacement cost − outstanding debt. **Housing valuation dominates this measure**, so the sensitivity of
  every wealth statistic to the housing valuation rule is reported alongside it

*Spread*
- Wealth Gini; income Gini; **consumption Gini** — the last is the one closest to lived
  experience and usually the smallest of the three
- P90/P10 and P50/P10 ratios for wealth and for real consumption
- Top-decile and top-percentile shares of wealth

*Poverty and affluence, on stated thresholds*
- **At-risk-of-poverty rate**: households below 60% of median equivalised income — the EU
  standard definition, used so the number is comparable to published figures
- **Material deprivation proxy**: households at step 3 of the squeeze order (§6.7) or beyond, i.e.
  cutting food toward subsistence
- Households with negative net worth
- Households above 200% of median income and above 200% of median wealth, as the "rich" counterpart
- Movement between deciles over the run — how much mobility there is, not just how much spread

*Is the town better off?* — the general-welfare question, answered three ways because no single
measure is honest on its own
- Total real consumption per household (the aggregate answer)
- Median real consumption per household (the typical-household answer, which diverges from the
  mean exactly when distribution is changing)
- Real consumption of the bottom quintile (the answer that matters for the distributional claim)

Where these three disagree, the disagreement is the finding and all three are reported.

*Channels of income*
- Share of household income arriving as wages, as firm dividends, as bank profit, and as rent
  received — separating the ownership channel from the employment channel
- Wage dispersion: within-firm top-to-bottom ratio, between-sector spread, and the share of total
  wage income going to each tier
- Interest paid to the bank as a share of household income ← **C4**
- Bank profit and equity over time

**Wellbeing** (interpret with care — these are model constructs, not welfare claims)
- Mean joy consumed
- Mean stress
- Aggregate status (must be ≈ constant when `status_relative = true`; a useful internal check)

---

## 10. Validation

Before any scenario is interpreted, the model must pass:

1. **Balance-sheet consistency**: `assets = liabilities + net worth`, per agent and in aggregate,
   every tick. Note that "all financial assets equal all liabilities" is **false** here and must
   not be asserted: cash is *outside money* — an asset of the public with no offsetting
   liability — so the aggregate identity is `Σ net financial assets = M0`. Every *inside* claim
   (deposits, loans) does net to zero, and that is asserted separately.
2. **Base money conservation**: `M0` = public cash + firm cash + bank reserves, constant to the
   cent, every tick, at every setting of `reserve_ratio`. This is the invariant that catches
   almost every accounting bug. At `reserve_ratio = 1.0` the correct assertion is `M ≤ M0`, not
   `M` constant: the reserve rule is an inequality, so a bank holding excess reserves may still
   lend and expand `M` without breaching it.
3. **Null run**: **credit stationary** — new lending exactly replacing principal repaid, so the
   loan book neither grows nor shrinks — and the economy must reach a stable state with constant
   prices. A *zero*-lending null run is the wrong test: the town starts with 240 mortgages and
   firm debt (§5.1.2, §5.2.1), and forbidding new lending would amortise that book to zero,
   destroying deposits and producing secular deflation that is an artefact of the test rather than
   of the model. If the stationary run drifts, the drift is a real artefact and must be found
   before anything else is trusted.

   **The lever is not `θ = 0`.** An earlier draft asked for both, which cannot be had: §6.2 grants
   household credit with probability rising in `θ`, so at `θ = 0` new household lending is zero
   and the book amortises — the very artefact this test was rewritten to avoid. The stationary
   condition is imposed directly instead, by a `credit_stationary` switch that caps aggregate new
   lending each tick at that tick's principal repayments and rations it across the queue of §6.1.
   `θ` is left at its normal low-scenario value.
4. **Neutrality check**: scale `M0`, all cash, all deposits, all loan principals, bank equity,
   wages and posted prices by λ **simultaneously**. Nothing real may change. Scaling balances
   alone is not a valid test — `M0` is a fixed constant, so scaling cash without it drives
   `reserves = M0 − cash` negative, and scaling deposits without loan principals silently alters
   every real debt burden. Failure means a nominal illusion is hidden in a decision rule.
5. **Sensitivity**: each headline result is swept across ±50% of every parameter it plausibly
   depends on. A result that survives is reported as robust; one that does not is reported with
   the range in which it holds.

---

## 11. What this model cannot show

Stated here so it can be stated in the essay too. The simulation cannot demonstrate anything
about the actual economy. Its agents are rule-followers, its parameters are chosen, and any
agent-based model can be tuned to produce a desired outcome. What it can do:

- show whether the proposed mechanism is internally **coherent** — whether credit-driven demand,
  positional consumption and lender profit can actually generate the claimed price divergence,
  or whether the story falls apart once every euro has to come from somewhere;
- identify **what the result depends on** — the sensitivity analysis is the real output, more
  than any single number;
- produce a **falsification** if the mechanism does not work, which is worth as much as
  confirmation and will be published either way.

---

## 12. Open decisions

- **Bank monopoly.** One bank makes accounting simple but hands it pricing power, which
  inflates C4 by construction. Competing banks would separate "profit from lending" from
  "profit from market power". Deferred, but the v1 result must be reported with this caveat.
- **Where bank profit goes.** Concentrated in a few households (sharpens the distribution
  result), spread evenly (weakens it), or retained as equity (defers it). This choice largely
  determines C4 and should probably be run all three ways rather than decided.
- **Rate formation beyond scarcity.** §7.3 makes rates respond to reserve headroom, which is
  the mechanism that matters here. Not modelled: term structure, the bank's own expectations,
  or competitive pressure on the spread — there being only one bank.
- **`h_floor`.** It caps how expensive credit can get and therefore how sharp a crunch can be.
  There is no principled value for it. Sweep it and report the sensitivity rather than pretending
  the default is meaningful.
- **Labour supply.** Fixed hours in v1. Making hours endogenous — households working more to
  service debt — would directly address the "who works for whom" question, but adds a
  labour-leisure decision to every household.
- **Panic dynamics in a bank run.** Runs now terminate the simulation when withdrawal demand
  exceeds reserves (§5.3.2), but they are *emergent* — there is no confidence variable, no
  contagion, no panic. A confidence dynamic is easy to tune into producing whatever one wants, so
  it stays out. The consequence: the model's runs are ordinary liquidity failures, real runs are
  psychological, and therefore **the absence of a run in a scenario proves nothing about safety.**
- **No demography.** Outright home-ownership tracks age in reality; the model has no ages and
  assigns it by wealth decile instead (§5.1.2). This makes outright owners richer than they are
  and slightly overstates the wealth gap the model reports. A life cycle would fix it and would
  also give saving and borrowing a motive they currently lack.
- **Whether food subsistence can fail.** Currently recorded as destitution with no further
  consequence. A hard floor would change behaviour near the margin considerably. Less pressing now
  that unemployment is excluded (§5.2), since the zero-income case can no longer arise.
- **No unemployment.** A deliberate exclusion (§5.2), not an oversight: it keeps a second large
  shock channel from competing with credit for explanatory power. The cost is that default rates
  are conservative, because demotion is milder than job loss, and they must not be compared with
  observed data.

---

## 13. Parameter appendix

Every configurable quantity in the model. **A parameter that is not in this table does not
exist**: if the implementation needs one that is missing, it is added here first, with units and
a default, before any code depends on it.

### 13.0 Conventions

| | |
|---|---|
| **Tick** | One month. Everything described "per tick" is monthly |
| **Interest rates** | **Annual nominal, quoted in per cent** (`3.0` means 3% p.a.). Per-tick rate is `r_percent / 1200` — simple division, as instalment loans are quoted. The same convention must hold in the loan schedule, the deposit credit and the DSTI test alike |
| **Prices and values** | Euros. `joy_g`, `w_g` and every `value` term are euros **per tick at the t = 0 price level**, indexed to CPI thereafter (§6.2). `score` and `λ` are dimensionless |
| **Money** | Euro. Integer cents internally; never floating point for balances |
| **Shares and propensities** | Fractions in [0, 1] unless stated |
| **`{min, max}`** | Drawn per agent at initialisation, uniform unless the text says the position within the range is tied to something (income decile, wealth decile) |
| **Swept** | ✓ = varied in the sensitivity analysis (§10, item 5). **⚠** = swept *and* has no empirical anchor, so its sensitivity must be reported prominently alongside any result that depends on it |

### 13.1 Town and run

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `n_households` | Households, one decision-maker each | count | 800 | |
| `persons_per_household` | Reporting only | ratio | 2.4 | |
| `n_dwellings` | Housing stock at t=0 | count | 850 | ✓ |
| `M0` | Base money. Fixed for the whole run | € | **20,000,000** | ✓ |
| `tick_length` | | months | 1 | |
| `run_length` | | ticks | 480 | |
| `warmup` | Discarded from statistics | ticks | **240** | ✓ |
| `seed` | Run seed. The *same* 30 seeds are used across all scenarios, compared paired | int | — | |
| `n_seeds` | Runs per scenario | count | 30 | |

`warmup` was raised from 60. Mortgages run 300 months and `initial_remaining_term` draws up to
300, so 60 ticks cannot clear the initialisation transient. The run log records whether the
transient actually decayed; if it did not, the statistics are not usable regardless of this
setting.

**`M0` was raised from 8,000,000, and the reasoning is worth keeping** because it is the one
parameter that cannot be judged in isolation. Against §13.11's levels the town's wage income is
about €2.1m a month, so €8m was roughly four months of aggregate income while the opening mortgage
book alone is €40m+. That is not by itself impossible — at full reserve the opening book is funded
by **time deposits** (§7.1.1), which carry no reserve requirement, so a large loan book and a
fixed base money stock are perfectly consistent. What `reserve_ratio = 1.0` actually pins is
`demand_deposits ≤ reserves ≤ M0 − cash`, which constrains the **composition** of household claims
rather than their total. At €8m that constraint bound so tightly at t = 0 that the loader could
only satisfy it by handing the bank an implausible equity position, leaving the capital and
leverage constraints slack in every scenario — the failure §3.1 warns about, arriving through the
one door nobody was watching. €20m clears it while still letting the reserve constraint bind at
moderate ratios, which it must, or the main dial does nothing over most of its range.

**Two loader assertions follow**, and they replace the old test that bank equity merely be
non-negative — a test almost nothing fails:

- `demand_deposits ≤ reserves / reserve_ratio` at t = 0, per scenario. Where it fails the loader
  shifts household claims from demand to time deposits until it holds, and **reports how much it
  had to shift**, because that shift is a scenario-dependent difference in the starting state and
  not a neutral one.
- `bank_equity / RWA` at t = 0 must lie within a stated band of `capital_ratio_min`. A bank that
  starts with four times the equity it needs has no capital constraint for the length of the run,
  and would have produced a clean sweep of a dial that was never connected.

### 13.2 Household

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `θ` `credit_appetite` | Willingness to finance rather than wait | [0,1] | `{0.15}` low / `{0.70}` high | ✓ |
| `φ` `savings_propensity` | Target financial buffer | months of income | `{min 1, max 8}` | ✓ |
| `κ` `cash_preference` | Share of financial assets held as cash, `C/(C+D)` | [0,1] | `{min 0.02, max 0.40}` | ✓ |
| `σ` `status_sensitivity` | Weight of relative standing in the purchase decision | [0,1] | `{min 0.0, max 0.8}` | ✓ |
| `π` `payment_priority` | Protection of debt service against consumption | [0,1] | `{min 0.2, max 0.95}` | ⚠ |
| `ε` `equity_appetite` | Share of surplus buffer directed into shares | [0,1] | `{min 0.0, max 0.5}` | ✓ |
| `τ` `time_deposit_propensity` | Share of the **excess over `φ`** placed on term (§6.6, §7.1.1) | [0,1] | `{min 0.0, max 0.6}` | ✓ |
| `λ` | Marginal value of money. **Derived** (§6.2), not configured | dimensionless | — | |
| `λ_base` | Reservation ratio at or above the buffer target. `1.0` = buys anything worth its cost | dimensionless | 1.0 | ✓ |
| `λ_gap` | How much more a household with an empty buffer demands per euro | dimensionless | 1.5 | ⚠ |
| `rho_theta_phi` | Correlation of `θ` and `φ` in the joint draw | [−1,1] | −0.3 | ✓ |
| `schwabe_gradient` | Strength of the falling-housing-share-with-income tie | [0,1] | 0.5 | ⚠ |
| `abstainer_share` | Fraction of households in the abstainer cohort (§1.2) | [0,1] | 0.10 | ✓ |

The abstainer cohort has `θ` pinned at 0 and `φ` at the top of its range, held fixed across every
scenario **and exempt from the §6.4 and §6.6 feedback that would otherwise move both** (§1.2).
Every other parameter is drawn for them exactly as for anyone else, and their realised draws are
reported per scenario so that a cohort that differs from the town in some third respect is
visible rather than assumed away.

### 13.3 Budget shares at t=0 (§5.1.1)

Shares of income, drawn per household, position within range tied to income decile. Initialisation
and reporting reference only — **never consulted during the run**.

| Category | Default range | Binding |
|---|---|---|
| `housing` | 0.30 – 0.50 | contractual |
| `food` | 0.08 – 0.16 | subsistence |
| `clothing` | 0.03 – 0.07 | — |
| `electronics` | 0.01 – 0.04 | — |
| `furniture` | 0.01 – 0.05 | — |
| `transport` | 0.02 – 0.18 | car running cost is contractual while owned. **Bimodal by construction** (§13.11): a public-transport household spends ~2%, a car owner ~18%, and almost nobody sits between — so the *mean* of this share is not a household anyone resembles |
| `restaurants` | 0.02 – 0.07 | — |
| `services` | 0.01 – 0.03 | — |
| `leisure` | 0.04 – 0.12 | — |

The housing range is roughly 1.5× the German average (§5.1.1) and is swept.

### 13.4 Goods and the purchase decision

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `α_g` | Diminishing-marginal-value exponent per sector. **Sets the effective demand elasticity of the whole economy.** Never evaluated for durables, which are indivisible (§6.2) | > 0 | 0.6 services, 0.9 non-durables, n/a durables | ⚠ |
| `joy_g` | Direct value per unit (§6.2). **Indexed to CPI.** Not configured directly — *solved for* at initialisation, §13.11 | € per tick at t=0 | derived | ✓ |
| `w_g` `status` | **Relative** status weight per sector — an ordering, not a level | dimensionless | per §4 table | ✓ |
| `status_scale` | Euros per tick that one full rank of standing is worth. **Indexed to CPI.** The level at which status competes with rent and groceries — unobservable, and the whole positional result moves with it | € per tick at t=0 | 200 | ⚠ |
| `durability_g` | Useful life | ticks | per §4 table | ✓ |
| `financeable_g` | **Per-good switch.** Toggled one good at a time for the C2a experiment (§1.3) | bool | per §4 table | ✓ |
| `term_g` | Loan term when financed | months | 24–300, per §4 table | ✓ |
| `elasticity_g` | Numeric supply elasticity, replacing the "high/medium/low" wording so that §4's grid is derivable from the table | ≥ 0 | **per sector, below** | ✓ |
| `status_relative` | Relative (true) versus absolute status | switch | true | ✓ |
| `status_decay` | Rate at which a generation loses standing once superseded | per tick | 0.02 | ⚠ |
| `perceived_obsolescence` | Whether status decay operates at all | switch | true | ✓ |
| `mobility_need` | Mobility units required per household per tick. **Trips per month, not journeys per lifetime** (§4.1) | units | **20** | ✓ |
| `car_capacity` | Mobility units a car supplies per tick. Excess over `mobility_need` is worth nothing (§6.2) | units | **30** | ✓ |
| `bike_capacity` | Mobility units a bicycle supplies per tick — it cannot meet the whole need alone | units | **8** | ✓ |
| `fare` | Public transport, per mobility unit. `mobility_need · fare` = €60/month is the transport numéraire (§4.1) | € | 3.0 | ✓ |
| `car_running_cost` | Fuel, maintenance, insurance — recurring and unavoidable while owned | € per tick | 180 | ✓ |
| `transport_status_asymmetry` | Whether public transport carries negative status. **An assumption about social meaning, not economics** | switch | true | ⚠ |
| `capitalisation_factor` | Converts a dwelling's per-tick value into a purchase valuation (§6.5). The **valuation horizon**, distinct from the 360-tick ownership horizon and the 300-tick mortgage term | ticks | 240 | ⚠ |

**Supply elasticity per sector**, so that §4's 2×2 grid is derivable from numbers rather than from
the words "high/medium/low" — which it was not, since restaurants and furniture were both "medium"
and landed in opposite cells:

| Sector | `elasticity_g` | Grid cell |
|---|---|---|
| Food | 2.0 | elastic, not financeable |
| Clothing | 1.8 | elastic, not financeable |
| Personal services | 1.5 | elastic, not financeable |
| Electronics | 1.6 | elastic, financeable |
| Furniture | 1.4 | elastic, financeable |
| Bicycles | 1.3 | elastic, financeable |
| **Leisure / holidays** | 1.2 | **elastic, financeable** — the collinearity breaker (§1.3) |
| Restaurants | 0.5 | inelastic, not financeable |
| Public transport | 0.2 | inelastic, not financeable — capacity-limited |
| Cars | 0.6 | inelastic, financeable |
| Housing | 0.05 | inelastic, financeable |
| Construction | 0.4 | — (produces dwellings and capacity) |

Leisure/holidays was missing from §4's grid entirely despite §1.3 calling its price series the
single most informative one in the model. It belongs in the elastic-financeable cell, and the
whole point of it is that it sits there alone among goods of durability 1.

### 13.5 Stress (§6.4)

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `stress_feedback` | Whether stress affects behaviour at all | switch | true | ✓ |
| `a_dsr` | Weight on debt-service ratio | — | 0.15 | ⚠ |
| `b_joy` | Weight on joy consumed | — | 0.10 | ⚠ |
| `c_rank` | Weight on loss of status rank | — | 0.05 | ⚠ |
| `stress_decay` | Per-tick decay toward zero | per tick | 0.05 | ⚠ |
| `stress_bounds` | Clamp | — | [0, 1] | |

`stress_decay` did not exist in earlier drafts, which left stress ratcheting monotonically for any
indebted household. All four weights are unanchored and act on quantities with incommensurable
scales, so **any result that depends on stress feedback must be reported with
`stress_feedback = false` alongside it.**

### 13.6 Firms

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `headcount` | Positions per firm, per sector | count | per sector, scaled to fill the population exactly | ✓ |
| `capital_stock` | Real productive capacity | € | per sector range | ✓ |
| `cash`, `deposits` | Financial working capital | € | per sector range | ✓ |
| `initial_debt` | Pre-existing loans | € | per sector range | ✓ |
| `firm_buffer_months` | Working capital held as a buffer | months of wage bill | 3 | ✓ |
| `firm_cash_preference` | Share of that buffer held as cash | [0,1] | 0.15 | ✓ |
| `target_util` | Capacity utilisation above which price rises and investment begins | [0,1] | 0.85 | ✓ |
| `soft_capacity` | Utilisation above which unit cost starts rising | [0,1] | 0.75 | ✓ |
| `price_step` | Maximum price change per tick | fraction | 0.03 | ✓ |
| `markup` | Over unit cost | fraction | 0.20 | ✓ |
| `shareholder_profit_share` | Distributed to shareholders; remainder retained | [0,1] | `{min 0.40, max 0.80}` | ✓ |
| `firm_ownership_concentration` | Gini of initial shareholding | [0,1] | 0.7 | ✓ |
| `earnings_multiple` | Share valuation = trailing earnings × this. **No market price in v1** | ratio | 12 | ✓ |
| `wage_increment` | Annual rise in a profitable year, scaled by profit ÷ wage bill | fraction | 0.02 | ✓ |
| `wage_cut_max` | Maximum annual fall in a loss year | fraction | 0.03 | ✓ |
| `wage_review_period` | | ticks | 12 | |
| `firm_icr_min` | Operating surplus ÷ debt service at origination (§5.2) | ratio | 1.5 | ✓ |
| `firm_gearing_max` | Total debt ÷ `capital_stock` at origination (§5.2) | ratio | 0.6 | ✓ |
| `investment_lag` | Ticks from payment to capacity appearing. **The only such parameter** — `construction_lag` was a duplicate of the same quantity and is removed | ticks | 6; **24** for dwellings | ✓ |

**Wage tiers** (§5.2.3), multiples against the firm's base wage:

| Tier | Share of positions | Multiple |
|---|---|---|
| `management` | 0.08 | 2.5 – 6.0 |
| `skilled` | 0.32 | 1.2 – 1.8 |
| `basic` | 0.60 | 0.7 – 1.0 |

Both the shares and the multiples are swept. Top-to-bottom of 3–8× is the small-town range;
listed-company ratios of 50×+ are a different phenomenon.

### 13.7 Bank

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `reserve_ratio` | Required reserves ÷ demand deposits. **The main dial** | (0,1] | 0.20 | ✓ swept 1.00 → 0.01 |
| `capital_constraint` | Whether the capital rule is active | switch | true | ✓ |
| `capital_ratio_min` | Equity ÷ risk-weighted assets. CET1 basis: 4.5% + 2.5% buffer | fraction | **0.07** | ✓ |
| `leverage_ratio_min` | Tier 1 ÷ unweighted assets. Often binds first for a mortgage-heavy bank | fraction | 0.03 | ✓ |
| `risk_weight` | Per loan type. Mortgages LTV-graduated per CRR3 | fraction | 0.20–0.70 mortgage, 0.75 consumer, 1.0 firm, 1.0 repossessed | ✓ |
| `r_base` | Base annual loan rate. **`spread` is folded into this** — both were additive constants | % p.a. | 3.0 | ✓ |
| `r_d_base` | Base annual deposit rate | % p.a. | 0.5 | ✓ |
| `r_t_premium` | Time deposit premium over `r_d` | % p.a. | 1.5 | ✓ |
| `time_deposit_term` | Access surrendered for | ticks | 36 | ✓ |
| `time_deposit_break_penalty` | Forfeited on early withdrawal, over and above accrued interest (§6.6) | fraction | 0.01 | ✓ |
| `k_scarcity` | Loan-rate response to falling headroom. `0` = fixed-rate control. **Lowered from 4.0**, which put the rate above 75% at the floor and made `high_credit_gold` fail for reasons unrelated to the thesis (§7.3) | % p.a. | **0.25** | ⚠ |
| `scarcity_form` | `reciprocal` (`1/h − 1`) or `linear` (`1 − h`). The curve is an assumption and is swept | enum | reciprocal | ⚠ |
| `k_deposit` | Deposit-rate response to falling headroom | % p.a. | 2.0 | ⚠ |
| `h_floor` | Floor on headroom in the rate formula. **Silently sets how sharp a crunch can be** | [0,1] | 0.05 | ⚠ |
| `risk_premium` | Added by credit record | % p.a. | 0 – 6 | ✓ |
| `dsti_max` | Debt service ÷ income at origination | fraction | 0.35 tight / 0.50 loose | ✓ |
| `ltv_max` | Loan ÷ collateral value | fraction | 0.80 tight / 0.95 loose | ✓ |
| `forbearance_ticks` | Missed payments tolerated before repossession | ticks | 6 | ✓ |
| `credit_denial_ticks` | No new lending after a default | ticks | 36 | ✓ |
| `liquidation_ticks` | Repossessed collateral held before sale | ticks | 6 | ✓ |
| `liquidation_haircut` | Bank's **opening ask** on repossessed collateral, below last market price (§5.3.1) | fraction | 0.20 | ✓ |
| `used_markdown_step` | Further markdown per tick a used unit goes unsold. Lets used prices respond to used supply | fraction | 0.03 | ✓ |
| `secondhand_status_factor` | Status of a used unit relative to new (§5.3.1) | fraction | 0.5 | ⚠ |
| `application_order_seed` | Per-tick shuffle of the credit queue. **Never a fixed agent order** | int | derived from `seed` | ✓ |
| `bank_headcount` | The bank is an employer like any firm (§5.3) | count | 12 | ✓ |

### 13.8 Housing

| Symbol | Meaning | Units | Default | Swept |
|---|---|---|---|---|
| `initial_tenure` | outright / mortgaged / renter | fractions | 0.30 / 0.30 / 0.40 (`small_town`) | ✓ vs `german_average` 0.20/0.23/0.57 |
| `initial_ltv` | On pre-existing mortgages | fraction | `{min 0.15, max 0.85}` | ✓ |
| `initial_remaining_term` | On pre-existing mortgages | ticks | `{min 12, max 300}` | ✓ |
| `landlord_private_share` | Rented stock held by households rather than the rental firm | fraction | 0.65 | ✓ |
| `landlord_pool_deciles` | Wealth deciles landlords are drawn from | count | 3 | ✓ |
| `landlord_concentration` | Pareto exponent for dwellings per landlord | > 0 | 1.6 | ✓ |
| `move_propensity` | Households wishing to move per tick | fraction | 0.01 | ✓ |
| `seller_reserve_margin` | Reserve price above own valuation | fraction | 0.05 | ⚠ |
| `rent_target_yield` | Landlords' listing target. Realised yield is an **output** | % p.a. | 4.0 | ✓ |
| `sitting_tenant_lag` | Maximum annual move of a sitting tenant's rent toward market (§6.5). The *Kappungsgrenze* in model form | fraction p.a. | 0.15 | ✓ |
| `housing_depreciation_rate` | Physical wear and maintenance. **Housing does not use `price / durability`** (§6.5) | fraction p.a. | 0.01 | ✓ |
| `initial_loan_book_ratio` | Opening loans ÷ `M0`, checked by the loader and swept because §7.1.1 shows the gold run is otherwise set by it | ratio | 2.0 | ✓ |

### 13.9 Scenario switches

| Symbol | Default | Note |
|---|---|---|
| `state_present` | false | Phase 5 |
| `state_money_creation` | false | The only route by which `M0` can change. Report prominently when on |
| `unemployment` | **not implemented** | Excluded by decision (D20). Firms reallocate positions; nobody is idle |
| `firm_failure` | **not implemented** | Excluded by decision (**D22**). Wage cut → capital call → bank loan |
| `intermediate_goods` | **not implemented** | Firms buy labour only; wages are the whole of marginal cost |
| `halt_on_bank_run` | true | Withdrawal demand exceeding reserves terminates the run (§5.3.2). The halt tick **is** the result |
| `halt_on_negative_equity` | true | `bank_equity ≤ 0` terminates the run (§7.2). The capital constraint's version of the same event |
| `credit_stationary` | false | Validation only (§10, item 3): caps new lending each tick at that tick's principal repayments |

### 13.10 Parameters with no empirical anchor

Every ⚠ above, gathered — the list is generated from the tables, not maintained by hand, because
the hand-maintained version had already drifted: `α_g`, `π`, `schwabe_gradient`, `status_decay`,
`transport_status_asymmetry`, `capitalisation_factor`, `status_scale`, `a_dsr`, `b_joy`, `c_rank`,
`stress_decay`, `k_scarcity`, `scarcity_form`, `k_deposit`, `h_floor`, `seller_reserve_margin`,
`secondhand_status_factor`, `λ_gap`.

`status_scale` deserves singling out. With `σ` reaching 0.8 and `w_car = 1.3`, status can be worth
up to €208 a tick on a car whose cost premium over public transport is €420 — so this one number
decides whether the positional channel is decorative or dominant, and **every result involving
the status treadmill must be reported across its swept range.**

These are the model's soft underbelly. A headline result that moves materially across the swept
range of any of them is **not a finding about credit** — it is a finding about that parameter, and
must be reported as such.

### 13.11 Opening levels — the nominal anchor

**Nothing else in this appendix could be sanity-checked until these existed.** Every earlier
table gave ratios, propensities and multiples; not one gave a euro. There was no base wage (though
§5.2.3 quotes multiples "against the firm's base wage"), no opening price for any good, no dwelling
price, and no household opening balance sheet — although §3.1 requires the last of these as
configuration. A reader could not tell whether `car_running_cost = 180` was a large number or a
small one, which is exactly how the transport magnitudes of §13.4 went unnoticed.

| Symbol | Meaning | Units | Default |
|---|---|---|---|
| `base_wage` | The tier-1.0 wage. Every `multiple` in §13.6 is against this | € per month | 2,000 |
| `price_0{sector}` | Opening price per unit, per sector | € | below |
| `dwelling_price_0` | Mean opening dwelling price; dispersed by quality tier | € | 180,000 |
| `dwelling_price_dispersion` | Spread across quality tiers | fraction | ±0.30 |
| `household_assets_0` | Opening cash + deposits + time deposits, in months of own income | months | `{min 0.5, max 18}`, tied to wealth decile |
| `household_cash_share_0` | Of those assets, the share held as cash at t = 0 | [0,1] | drawn from `κ` |

**What this implies, so that it can be checked rather than assumed:**

- Mean wage multiple across the tiers of §13.6 is 1.33, so **mean household income ≈ €2,660/month**
  and the town's wage bill is ≈ €2.13m/month, ≈ €25.5m/year.
- Housing stock 850 × €180,000 ≈ **€153m**. Rent at `rent_target_yield = 4.0%` ≈ **€600/month**,
  which is 23% of mean income — and 43% for a household at the bottom of the `basic` tier. That
  gradient is the German one (Mietbelastungsquote ~28% on average, 40%+ in the bottom quintile),
  and it is reproduced rather than imposed.
- A 300-month mortgage on €144,000 at 3% is ≈ **€683/month**: DSTI 0.26 at mean income, inside
  `dsti_max` at both settings.
- **The deposit is the binding constraint on tenure change, and it is a policy parameter.** At
  `ltv_max = 0.80` a buyer needs €36,000 — 13.5 months of *mean* gross income, and 21 months for
  the basic-tier households who make up most renters — while `φ` tops out at 8 months. At
  `ltv_max = 0.95` the same deposit is €9,000, about 3.4 months, comfortably inside the buffer a
  household already wants to hold.

  **The deposit is best read as a waiting time, and the two settings put it on opposite sides of
  the run.** Nothing in this model *directs* saving toward a house: `φ` is a homeostatic target,
  not an ambition, and §6.2's affordability test for a financed purchase is a **flow** test —
  the deposit is the only place a household must assemble a **stock**. So accumulation past `φ`
  happens only as an undirected by-product of `λ_base`, `ε` and the `joy` calibration. At a
  persistent surplus of €100/month a basic-tier renter reaches the €25,200 deposit on the
  cheapest dwelling in **252 ticks**, and the €6,300 deposit in **63**. The post-warm-up
  measurement window is 240 ticks. Tenure mobility is therefore routine at one setting and
  essentially absent at the other — not because of anything about housing, but because of where
  an unanchored accumulation rate falls relative to the run length.

  **`ltv_max` is consequently doing two jobs at once**, and they must not be conflated in the
  reporting. It is meant to be a *lending standard*, swept to show how credit standards move
  prices. It is also, at these levels, an on/off switch for whether renters can enter ownership at
  all. Sweeping it will produce a large effect that looks like a finding about lending standards
  and is substantially a finding about tenure mobility — the same species of confound as the
  pooled price ratio of §1.3, in a different place.

  **Required reported diagnostics**, alongside any result that moves with `ltv_max`: renters
  crossing into ownership per scenario; the realised distribution of time-to-deposit; and the
  share of the `ltv_max` effect on house prices that survives when tenure transitions are held at
  their low-credit rate.

Opening prices, chosen so that the transport comparison of §4.1 and the budget shares of §13.3 are
both reachable:

| Sector | Unit | `price_0` | Per tick at t=0 |
|---|---|---|---|
| Food | week of groceries | 90 | — |
| Clothing | garment | 60 | — |
| Electronics | smartphone | 600 | 25 (24 ticks) |
| Furniture | item | 700 | 5.8 (120 ticks) |
| Bicycles | bicycle | 600 | 6.3 (96 ticks) |
| Cars | car | 18,000 | 300 + 180 running = **480** |
| Public transport | mobility unit | 3 | **60** for the whole need |
| Restaurants | meal | 35 | — |
| Personal services | visit | 30 | — |
| Leisure | holiday | 1,200 | — |
| Housing | dwelling | 180,000 | 150 depreciation + rent-or-interest |

The car-versus-public-transport gap is **€420 a month**, which is what `joy_car`, status and the
convenience a fare cannot buy have to cover. Whether they do is a result, not a setting — but it
is now a question the model can be asked, which under the old magnitudes (€3/month for the entire
mobility need) it could not.

**`joy_g` is solved for, not chosen.** Nobody can observe what a phone is worth per month in
euros, and inventing twelve such numbers would put the whole consumption pattern in the modeller's
hands. Instead the loader **calibrates `joy_g` at t = 0** so that a household at mean income with
mean `σ`, applying §6.2 against the opening prices above, realises approximately the reference
budget shares of §13.3 — which are Destatis figures. This is what makes §13.3 earn its place: it
is not decoration and not a constraint during the run (§6.7), it is the **calibration target that
pins the one free scale in the decision rule**.

Two things follow and both must be reported. The residual of that calibration — how far the
realised shares sit from the targets once every household is heterogeneous — is a diagnostic, and
a large residual means the functional form of §6.2 cannot reproduce observed German spending and
the model has a problem no parameter will fix. And because the targets are t = 0 shares, **the
model is calibrated to the world as it is, credit and all.** It is not calibrated to a
counterfactual low-credit town, so the low-credit scenario is a genuine extrapolation away from
the calibration point rather than a return to it. That cuts against the thesis if anything, and
is stated so it is not discovered later.
