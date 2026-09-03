# Result-based failure, and an ignored Result is a build error

**Epic:** E1 — Foundations you cannot retrofit
**Milestone:** M0
**Depends on:** 01-01
**New ground:** Failure as a return value, and the analyzer that makes it real

## Story

As a maintainer, I want operations that can fail to return a Result rather than throw, and an ignored Result to break the build, so that failure is visible in a signature, and cannot be silently dropped.

## Acceptance criteria

- [ ] FluentResults is referenced. Operations that can fail for **domain** reasons return `Result` or `Result<T>`.
- [ ] An analyzer or attribute makes **discarding** a returned `Result` a compiler error, not a warning.
- [ ] A compile-fail test proves it: calling a Result-returning method and ignoring the value does not build.
- [ ] The boundary is written down: Result at configuration, loading, I/O and the consistency check; **not** inside the §6.2 scoring loop.
- [ ] Error types carry enough to locate the fault — at minimum the tick, the agent and the invariant or rule that failed. A bare string is not enough to debug a run that is 480 ticks long.
- [ ] Arithmetic overflow and other defects are **not** modelled as Results. They terminate. The distinction is documented alongside the boundary.

## Where to start

There is one hazard here that is worse than the disease. An exception that nobody catches stops
the program; an ignored `Result` produces a silent wrong run, which is this project's defining failure
mode. C# has no `must_use`, so without an analyzer the discipline is imaginary. Get that working
before converting anything else, and prove it with a compile-fail test as in 01-01.

The second constraint is performance, and it is why the boundary matters. `Result<T>` in FluentResults
is a reference type and allocates; §7 of `LANGUAGE-CHOICE.md` commits the engine to no allocation in
the scoring loop. This is not a conflict once you notice that nothing in that loop can *fail* in a
domain sense — scoring a candidate has no failure mode, and a bad value there is a bug. So the rule is
not a compromise, it is a recognition of where domain failure actually lives.

Spend a moment on the error type. During a 480-tick run the message 'invalid state' costs an hour.

## How to verify

```sh
dotnet test --filter Category=CompileFail
```

The new ignored-Result test joins the three from 01-01. Four green.
