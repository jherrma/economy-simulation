# Notes

**State — 2026-09-04, second entry.** E0–E9 are built and E10 with them: the engine, the output
writer, the cohort metrics, five validation gates (V2 determinism, V3 neutrality, V4 null run, V5
credit-off regression, V5a the identity archetype table), the campaign runner, and a population of
four archetypes. All green, 531 tests. The campaign is thirteen scenarios over thirty seeds — the
five of §9 plus §3.5's typed sweep, four tables times two arms.

**Two engines produced the numbers below, and the blocks say which.** Everything dated 2026-09-04 in
the first four sections was measured on `6d18abb` (E8's last commit,
`6d18abb0c4bdb623038c39588c51d365a69172c4`) with `tools/Gates … pilot`, a power probe that runs the
two arms over the campaign's own 30 paired seeds at the campaign's own parameters (600 ticks,
measured window from tick 241, `credit_off` against `credit_high`, θ ~ U(0.4, 0.9)). The probe
reading it was `aa614de` for the headline table and `6163635` for the loan-rate sensitivity.

**The typed block at the end was measured on E10's engine**, `3201746` (`32017468a688c208d3b801edd91dd0162f3432f3` — this file's
own commit records the sha of the one before it, which is the engine), with the same
probe over each row of the sweep grid: `pilot table=<row>`. Its `identity` row is the same
measurement as the older blocks' and reproduces them, which is what makes the two engines
comparable.

Re-run any of it with
`git checkout <sha> && dotnet run -c Release --project tools/Gates -- pilot [table=… k=… rate=…]`.

A rough model of what the simulation has said so far — kept for angles, not for accuracy. Every
number here is measured and every one is provisional; `spec/01-SIMULATION.md` §7.1–§10.4 carries the
evidence and the caveats. Short entries on purpose: each is meant to be picked up and turned into a
paragraph or a new question, not cited.

## What the model says about the question — 2026-09-04

**The claim survives, but not as inflation.** The general price level moves +0.55% under
`credit_high`; electronics move +11.0% and appliances +7.5%. The externality is a *relative* price
effect concentrated in exactly the goods credit is used for, which is a sharper and more falsifiable
claim than "credit makes everything dearer".

**B is priced out, not queued out.** A quarter of appliance capacity goes unsold every tick — in
both arms — while abstainers obtain 2.6% of the appliances they want. Nobody takes the last unit off
the shelf; the unit sits there at a price B can no longer reach.

**Nobody gains units.** Town consumption falls 0.36% and both cohorts lose, borrowers included
(−0.35% units, −0.50% quality-weighted). Credit creates no goods, so in a stationary window the
borrower's head start is already spent and what remains of it is debt service.

**The leverage is the finding, not the volume.** New lending is 10% of the town's durable spending;
debt service is 1.27% of all spending and interest 0.18%. A flow that small moves B's access to
appliances by a quarter, because it all lands on the thin margin where B was already losing.

**The transfer objection is conceded and the effect stands anyway.** Interest here is pooled and
paid back pro rata to every household, abstainers included. B is worse off *while collecting a share
of the interest A pays* — the standard "it's a transfer, not a loss" reply is built into the model
and does not save it.

**Dearer credit is gentler credit.** Over `loan_rate` 6.8 → 20 (≈13% → 38% APR) less credit is taken
and the harm shrinks monotonically, from −26.0% to −9.6% on B's appliance access. A cheap-money
angle sits here: the externality is a function of how cheap borrowing is, not of whether it exists.

## Angles that came out of the measurement itself — 2026-09-04

**Aggregates hide distributional harm — and can reverse its sign.** Pooled across categories, the
abstainers' *premium* share rises 2.1% under credit, reading as trading up. Within category it is a
rout: appliance budget share +11.7 pp, electronics +7.1 pp — being priced out of durables pulls
those units out of the denominator and the freed money buys better food.

**Nobody faces the CPI.** The index weights the fixed supply basket, not what anyone bought, so it
barely moves while the prices B actually pays shift hard. Any real-world index has the same
structure, and this is a clean illustration of why a flat headline number is compatible with a group
getting badly squeezed.

**Relative prices converge eight times slower than the level.** The CPI settles in ~30 ticks; the
individual tier prices take 240+, and at tick 120 one was still 4.5% from its resting point. An
observer who measures the price level and declares the adjustment over will miss the entire
reallocation, which is still running.

**One cent decorrelates a run.** Add a cent to mean income and change nothing else: identical for
three ticks, then ~48% of cells differ. Individual outcomes here are genuinely unpredictable and
only the distribution is stable — worth saying out loud in a debate that usually argues about
representative agents.

**Scarcity here is monetary, not physical.** Capacity goes unused while households want the goods,
so the binding constraint is cash. The model quietly makes a strong claim: in a demand-constrained
market, who holds purchasing power decides who consumes, and credit is a way of reassigning it.

**A flat rate looks lenient and is not.** `loan_rate = 8.0` is an add-on rate on the opening
principal, which is ~15% effective APR over the 24-month term. The gap between the quoted and the
effective number is itself the BNPL story in miniature.

## What the model cannot say (and where a critic will push) — 2026-09-04

**Supply is fixed by construction.** So the "credit funds additional output" reply cannot even be
stated inside this model, let alone tested. The result is conditional on inelastic supply and must
always be reported that way — it is the first thing an economist will say.

**The steady state has no room for the timing channel.** The measured window holds a constant loan
stock, so the borrower's "buy it now" advantage was consumed during the warm-up and never appears.
If the interesting claim is about transitions, this design measures the wrong thing.

**Credit supply never binds.** The pool funds every loan on all sixty runs, so the lending-capacity
channel is inert at these parameters. Whether that is realistic is a modelling question nobody has
argued yet.

**`k` sets the price level, not just the speed.** Over a tenfold range the baseline CPI runs 1.03 to
1.43 and B's share obtained 0.78 to 0.49. Every number must be reported as a *difference* with a
band; no baseline level here is calibrated against anything real.

## Open, untested, worth doing — as of 2026-09-04

- `credit_low` against `credit_high`: is the harm monotone in credit appetite, or is there a
  threshold? A threshold would be a much better story than a slope.
- `credit_high_no_money_creation`: separates "more money chasing the goods" from "the same money
  reassigned". Money stock +0.44% against CPI +0.55% hints the two are comparable in size, which
  would be a genuinely surprising result if it holds.
- `credit_high_willingness_rationing`: when the shelf is short, does it matter *who* gets picked?
  This is where a fairness argument would live.
- Nothing yet measures the price index B actually faces — only `cpi_*` and per-category shares. The
  cohort-weighted number is the one a reader would intuitively want.
- Untested: whether the harm concentrates on the poorest abstainers or spreads evenly across the
  cohort. §10.1 says the excluded households sit at the poor end (median income ~€400 against a mean
  of €650), so this is likely the strongest version of the finding and it is not yet measured.

## What a typed population does to the answer — 2026-09-04

E10 is built: households now come in four archetypes, each with a per-category taste level `w` and a
per-category quality steepness `kappa`, where v1 had one number for both. The five tables of
`02-PARAMETERS.md` §3.5 were named before the first typed run. `spec/01-SIMULATION.md` §10.5 carries
the evidence; this is the shape of it.

**The claim splits in two, and the halves behave differently.** The *price* effect is untouched —
electronics +11.0% under v1's population, +11.7% under the typed one; appliances +7.5% against
+6.6%. The *access* effect halves: the abstainer's share of wanted units obtained falls 5.61% under
v1 and 3.08% under the typed table. Both still resolve at eight to twenty times what thirty seeds can
distinguish from zero, so this is a smaller effect and not a lost one.

**Lead with the price, not with access.** That is the practical consequence for the write-up. "The
same good becomes more expensive for B" survives a population that wants genuinely different things;
"and harder for B to get" survives it at half the size. Both are true; one is sturdier.

**The obvious objection to the typed table turns out to be wrong, and it was worth pre-registering.**
The typed table's average household cares less about quality than v1's implicit one, which could by
itself have put more households on the cheap shelves and produced the shrinkage without any
heterogeneity at all. The `typed_kappa_neutral` control rescales that away and gives −3.081% against
`typed`'s −3.084%. It is heterogeneity, not the level. Had the control not been in the grid, the
honest thing would have been to say the delta could not be read.

**Why the access effect shrank: there is less borrowing.** A quarter to a third less credit
outstanding at the same θ. The financing decision is a threshold and spreading taste around its mean
does not conserve the count of households on the far side of it. Worth a paragraph in the write-up:
it says something about how a heterogeneous population uses credit that a representative-agent
version of the same model could not say.

**New and untested.** With types, the abstainer's *quality* index rises slightly under credit
(+0.31%, t = 15.6) while their unit count is flat — they get about as many things and the things are
marginally better. Consistent with exclusion falling on the cheap marginal purchases first. Not
tested; do not quote it as though it were.

---

---

*Convention: entries are dated because they age. A finding here is true of the model as it stood on
that date and at those parameters, and E9 or a recalibration can move any of them. New findings get
a new dated block rather than an edit to an old one, so a claim that changed its mind stays
visible.*
