# Project skeleton and the safety settings

**Epic:** E1 — Foundations you cannot retrofit
**Milestone:** M0
**Depends on:** nothing
**New ground:** The solution layout, and the three csproj settings D27 was chosen for

## Story

As the person who will defend these results, I want the compiler configured so that the mistakes this project keeps making cannot build, so that the guarantees in D27 are enforced by the toolchain rather than by my memory.

## Acceptance criteria

- [ ] A solution with two projects: the engine and its tests, both targeting `net10.0`.
- [ ] The engine csproj sets `TreatWarningsAsErrors`, `CheckForOverflowUnderflow` and `Nullable` to enable/true. All three, not two.
- [ ] A test asserts that a `switch` expression missing an enum case **fails to compile** with `CS8509`.
- [ ] A test asserts that assigning a `double` to a `Money` **fails to compile** with `CS0029`.
- [ ] A test asserts that adding a `Rate` to a `Money` **fails to compile** with `CS0019`.
- [ ] The three compile-fail tests are tagged as a category that can be run alone, because they are slower than ordinary unit tests.
- [ ] `README` or a comment in the csproj records **why** each setting is there, naming the defect class it prevents. A setting whose purpose is not recorded gets removed by someone later.

## Where to start

The compile-fail tests are the unusual part and they are the point of the story: a setting
that nothing tests will be silently switched off the first time it is inconvenient. Compile a small
source snippet in memory with the Roslyn APIs, with the same language version and options as the
engine project, and assert on the diagnostic IDs that come back. Assert on the **ID**, not on the
message text, which is localised and will differ on a German-locale machine.

Note the trap that makes one of these tests fake: a discard arm in a switch expression makes it
trivially exhaustive, so `CS8509` never fires. The negative test must use a switch with **no**
discard arm, and the codebase rule that follows from it belongs in the same commit.

Overflow deserves a decision recorded here rather than discovered later. `CheckForOverflowUnderflow`
raises at runtime, which sits oddly beside the project's no-exceptions rule. Treat the two as
answering different questions: a domain operation that can legitimately fail returns a `Result`,
while arithmetic overflow on a money balance is a **defect**, not an outcome, and should terminate
the run loudly. Write that distinction down.

## How to verify

```sh
dotnet test --filter Category=CompileFail
```

Three tests, all green. A green run here means the *wrong* code did not compile — if any of
them goes red, the guarantee it protects has already been lost.
