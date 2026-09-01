# Choosing an implementation language

Decided 2026-09-02, before any engine code was written. The measurements below are reproducible
from [`../bench/`](../bench/).

**Outcome: C# on .NET 10.** Not because it was fastest — it was third of five — but because the
speed question turned out to be binary, and once it was settled every remaining criterion pointed
the same way.

---

## 1. The question, stated correctly

The naive question is "which language is fastest". That is the wrong one twice over.

First, because the workload has to be big enough for speed to matter at all, and that has to be
established rather than assumed. Second, because this program is a **scientific instrument whose
output will be published and defended**. Its characteristic failure is not a crash: it is a
plausible number that is wrong. There is no unit test that says *this economy is incorrect*. Two
review passes over `MODEL.md` found roughly forty-five defects in a document that could be read,
re-read and argued with; the code will not offer the same courtesy.

So the real question is: **how much compute does this need, and which language turns the largest
share of my mistakes into build failures instead of results?**

---

## 2. What the workload actually is

From `MODEL.md` §13.1 and §10:

| | |
|---|---|
| Households | 800, each an independent decision-maker every tick |
| Firms | ~79 across twelve sectors, plus the bank |
| Run length | 480 ticks, of which 240 are discarded warm-up |
| Seeds per scenario | 30, the same 30 across scenarios, compared paired |
| Scenarios | Grid A (6 named + `reserve_ratio` swept 1.00→0.01), Grid B, Grid C, twelve per-good financeability switches (§1.3, C2a), and ±50% sensitivity on the ~17 unanchored parameters of §13.10 |

That last row is what makes this a compute problem. A conservative count of the §10 sensitivity
programme is **~150 scenario-runs**, each 30 seeds × 480 ticks. The model is not large; the
*campaign* is.

---

## 3. The benchmark

### 3.1 What it computes

The §6.2 purchase decision, which is the model's inner loop. Per household, per tick: build the
candidate units, score each, sort descending, then walk the sorted list applying the two tests
against a budget that depletes as purchases are made.

```
constants:  HOUSEHOLDS = 800,  TICKS = 100,  CANDIDATES = 120
rng:        xorshift64, identical across all implementations

for tick in 1..TICKS:
  for household in 1..HOUSEHOLDS:

      λ      ← 1.0 + 1.5 · rnd()                    # reservation ratio     §13.2
      budget ← 2000 + 1000 · rnd()                  # monthly residual

      for c in 1..CANDIDATES:
          joy    ← 20 + 480 · rnd()                 # € per tick at t=0     §13.11
          status ← status_scale · w_g · (rnd() − 0.5)      # 200 · 1.3      §6.3
          σ      ← 0.8 · rnd()                      # σ ∈ [0, 0.8]          §13.2
          value  ← joy + σ · status

          if c is a divisible service:              # ~40% of candidates
              value ← value · n^(−α)                # α = 0.6               §13.4
                                                    # durables are indivisible:
                                                    # n ∈ {0,1}, never evaluated

          price ← 30 + 900 · rnd()
          dur   ← 1 + (c mod 60)                    # services(1) → cars(60)  §4
          cost  ← price / dur                       # depreciation          §6.2
                + 0.0025 · price                    # interest forgone, r_d/1200
                + running_cost if c is a car        # car_running_cost = 180  §13.4

          candidates[c] ← (score: value / cost, cost: cost)

      sort candidates by score, descending          # §6.2, "descending order of score"

      for (score, cost) in candidates:
          if score ≤ λ:      break                  # reservation test      §6.2
          if cost > budget:  continue               # affordability test
          budget ← budget − cost                    # sequential depletion
          bought ← bought + 1
```

### 3.2 Every constant is taken from the spec

Nothing here is invented to make a language look good:

| Benchmark expression | Source |
|---|---|
| `λ = 1.0 + 1.5·rnd()` | `λ_base = 1.0`, `λ_gap = 1.5` — §13.2, exact range |
| `σ = 0.8·rnd()` | `σ ∈ {min 0.0, max 0.8}` — §13.2, exact range |
| `200 · 1.3 · (rnd() − 0.5)` | `status_scale · w_g · (rank − 0.5)` — §6.3 form, `w_g` = the car's 1.3 |
| `n^(−0.6)` on ~40% | `α_g = 0.6 services` — §13.4; durables excluded because D26 made them indivisible |
| `price / dur` | depreciation term of `user_cost` — §6.2 |
| `0.0025 · price` | interest forgone on a cash purchase, `r_d / 1200` at `r_d_base = 3.0` — §6.2, §13.0 |
| `180` on ~1 candidate in 120 | `car_running_cost` — §13.4 |
| `dur = 1 + (c mod 60)` | durability spanning a service (1 tick) to a car (60) — §4 |
| descending walk, break at λ | §6.2, *The rule* |

### 3.3 Correctness gate

All five implementations must print **`bought = 8072155`**. They do. A differing count means they
are no longer computing the same thing and the comparison is worthless — which is the only defence
against accidentally benchmarking five different programs.

---

## 4. Why this loop is a fair proxy for the whole model

### 4.1 It dominates the tick

Per tick, in units of elementary work:

| Step | Work |
|---|---|
| **§6.2 purchase decision** | 800 × 120 scorings + **800 sorts of 120** ≈ **760,000** |
| Housing auction §6.5 | `move_propensity` 0.01 × 800 = **8 movers** |
| Credit queue §6.1 step 9 | tens of applications, each a DSTI/LTV test |
| Bank accounting §6.1 step 15 | ~2,000 loans × a few operations |
| Metrics §9 (deciles, Gini) | 800 · log 800 ≈ 7,700 |
| SFC assertion §6.1 step 17 | ~900 agents, linear |

§6.2 is roughly **95% of the tick**. The claim is not that the rest is free — it is that the rest
is either constant-small or linear in agents, and therefore cannot move the answer. Everything
else in the model is bookkeeping over hundreds of objects; §6.2 is a sort per household per tick.

### 4.2 It is the part that resists optimisation

This matters more than the raw share. The obvious rescue for a slow language is vectorisation —
push the hot loop into array operations. **§6.2 cannot be vectorised**, for three independent
reasons:

- Each household's ranking is **heterogeneous**: different `σ`, different `λ`, different holdings,
  so different scores over different candidate sets.
- There is a **sort per household per tick**, which is control-flow-heavy and data-dependent.
- The budget walk is **inherently sequential**: each purchase changes what is affordable next, so
  the loop cannot be reordered or batched.

This is the textbook case where NumPy-style rescue fails. It removes "just vectorise it" from the
list of answers, which is precisely why the language choice is load-bearing here and would not be
in a model built on matrix updates.

### 4.3 What the benchmark is made of, measured

Decomposed on the reference machine (C, best of three):

| | |
|---|---|
| Full: score + sort + walk | 0.51 s |
| Same with the sort removed | 0.17 s |
| **The sort is** | **67% of the total** |

Two consequences, both worth carrying forward:

1. **The results are sensitive to the sort idiom**, which is why section 5.2 below equalises it. Read the
   table as *"how well does each language sort 120 structs 80,000 times"* — a narrower claim than
   "how fast is this simulation", and one that happens to be sufficient.
2. **A full sort is wasted work.** The walk *breaks* at `score ≤ λ`, so only the candidates above
   λ are ever examined. Filtering to `score > λ` first (O(n)) and sorting only the survivors should
   be built in from the start rather than retrofitted.

### 4.4 Where it could mislead — stated because it is not free

- **`CANDIDATES = 120` is an estimate, not derived from the spec.** `MODEL.md` never says how many
  candidate units a household evaluates. At 30 everything is ~4× faster; at 300, ~2.5× slower.
  This scales every language identically, so the **ratios hold and only the wall-clock predictions
  move** — but the wall-clock predictions in section 5.3 should be read as order-of-magnitude.
- **λ barely prunes here.** About 101 of 120 candidates are bought per household-tick, an artefact
  of the synthetic score distribution and not a household anyone recognises. A real run breaks out
  of the walk far earlier, so the benchmark is **conservative**: the real thing should be faster.
- **No allocation in the hot loop** — one candidate array is reused. This is favourable to the
  garbage-collected languages relative to a naive implementation that allocates per household per
  tick. It is the design that would actually be written, so it is fair as a measure of *achievable*
  performance, but it is a constraint the implementation now owes the benchmark.
- **The housing auction is the genuine unknown.** §6.5's simultaneous chain resolution is the one
  step whose cost cannot be bounded from the spec. If chain resolution needs backtracking it could
  be superlinear in movers, and it is the single thing most likely to invalidate section 4.1's 95%.
- Single-threaded throughout. The campaign parallelises across seeds identically in every
  candidate language, so this does not shift the comparison.

---

## 5. Results

### 5.1 Method

Reference machine: AMD Ryzen 7 2700U, 8 cores, Fedora, Linux 7.1.10. Python 3.14.7, Go 1.26.6,
gcc 16.2.1 (`-O2`), Zig 0.17.0-dev.644 (`-OReleaseFast`), .NET SDK 10.0.111 (Release).

**Minimum of five interleaved rounds.** Interleaved because single runs on this machine varied by
up to 60% under background load — enough to reorder adjacent languages, and enough that a
first-past-the-post measurement produced a table that had to be retracted.

### 5.2 Best idiom per language

The first measurement was wrong in two directions, because the sort idiom dominates (section 4.3) and the
idioms are not equivalent:

| Language | Slow idiom | Fast idiom | Ratio |
|---|---|---|---|
| C | `qsort` — function pointer per comparison — 0.890 s | hand-rolled inlined sort — **0.487 s** | 1.83× |
| Go | `sort.Slice` — reflection-based swap — 1.450 s | `slices.SortFunc` — **1.022 s** | 1.42× |
| C# | `Span.Sort` + struct `IComparer` — 0.999 s | `Array.Sort` + `Comparison<T>` — **0.857 s** | 1.17× |

Note that C# inverts the expectation: the supposedly-faster devirtualisable struct comparer lost to
the plain delegate. **Every language must be given its best idiom or the comparison is fiction**,
and which idiom is best cannot be assumed.

### 5.3 The table

800 households × 100 ticks × 120 candidates, all producing `bought = 8072155`:

| | min | vs Python | vs C |
|---|---|---|---|
| **C** (inlined sort) | 0.487 s | 75.6× | 1.00× |
| **Zig 0.17** (`std.mem.sort`) | 0.729 s | 50.4× | 1.50× |
| **C# .NET 10** (`Array.Sort`) | **0.857 s** | **43.0×** | **1.76×** |
| **Go 1.26** (`slices.SortFunc`) | 1.022 s | 36.0× | 2.10× |
| **Python 3.14** | 36.788 s | 1.0× | 75.6× |

Extrapolated (one seed = 480 ticks = 4.8× the benchmark; one scenario = 30 seeds; campaign = 150
scenarios over 8 cores):

| | 1 seed | 1 scenario | Full campaign, 8 cores |
|---|---|---|---|
| C | 2 s | 70 s | 22 min |
| Zig | 4 s | 2 min | 33 min |
| **C#** | 4 s | 2 min | **39 min** |
| Go | 5 s | 2 min | 46 min |
| **Python** | 3 min | 88 min | **27.6 h** |

### 5.4 What this settles, and what it does not

**It settles one thing: the choice is binary.** Python is 36–76× behind *every* compiled candidate.
Twenty-eight hours per campaign against forty minutes is not a tuning difference — it is the
difference between "start it Friday and hope there was no bug" and "re-run it over lunch". In a
research loop where the model changes many times, that compounds into how many questions get asked
at all.

**It does not settle anything finer.** The four compiled options span **2.1×**, and 33 minutes
versus 46 minutes for an entire sensitivity campaign is not a decision criterion. The C#-to-Go gap
of 1.2× is comfortably inside the range the `CANDIDATES = 120` guess could move on its own.

Which means: **pick on maintainability, type safety and fluency, and the speed takes care of
itself — provided it is not Python.**

---

## 6. Why C#

### 6.1 The criterion that decided it

Given section 5.4, the remaining question is which language turns the most mistakes into build failures.
This project has a *known* defect profile — not a hypothetical one. The two specification reviews
kept producing the same four classes:

1. **Unit confusion.** `value` in utils against `user_cost` in euros survived multiple passes and
   silently pinned the price level to the utility scale (D26).
2. **Convention misapplication.** `r_annual / 12` against rates quoted in per cent — read
   literally, a 25% monthly charge (D26).
3. **Float money.** §13.0 requires integer cents and never floating point for balances, which was
   a rule in prose that nothing enforced.
4. **Missed cases in closed sets.** The squeeze order, strategic-versus-involuntary default, which
   of four constraints binds, tenure states, the four overextension routes. Adding a case and
   missing a call site is exactly the bug that produces a plausible wrong run.

### 6.2 What C# does about each, verified by compiling the failures

Three project settings:

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>
<Nullable>enable</Nullable>
```

and two domain types:

```csharp
// Money is cents. There is deliberately no conversion from double.
readonly record struct Money(long Cents)
{
    public static Money operator +(Money a, Money b) => new(a.Cents + b.Cents);
}

// A rate is per cent per annum. The only way out applies the correct divisor.
readonly record struct Rate(double PercentPerAnnum)
{
    public double PerTick => PercentPerAnnum / 1200.0;
}
```

give, each confirmed by writing the mistake and watching the build fail:

| Mistake | Result |
|---|---|
| `money + rate` | **CS0019** — *Operator '+' cannot be applied to operands of type 'Money' and 'Rate'* |
| `Money m = 1234.56;` (euros-as-double into cents) | **CS0029** — *Cannot implicitly convert type 'double' to 'Money'* |
| Forgotten case in a `switch` expression | **CS8509** — *…is not exhaustive. For example, the pattern 'Constraint.Leverage' is not covered* |
| `long` money arithmetic wrapping | **`OverflowException`** at runtime, not a silent wrap |

That is defect classes 1, 2, 3 and 4 respectively, moved from prose to the compiler. `Rate.PerTick`
in particular means the §13.0 convention cannot be misapplied at a call site, because no other
route out of the type exists.

### 6.3 The trap that defeats this

**A `_ => throw new ArgumentOutOfRangeException()` arm makes any switch trivially exhaustive, so
CS8509 never fires.** The safety in row three is bought entirely by *omitting* the discard arm.

Rule for this codebase: **no discard arm on a domain enum.** Let the compiler enforce coverage, so
that adding a case to `Constraint`, or a step to the squeeze order, breaks the build at every site
that must handle it. Discard arms are permitted only for genuinely open inputs — parsed
configuration, external data — where the set really is not closed.

### 6.4 Why not Go

Go was a serious candidate and lost narrowly.

**In its favour:** named types do give unit safety — `type Cents int64` and `type Rate float64`
cannot be mixed without an explicit conversion, which covers defect class 1, the most damaging one.
It is the most readable of the candidates, and a codebase mostly written by an assistant benefits
from a language with fewer ways to be clever. Its compile-test loop is the fastest here.

**Against:** no exhaustiveness checking on switches — the `exhaustive` linter is external and
advisory, so defect class 4 stays a discipline rather than a guarantee. And **no checked
arithmetic**: integer overflow wraps silently, which is a real liability when every balance is
integer cents accumulating over 480 ticks. It was also 1.2× slower, which is noise, and is not part
of the argument.

The decisive point is the author's own fluency: C# is his daily working language and he already
profiles and optimises managed code, so the performance in section 5.3 is performance he will actually
realise rather than leave on the table. Go's readability advantage is worth most when the reader is
not fluent in the alternative. Here he is.

### 6.5 Why not Rust, and why not Zig

**Rust** has the strongest story of the five for §6.1 — newtypes make unit confusion
unrepresentable, `match` is exhaustive by default, and debug builds panic on integer overflow. It
was **excluded by the author on maintainability grounds**: he does not know it well enough to
maintain confidently. That is the right call and not a reluctant one. Code that is
assistant-written but human-unmaintainable creates a dependency, and this instrument has to be
debuggable by its owner at two in the morning without help. Type safety bought with that dependency
is not a bargain.

It is also worth noting that Rust's *unique* strengths — fearless concurrency and zero-cost memory
management — are the two things this workload does not need. Parallelism here is embarrassingly
parallel across seeds, one process per seed with no shared state at all.

**Zig** was fastest of the practical options and would be an appealing project. It is excluded for
stability: it is pre-1.0, and during this very benchmark `std.time.Timer` proved not to exist in
0.17-dev, so code written against current documentation would not compile. That is acceptable
friction when the language *is* the project. It is not acceptable in a research instrument backing
a published claim.

---

## 7. What this choice obliges the implementation to do

The benchmark measured *achievable* performance, not automatic performance. Four constraints follow
and are owed back:

1. **Agents live in flat arrays, indexed by integer**, not as an object graph of references. Cache
   locality is most of the gap between the compiled languages and it is not free.
2. **No allocation in the §6.2 hot loop.** The candidate buffer is allocated once per household and
   reused, as in the benchmark. A per-tick allocation would hand the measured advantage back to the
   garbage collector.
3. **Filter before sorting** (section 4.3): candidates below λ are never examined, so they should never be
   sorted.
4. **Parallelise across seeds only, never within a tick.** One process per seed, no shared mutable
   state. This preserves determinism and the paired-seed comparison of §13.1, and it is why none of
   Go's or Rust's concurrency advantages came into the decision.

And one boundary: **the engine writes CSV or Parquet and nothing else.** No analysis, no plotting,
no statistics inside the C# project. This keeps the engine small enough to audit end to end, and
leaves the choice of analysis tooling as a separate and reversible decision.

---

## 8. What would reopen this decision

Stated so that it is a decision rather than a preference:

- **If the housing auction turns out superlinear** in movers (section 4.4), §6.2 is no longer 95% of the
  tick and the whole benchmark is measuring the wrong loop. Re-profile the real engine at phase 2
  before trusting these extrapolations further.
- **If `CANDIDATES` is much larger than 120** — say the model ends up evaluating several hundred
  units per household per tick — the campaign moves from 39 minutes toward hours, and the 2.1×
  spread among compiled languages starts to matter after all.
- **If the campaign grows past ~1,000 scenarios**, the same applies.

None of these would favour Python. They would favour moving from C# toward Zig or C, and they would
do so on evidence rather than on taste.
