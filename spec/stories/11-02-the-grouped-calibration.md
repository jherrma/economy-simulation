# The grouped calibration, authored by base score

**Epic:** E11 — Product groups and replacement cycles
**Depends on:** 11-01
**New ground:** `v` derived from an authored base score; a second calibration as a config file

## Story

As the model author, I want the eighteen-good table authored by base score and shipped as a configuration, so that every value weight in it can be argued about and the v1 findings keep the table they were measured on.

## Acceptance criteria

- [ ] `base_score` is a goods-table field and `v` is **derived**: `v_g = base_score_g · price_ref_g / (life_g · mean_income)`. A configuration that states `v` must state the derived value or be rejected — the rule `capacity` already obeys.
- [ ] A test asserts nominal neutrality survives the derivation: scaling `price_ref` and `mean_income` together leaves every `v_g` unchanged (V3).
- [ ] `config/calibrations/grouped.toml` holds the eighteen goods of `02-PARAMETERS.md` §3.6. **The default stays §3.1's six categories.**
- [ ] A test asserts the first calibration identity on the grouped table: `Σ_g price_ref_g / life_g = mean_income`, to the cent.
- [ ] A test asserts §3.4's three design requirements on the grouped table — essentials top the ordering at €300; **the four large financeable durables** (phone, laptop, medium and large appliances) have a standard upgrade between 0.95 and 1.00 at the median; no premium candidate clears λ at the median. It must name those four: hobby equipment is 0.896, TV 0.880 and big kit 0.816, so a criterion phrased over "the financeable durables" rejects the table in §3.6.
- [ ] A test reproduces the opening-pressure figures of §3.6 on **both** calibrations by the same method, and asserts the grouped table opens three to four points *lower* rather than equal — 80.1% against 77.1% at `w = 1`. The figures are for `w` fixed at 1 and count (household, good) decisions; both facts are stated where the numbers are.
- [ ] `financeable` and `term` are per good: phone, laptop, TV, hobby equipment and big kit, medium and large appliances; **not** small appliances, and nothing in food, leisure or clothing.

## Where to start

Deriving `v` from a base score is the story's one real idea and it is worth being clear about why. Nobody has an intuition about `v = 0.0141`; everybody has one about "this good scores 1.10 for a household on the mean income, so the median buys the cheap one and a household on €900 buys the middle one". The authored number should be the one a reader can disagree with, and the derived number should be the one the engine uses.

The three design-requirement tests are the guard against the thing that would quietly ruin this epic. Eighteen base scores is a lot of freedom, and freedom plus a headline is how a calibration gets nudged. Encoding §3.4's requirements as assertions means the table can be argued about openly and cannot be adjusted silently.

Do not delete or supersede §3.1. Every number in §10 was measured on it, and a finding whose calibration has been replaced underneath it is a finding nobody can check.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~CalibrationTests
dotnet run --project tools/Gates -- neutrality
```

The identity holds to the cent; the three design requirements hold; V3 unaffected by the derivation.
