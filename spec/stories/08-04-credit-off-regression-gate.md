# **V5**: the credit-off regression gate

**Epic:** E8 — Validation gates
**Depends on:** 08-03, 06-02
**New ground:** The device that makes every future mechanism measurable

## Story

As the person who will add mechanisms to this model for months, I want `credit_high` with θ forced to zero to reproduce `credit_off` byte for byte, so that the difference between credit on and credit off is the attributed effect of one mechanism and nothing else.

## Acceptance criteria

- [ ] `credit_high` with every `θ = 0` produces output **byte-identical** to `credit_off` on the same seeds.
- [ ] Combined with 08-01's unused-purpose check, this proves the θ draws themselves cost nothing.
- [ ] The gate stores small baselines under version control — 48 ticks, 4 seeds — with the commit and effective configuration that produced each.
- [ ] Regenerating a baseline is a **separate, explicit command**, never a side effect of a comparison failing.
- [ ] A comparison against a baseline whose configuration no longer matches the schema fails loudly rather than comparing anyway.
- [ ] The gate is documented as the pattern every later mechanism inherits: arrive behind a switch, and *off* reproduces the previous version byte for byte.

## Where to start

This gate does two jobs and the second is the more valuable.

As a regression test it says the credit machinery does not perturb the model when it is switched
off — which is where most mistakes at a mechanism boundary show up first, because the economy is
perfectly capable of absorbing a wrong number into a plausible one.

As a **measurement instrument** it makes the on-versus-off difference the attributed effect of
exactly one mechanism, on paired seeds, with everything else held bit-identical. That is what lets
the write-up state how much of the result credit accounts for, and there is no honest way to
reconstruct it afterwards.

The rule generalises and should be written down here, because this is where it is first used: a
mechanism that cannot be switched off is a mechanism whose contribution cannot be measured. If a
future design does not admit an off switch, that is a reason to redesign it rather than to skip the
switch.

## How to verify

```sh
dotnet run --project tools/Gates -- creditoff
```

Byte-identical output, and a stale baseline being rejected rather than silently compared.
