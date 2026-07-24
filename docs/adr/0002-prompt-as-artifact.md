# ADR-0002: A prompt is a versioned, immutable artifact

- **Status:** Accepted
- **Date:** 2026-07-23

## Context

Everything the registry does — testing, promotion, rollback, "which prompt was live" — requires a
prompt to have a stable identity. Without one, there is nothing to test, promote, or roll back
*to*. So the foundational decision is what a prompt *is* as far as the registry is concerned.

The naive model is a mutable record: a prompt named `summarize` with a `text` field you edit. It
is simple and it destroys every capability that depends on stability. Edit the text and the old
version is gone — you cannot compare the new one against it, cannot roll back to it, and cannot
answer what was live yesterday. A mutable prompt has no version, only a current value.

Options considered:

1. **Mutable prompt record.** One row, editable text. Simple; no history, no rollback, no
   comparison, no answer to "what was live then".
2. **Prompt with an append-only version list.** The prompt is a name; each edit creates a new
   immutable version under it. Every version is preserved and addressable.
3. **Git as the version store.** Prompts in files, versioned by commits. Real versioning, but it
   ties every prompt change to a code deploy — the exact coupling the registry exists to break
   ([prompts-are-code.md](../prompts-are-code.md)).
4. **A prompt as a template with mutable variables.** Conflates the stable part (the template) with
   the volatile part (the inputs); the template still needs versioning, so this does not answer the
   question, it splits it.

## Decision

**A prompt is a named entity; each change publishes a new immutable version under that name.**

- **A version, once published, never changes.** New wording is a new version, not an edit. Version
  `summarize@7` is `summarize@7` forever, so a test result, a rollback target, and an audit record
  all refer to the same bytes.
- **A version's identity is its name plus its version number.** An application resolves
  `summarize@7` — or an alias that points to a version ([ADR-0005](./0005-referencing-and-rollback.md))
  — to exactly one immutable prompt.
- **A version carries its own metadata**: who published it, when, the message describing the change,
  and its test results. The version is the unit of review and audit, so it must carry the context a
  reviewer needs.
- **The template and its input variables are separate.** The versioned artifact is the template
  with named slots; the values that fill the slots at runtime are not part of the version. This
  keeps a version stable while the data flowing through it varies per request.

Git-as-store is rejected as the primary mechanism because it couples prompt release to code
release. The registry may be *backed* by a git-like immutable store, but the prompt's lifecycle is
independent of the application's — that independence is the point.

## Consequences

**Positive**

- Immutability is what makes everything else possible: you can compare version 8 to version 7,
  because 7 still exists exactly as it was; you can roll back to 7, because it is still there; you
  can answer "what was live" because every version is preserved.
- A version is a reviewable, auditable unit. A prompt change becomes a discrete event with an
  author, a timestamp, and a rationale — the same shape as a commit.
- Separating the template from its variables keeps a version stable across the endless variation of
  runtime inputs, so a version means one fixed thing.

**Negative**

- **Immutable versions accumulate forever.** A prompt edited daily produces a long version history
  that must be stored, and most of those versions will never be referenced again. The registry
  needs a retention or archival story, or the version list becomes unwieldy — and deciding which
  old versions are safe to drop reintroduces a small version of the "what might we still need"
  problem.
- Immutability means a typo fix is a new version, not an edit, which feels heavy for a trivial
  change and tempts authors toward wanting mutability "just for small fixes" — the exception that
  would unravel the guarantee.
- Decoupling the prompt's lifecycle from the code's is the feature and also a new failure mode: the
  application and the prompt can now be at incompatible versions. A prompt version expecting a new
  input variable that the deployed code does not provide is a mismatch that git-in-the-source-file
  could not produce, and the reference contract ([ADR-0005](./0005-referencing-and-rollback.md))
  has to manage it.
- Separating template from variables draws a line that is not always clean. A value that is
  sometimes hard-coded guidance and sometimes runtime input does not fit neatly on either side, and
  mis-placing it either bloats the version count or leaks volatile data into a "stable" version.
