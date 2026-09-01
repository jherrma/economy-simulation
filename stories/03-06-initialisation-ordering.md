# Initialisation ordering, and breaking the tenure circularity

**Epic:** E3 — Configuration and opening state
**Depends on:** 03-04
**New ground:** Resolving a definition that refers to its own result

## Story

As the model author, I want tenure and the landlord pool assigned on financial wealth, before property is valued, so that an initialisation that is circular as written becomes a defined sequence.

## Acceptance criteria

- [ ] Households are ranked on **financial** wealth alone (`household_assets_0`) for the purpose of assigning tenure.
- [ ] Tenure (outright / mortgaged / renter) is assigned on that ranking, per `initial_tenure`.
- [ ] The landlord pool is drawn from the top deciles as a **weighted sample**, not as their entirety.
- [ ] Full net worth, including dwellings, is computed **after** tenure is assigned — never before.
- [ ] The realised overlap between outright owners and landlords is reported as a diagnostic.
- [ ] A test asserts the two groups are not identical by construction at the default settings.

## Where to start

§5.1.2 assigns outright ownership and the landlord pool by wealth decile, but wealth at t = 0 includes
dwellings — so the assignment needs the answer it is producing. Ranking on financial wealth first breaks
the loop, and it is also closer to what the substitution was standing in for: §5.1.2 uses wealth as a
proxy for age, since the model has no demography.

The weighted-sample requirement addresses a blunter problem underneath it. At the defaults the top three
wealth deciles are 240 households, which is *also* exactly the number of outright owners — so the two
concentration channels would be perfectly rank-correlated by construction, and the model would report an
ownership concentration it had itself assumed.

Report the overlap every run. It is the kind of number that looks fine until someone changes
`landlord_pool_deciles` and quietly recreates the degeneracy.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~TenureInitTests
```

The reported overlap. If outright owners and landlords coincide exactly, the weighted sample is not
weighting.
