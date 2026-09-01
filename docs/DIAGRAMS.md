# Diagrams

Four views of the model and the engine. All are Mermaid, so they render on GitHub, in Obsidian and
in most editors without a toolchain, and they diff as text.

`§` refers to a section of [`MODEL.md`](MODEL.md); `D<n>` to [`DECISIONS.md`](DECISIONS.md);
story ids to [`../stories/`](../stories/).

1. [The tick](#1-the-tick) — the pipeline, and which phases move money
2. [Value types and service boundaries](#2-value-types-and-service-boundaries) — the only class diagram that earns its place
3. [Data layout](#3-data-layout) — why there is almost no object graph to draw
4. [Every entity in the simulation](#4-every-entity-in-the-simulation) — the domain model

---

## 1. The tick

§6.1, seventeen steps in five phases, following a **plan → arbitrate → settle** structure. Nothing
is paid until every claim on a household's income is known, because the choice between paying debt
service and consuming (§6.8) cannot be made before both amounts exist.

**Read the shading carefully.** It is tempting to say "money moves in phase 4", and that is false:
wages are paid at step 3 in phase 1, and phase 5 settles capital calls, dividends, rebalancing and
interest. The genuinely quiet phases are **2 and 3** — that is the invariant story 04-05 tests.

```mermaid
flowchart TD
    subgraph P1["Phase 1 — publish and produce"]
        S1["1 · Bank publishes r_l and r_d<br/>from <b>last</b> tick's headroom"]
        S2["2 · Firms set prices and production plans"]
        S3["3 · Firms pay wages"]
        S1 --> S2 --> S3
    end
    subgraph P2["Phase 2 — plan · no money moves"]
        S4["4 · Rental market clears<br/>sitting tenants lag toward market"]
        S5["5 · Households compute obligations<br/>debt service · rent · subsistence"]
        S6["6 · Households form a consumption plan<br/>and a savings target"]
        S4 --> S5 --> S6
    end
    subgraph P3["Phase 3 — arbitrate · no money moves"]
        S7["7 · Resolve obligations against plan<br/><b>strategic vs involuntary</b> default decided here"]
    end
    subgraph P4["Phase 4 — settle · household spending"]
        S8["8 · Debt service, rent, subsistence paid"]
        S9["9 · Credit applications<br/><b>one queue</b>, seeded random order"]
        S10["10 · Discretionary purchases settle"]
        S11["11 · Housing auction clears<br/>simultaneous settlement"]
        S8 --> S9 --> S10 --> S11
    end
    subgraph P5["Phase 5 — close the books"]
        S12["12 · Firms invest, reallocate positions,<br/>run the wage-cut → capital-call ladder"]
        S13["13 · Firms distribute profit"]
        S14["14 · Rebalance cash / demand / time deposits"]
        S15["15 · Bank <b>accrues</b> interest, processes<br/>arrears and defaults, books profit"]
        S16["16 · Stress, status ranks, expectations update"]
        S17["17 · <b>Consistency check</b> — abort on violation"]
        S12 --> S13 --> S14 --> S15 --> S16 --> S17
    end
    P1 --> P2 --> P3 --> P4 --> P5
    S17 -.->|next tick| S1

    classDef moves fill:#f6d5d0,stroke:#b4553f,color:#3a1a12
    classDef quiet fill:#dfe8f5,stroke:#4a6fa5,color:#12203a
    classDef gate  fill:#d8ead8,stroke:#3f7d3f,color:#123312
    class S3,S8,S9,S10,S11,S12,S13,S14,S15 moves
    class S4,S5,S6,S7 quiet
    class S17 gate
```

Four orderings in that diagram were defects found in review rather than design intentions, and each
is load-bearing:

| Ordering | Why |
|---|---|
| Rates at step 1 use **last** tick's headroom | The bank discovers its reserve position only after the tick settles at step 14. It cannot see the future, and neither should the code |
| Rent set at step 4, **before** obligations at step 5 | §6.5 replaced the fixed-yield rule and §6.1 had no step to run the replacement in — the two sections each assumed the other did it, with 40% of the town renting |
| Accrual at step 15 on balances **after** step 8 | Payment and accrual are separate operations on the same loan. Treating both as interest is the standard double-counting error |
| Capital call at step 12, **after** wages | Otherwise a shareholder who is also the firm's employee is asked to fund the wage he has not yet received |

---

## 2. Value types and service boundaries

The only class diagram worth drawing, because it is where the type system carries the argument from
D27: the four defect classes both reviews kept producing are turned into build failures here.

```mermaid
classDiagram
    class Money {
        <<readonly record struct>>
        +long Cents
        +FromEuros(decimal, Rounding) Money
        +SplitProRata(weights) Money[]
    }
    class Rate {
        <<readonly record struct>>
        -double percentPerAnnum
        +PerTick() double
    }
    class RngStream {
        <<struct>>
        +Derive(seed, agentId, purpose) RngStream
        +NextDouble() double
    }
    note for Money "No conversion from double. Pro-rata splits sum back exactly."
    note for Rate "The only exit divides by 1200."
    class Ledger {
        +Pay(from, to, Money) Result
        +DepositCash(agent, Money) Result
        +GrantLoan(borrower, terms) Result~LoanId~
        +CheckBaseMoney() Result
        +CheckInsideClaims() Result
    }
    class PurchaseDecision {
        <<hot path · no allocation>>
        +Value(good, n) Money
        +UserCost(good, financed) Money
        +Score(good, n) double
        +Lambda(household) double
    }
    class LendingCapacity {
        +Headroom() double
        +BindingConstraint() Constraint
        +LoanRate(borrower) Rate
    }
    class MetricsWriter {
        <<boundary · CSV or Parquet only>>
        +WriteTick(tick, series) Result
        +WriteCompletionMarker() Result
    }
    class ValidationGates {
        +BalanceSheet() Result
        +Neutrality() Result
        +NullRun() Result
        +WarmupTransient() Result
    }

    Ledger ..> Money : moves
    Ledger ..> Result : reports failure
    PurchaseDecision ..> Money : values in
    PurchaseDecision ..> Rate : financing cost
    LendingCapacity ..> Rate : prices
    LendingCapacity ..> Ledger : reads balances
    ValidationGates ..> Ledger : asserts against
    MetricsWriter ..> ValidationGates : records gate result
    PurchaseDecision ..> RngStream
```

Note what is deliberately absent. `Result` appears at the boundaries — the ledger, the writer, the
gates — and **not** inside `PurchaseDecision`, because nothing on that path can fail for a domain
reason and `Result<T>` allocates (story 01-04). A bad value there is a defect, not an outcome.

---

## 3. Data layout

There is almost no object graph in this engine, and that is a decision rather than an omission:
`LANGUAGE-CHOICE.md` §7 commits to agents as **integer indices into flat parallel arrays**. Cache
locality is most of the measured gap between the compiled languages, and retrofitting it after E5
would mean rewriting the hot loop.

So the interesting structure is not "which class points at which" but "which columns does the tick
walk".

```mermaid
flowchart LR
    subgraph H["Households · struct-of-arrays, indexed 0..799"]
        direction TB
        HC["Money[] Cash"]
        HD["Money[] DemandDeposits"]
        HT["Money[] TimeDeposits"]
        HTh["double[] Theta · Phi · Kappa · Sigma"]
        HA["bool[] IsAbstainer"]
        HE["int[] EmployerId · int[] WageTier"]
    end
    subgraph F["Firms · indexed 0..77"]
        direction TB
        FP["Money[] Price · Money[] Cash"]
        FK["Money[] CapitalStock"]
        FH["int[] Headcount · int[] Sector"]
    end
    subgraph B["Bank · a single record"]
        BR["Reserves · Loans · Deposits · Equity"]
    end
    LOOP["§6.2 scoring loop<br/>800 × ~120 candidates × 480 ticks<br/><b>zero allocation</b>"]
    H --> LOOP
    F --> LOOP
    LOOP --> B

    classDef arr fill:#eef2f7,stroke:#4a6fa5,color:#12203a
    classDef hot fill:#f6d5d0,stroke:#b4553f,color:#3a1a12
    class HC,HD,HT,HTh,HA,HE,FP,FK,FH,BR arr
    class LOOP hot
```

An agent is an **integer**, not a reference. A household does not hold a pointer to its employer; it
holds `EmployerId`, and the firm's data is `firms.Price[employerId]`. The same applies to loans,
shares and tenancies, which are records carrying integer ids on both sides.

```
Household 0    Household 1    Household 2   …      ← what an object graph would give you
[cash|dep|θ|φ] [cash|dep|θ|φ] [cash|dep|θ|φ]         (a pointer chase per field)

Cash        [ h0 | h1 | h2 | h3 | … | h799 ]      ← what this engine uses
Deposits    [ h0 | h1 | h2 | h3 | … | h799 ]         (one contiguous sweep per column)
Theta       [ h0 | h1 | h2 | h3 | … | h799 ]
```

If someone later "tidies" this into a `Household` class, the benchmark gate in story 05-07 is what
should catch it.

---

## 4. Every entity in the simulation

The domain model: every agent, every real asset, and every claim that connects them. Multiplicities
are the defaults from §13.

```mermaid
classDiagram
    direction TB

    class Household {
        count : 800
        params : theta phi kappa sigma pi epsilon tau
        stress and credit record
        abstainer flag
    }
    class Firm {
        count : 78 across 12 sectors
        price and capital stock
        inventory and headcount
    }
    class Bank {
        count : 1
        reserves and equity
        also an employer
    }
    class State {
        phase 5 : absent in v1
    }

    class Dwelling {
        count : 850
        quality tier
        assessed value
    }
    class Durable {
        car phone furniture bicycle clothing
        generation and age
    }

    class Position {
        tier : management 8 percent
        tier : skilled 32 percent
        tier : basic 60 percent
    }
    class Shareholding {
        fraction held
    }
    class Loan {
        kind : mortgage consumer firm
        principal rate term
        risk weight
    }
    class Tenancy {
        rent and sitting-tenant lag
    }
    class Cash {
        base money, bearer
        part of M0
    }
    class DemandDeposit {
        reserve-requiring
    }
    class TimeDeposit {
        NO reserve requirement
        term and break penalty
    }
    note for TimeDeposit "Exempt from the reserve inequality. This is what funds lending at full reserve."

    Household "1" --> "1" Position : holds — exactly one, always
    Firm "1" --> "3..40" Position : offers
    Bank "1" --> "12" Position : offers

    Household "1" --> "0..*" Shareholding : owns
    Shareholding "0..*" --> "1" Firm : stake in
    Shareholding "0..*" --> "1" Bank : stake in

    Household "1" --> "0..*" Dwelling : occupies or lets
    Household "1" --> "0..*" Durable : holds
    Bank "1" --> "0..*" Durable : repossessed, resells
    Bank "1" --> "0..*" Dwelling : repossessed, resells

    Household "1" --> "0..*" Loan : borrower
    Firm "1" --> "0..*" Loan : borrower
    Loan "0..*" --> "1" Bank : creditor
    Loan "0..1" --> "1" Dwelling : secured on
    Loan "0..1" --> "1" Durable : secured on

    Tenancy "0..*" --> "1" Dwelling : of
    Household "1" --> "0..*" Tenancy : landlord
    Household "1" --> "0..1" Tenancy : tenant

    Household "1" --> "1" Cash : holds
    Firm "1" --> "1" Cash : holds
    Bank "1" --> "1" Cash : as reserves
    Household "1" --> "1" DemandDeposit : at
    Household "1" --> "0..1" TimeDeposit : at
    Firm "1" --> "1" DemandDeposit : at
    DemandDeposit "0..*" --> "1" Bank
    TimeDeposit "0..*" --> "1" Bank

    State ..> Household : taxes and transfers
    State ..> Bank : issues bonds
```

### The twelve sectors

`Firm` is one type with a sector field, not twelve subclasses. **This town has no imports**, so it
must produce everything it consumes — its clothing, electronics, furniture and car firms are makers,
not shops, and are correspondingly large (§13.6).

| Sector | Firms | Positions | Notes |
|---|---|---|---|
| Food | 12 | ~120 | |
| Clothing | 6 | ~84 | |
| Electronics | 4 | ~80 | Obsolescence; 24-month replacement cycle |
| Furniture | 4 | ~56 | |
| Bicycles | 3 | ~9 | |
| Cars | 4 | ~110 | Running cost; second-largest financed purchase |
| **Public transport** | 1 | ~25 | Capacity-limited, not elastic. The transport numéraire |
| Restaurants | 16 | ~120 | |
| Personal services | 14 | ~42 | |
| Leisure / holidays | 7 | ~35 | **The collinearity breaker** — financeable, durability 1 (§1.3) |
| **Rental firm** | 1 | ~4 | Holds 35% of rented stock |
| Construction | 6 | ~99 | **Two products**: dwellings, and capacity for other firms |
| **Bank** | 1 | 12 | The 79th employer |
| **Total** | **79** | **~796** | Scaled by the loader to exactly 800 |

### What a household can be at once

There is no `Person` hierarchy. One `Household` record carries every role simultaneously, and the
combination is what makes the distributional questions answerable:

```mermaid
flowchart LR
    HH(("Household"))
    HH --- W["<b>Worker</b><br/>one position, always — no unemployment, D20"]
    HH --- SH["<b>Shareholder</b><br/>receives firm and bank profit"]
    HH --- LL["<b>Landlord</b><br/>if in the top wealth deciles"]
    HH --- TN["<b>Tenant or owner</b><br/>30 / 30 / 40 at t=0"]
    HH --- BR["<b>Borrower</b><br/>with probability rising in θ"]
    HH --- AB["<b>Abstainer</b><br/>10% · θ=0 · exempt from §6.4 and §6.6 feedback"]

    classDef role fill:#eef2f7,stroke:#4a6fa5,color:#12203a
    classDef key  fill:#f6d5d0,stroke:#b4553f,color:#3a1a12
    class W,SH,LL,TN,BR role
    class AB key
```

**This overlap is exactly why the headline result has to be decomposed** (§1.2, story 10-03). An
abstainer is also a wage earner, a shareholder, possibly a landlord and possibly a bank employee —
so under D21 more credit in the town raises his income a year later, while raising the prices he
pays. The two channels run in opposite directions, and reporting real consumption alone would mix
them.

### Entities deliberately absent

| Not modelled | Consequence |
|---|---|
| Unemployment (D20) | Income shocks run through demotion only; default rates are conservative |
| Firm failure (D22) | No insolvency wave, no fire sale — the model cannot produce a crash |
| Intermediate goods (D22) | Wages are the whole of marginal cost |
| A second bank | Inflates C4, the interest-to-income share, by construction |
| Demography, inheritance | Cannot show how housing wealth concentrates across generations (B21) |
| Cooperative/municipal landlords | ~10% of German rental stock is cost-priced; rents here are more market-responsive than reality |
| An equity market price (D16) | The model can say nothing about asset-price inflation in shares |
