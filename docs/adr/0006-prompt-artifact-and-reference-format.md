# ADR-0006: Prompt artifact and reference format

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

[ADR-0002](./0002-prompt-as-artifact.md) decided a prompt is a named entity with immutable versions
under it; [ADR-0005](./0005-referencing-and-rollback.md) decided an application resolves an alias,
never a literal or a pinned version. Neither ADR specifies the actual record shape or the actual
reference grammar — and without that, "a producer and a consumer could be built independently and
interoperate" (this repository's Milestone 2 goal) has nothing precise to agree on.

This ADR is being written after Milestones 3 and 4 already shipped — the registry, client, sample
app, regression harness, and validation drills exist and work
([`src`](../../src), [`docs/validation`](../validation) once M4 is written up). Milestone 2 was
skipped in sequence; this decision documents the contracts *as they were actually built*, not as a
fresh design. That is a deliberate choice explained below, not an oversight papered over.

Options considered, now that the code already exists:

1. **Design new contracts and reconcile the implementation to match.** The textbook order, but
   backwards from what happened — it would mean rewriting working, tested code (the registry API,
   the client library, the harness, three passing validation drills) to match a specification
   invented after the fact, for no behavioral benefit.
2. **Document the contracts as implemented, including the gaps between the ADR-0002/0005 language
   and what was actually built.** Slower to feel "correct" retroactively, but it is the only option
   that doesn't either destabilize working code or silently paper over a real documentation debt.

## Decision

**The prompt artifact schema and the reference format are documented exactly as implemented,
in [prompt-artifact-schema.md](../contracts/prompt-artifact-schema.md) and
[reference-format.md](../contracts/reference-format.md), including the specific places where the
implementation is narrower than what ADR-0002 and ADR-0005 described.**

- **The artifact is `PromptVersion`**: `Name`, `Version` (registry-assigned, never client-chosen),
  `Template`, `Variables`, `Metadata` (free-form, unenforced), a `ContentHash` (SHA-256 over the
  canonical template+variables+metadata), `GateStatus`, `TestResults`, and `CreatedAt`. Immutability
  is enforced by there being no update/delete code path, plus a `unique (name, version)` database
  constraint — not by a database-level write guard, which is disclosed rather than assumed.
- **The reference format is `prompt://<name>@<environment>`** (scheme optional on parse, split on
  the last `@`), resolved through exactly one endpoint
  (`GET /environments/{environment}/prompts/{name}`) via a client library that caches with a TTL,
  falls back to a stale cache entry on a registry error, and falls back further to a bundled
  last-known-good value on a cold start with no cache.
- **Two specific gaps between ADR-0002/0005's language and the built system are recorded, not
  fixed here:** ADR-0002 promised a version records "who published it" and "the message describing
  the change" — neither is a real field, only an unenforced convention inside `Metadata`. ADR-0005
  described an application "declaring which prompts and versions it is compatible with" — no such
  declaration mechanism exists. Both are named explicitly in the contract documents rather than
  quietly implied to exist.

## Consequences

**Positive**

- The contracts now match code that already runs and is already tested by three validation drills
  ([ROADMAP.md](../../ROADMAP.md)'s Milestone 4) — there is no risk of a freshly-designed contract
  turning out to be unbuildable or requiring a rewrite, because it was never speculative.
- The disclosed gaps (authorship metadata, compatibility declarations) are now a visible, trackable
  backlog instead of an implicit assumption someone would eventually discover the hard way.
- A new producer or consumer integrating against this registry has an accurate specification to
  build against, not an aspirational one that would mislead them about what the API actually returns.

**Negative**

- **Documenting after the fact means this ADR has less power to shape the implementation than a
  normal ADR does** — by definition, the code was already fixed before this decision was recorded.
  Where the built system made a debatable choice (e.g., no validation that `Variables` matches what
  the `Template` actually references), this ADR describes that choice rather than revisiting it.
- The disclosed gaps are still gaps. Naming them here does not close them — `author`/change-message
  fields and a compatibility-declaration mechanism remain unbuilt, and a future contributor could
  reasonably read ADR-0002 alone and expect fields that do not exist.
- Retroactive documentation is a process smell worth naming plainly: Milestone 2 existing as a
  named, tracked milestone and still being skipped in practice is exactly the failure mode a
  contracts-before-implementation discipline exists to prevent. This ADR closes the gap after the
  fact; it does not undo the fact that the gap existed.
