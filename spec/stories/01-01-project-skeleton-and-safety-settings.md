# Project skeleton and the safety settings

**Epic:** E1 — Foundations
**Depends on:** —
**New ground:** The guarantees the language was chosen for, made real

## Story

As a maintainer, I want the solution to refuse to build when code is unsafe, so that the reasons C# was chosen are enforced by the compiler rather than by discipline.

## Acceptance criteria

- [ ] A solution with an engine library, a console runner and a test project. Nothing else.
      *Amended during 01-04:* plus one analyser project. Making an ignored `Result` a build error
      needs an analyser, and Roslyn only loads those from a separate `netstandard2.0` assembly.
      It is build tooling — it ships with the compiler, not with the engine — and the rule this
      criterion exists to protect, that the model does not grow layers, still holds.
- [ ] `TreatWarningsAsErrors`, `CheckForOverflowUnderflow` and `Nullable enable` in every project.
- [ ] A **compile-fail test** for each guarantee, proving wrong code does not build:
      adding a rate to a money value (CS0019), assigning a `double` to a money value (CS0029),
      a `switch` expression missing an enum case (CS8509 — only when there is no `_ =>` arm).
- [ ] A runtime test that `long` arithmetic overflow throws `OverflowException` rather than wrapping.
- [ ] `dotnet build` succeeds with zero warnings from a clean clone.

## Where to start

The compile-fail tests are the point of this story. Settings in a `.csproj` are a claim; a test that
asserts a specific compiler error is evidence, and it is the only thing that will notice when
someone relaxes a setting to make an unrelated build pass.

Keep the runner separate from the engine from the first commit. The engine writes CSV and nothing
else, and a runner that has grown analysis into it is much harder to separate later than to keep
apart now.

The missing-enum-case check only fires when the `switch` expression has no discard arm. A `_ =>`
default silently defeats it, so the test must assert the error is produced for the arm-less form.

## How to verify

```sh
dotnet build -warnaserror && dotnet test --filter FullyQualifiedName~CompileFail
```

Each compile-fail case reporting the expected diagnostic id. A test that passes because compilation
succeeded is the failure mode to watch for.
