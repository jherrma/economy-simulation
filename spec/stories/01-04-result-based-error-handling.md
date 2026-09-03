# Result-based failure, and an ignored `Result` is a build error

**Epic:** E1 — Foundations
**Depends on:** 01-01
**New ground:** Failure as a value, at the boundaries only

## Story

As a maintainer, I want operations that can fail to return a `Result` rather than throw, so that every failure path is visible in a signature and none of them can be silently discarded.

## Acceptance criteria

- [ ] FluentResults is referenced. Methods that can fail return `Result` or `Result<T>`.
- [ ] **No method in the engine throws** for a domain outcome. Exceptions remain only for genuine programming errors — overflow, index out of range — which are bugs, not outcomes.
- [ ] An **ignored `Result` is a build error**, via an analyser or a `[MustUseReturnValue]`-style attribute with a test proving it fires.
      *Resolved as:* an analyser, `ES0001`, in a fourth project — see the amendment on 01-01.
      C# has no attribute that makes a discarded return value an error, so the alternative was
      not available.
- [ ] Failures carry a message naming what was wrong and what was expected. A bare `Result.Fail("error")` is not acceptable and a review checklist item says so.
- [ ] Multiple validation failures **accumulate into one** failed `Result` rather than returning on the first.
- [ ] `Result` appears at boundaries only: configuration, loading, file I/O, the consistency check. It does **not** appear inside the shopping walk.

## Where to start

The boundary rule is the part worth thinking about. Inside the walk, a candidate that cannot be
afforded is not a failure — it is an ordinary outcome that the algorithm handles. Wrapping it in a
`Result` would allocate per candidate in the one loop that must not allocate, and would dress up a
normal branch as an error. Failure there means a bug, and a bug should stop the run loudly.

Accumulating validation errors rather than returning on the first is the difference between a loader
that costs one edit and one that costs a dozen run-fix-rerun cycles. FluentResults supports it
directly.

## How to verify

```sh
dotnet test --filter FullyQualifiedName~ResultTests
```

The ignored-result build failure, and a config with three separate problems reporting all three.
