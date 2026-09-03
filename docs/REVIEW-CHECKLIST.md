# Review checklist

Short, and every item is here because getting it wrong produces a plausible number rather than a
crash. Anything a test can check is a test instead; what is left is what a reader has to look for.

## Failure

- [ ] A method that can fail returns a `Result`. It does not throw for a domain outcome.
- [ ] An exception is present only where the situation is a **bug** — overflow, a bad index, an
      argument no caller should ever pass — never where it is an outcome the model has to handle.
- [ ] No `Result` inside the shopping walk. A candidate that cannot be afforded is an ordinary
      branch, not a failure, and wrapping it would allocate per candidate in the one loop that
      must not allocate.
- [ ] **No bare failure messages.** Every failure names its subject, what was expected and what was
      there — `Validation.Fail` and `Validation.Require` enforce the shape, so a message assembled
      by hand needs a reason.
- [ ] Validation accumulates. A configuration with three problems reports three problems.

## Money

- [ ] No `double` holding an amount of money. Ever.
- [ ] Every rounding of money goes through `Money.Scaled`, `Money.Split` or `Money.Allocate`.
- [ ] Two amounts that must add up to a third are cut with `Allocate`, not rounded separately.
- [ ] No literal `12`, `100` or `1200` near a rate. That divisor lives in `Rate` and nowhere else.

## Randomness

- [ ] Every draw comes from a named stream in the registry. No stream is derived from an index, a
      counter, or a slice of another stream.
- [ ] A new mechanism gets a new purpose. It does not borrow an existing stream, because that
      shifts the draws the current model makes and makes the two versions incomparable.

## Scope

- [ ] Nothing from `draft/` was implemented because it was easy. If a mechanism is not in
      `spec/01-SIMULATION.md`, it is not in v1.
- [ ] No parameter was changed to make a check pass. If a result contradicts the specification, the
      specification is corrected first and the contradiction is written down.
- [ ] The tier mix was not calibrated. It is the output the experiment turns on.
