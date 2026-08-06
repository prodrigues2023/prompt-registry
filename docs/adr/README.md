# Architecture Decision Records

Decisions are numbered, immutable once accepted, and superseded rather than edited.
See [ADR-0001](./0001-record-architecture-decisions.md) for the process itself.

| ADR | Title | Status |
| --- | --- | --- |
| [0001](./0001-record-architecture-decisions.md) | Record architecture decisions in ADRs | Accepted |
| [0002](./0002-prompt-as-artifact.md) | A prompt is a versioned, immutable artifact | Accepted |
| [0003](./0003-promotion.md) | Promotion through environments | Accepted |
| [0004](./0004-regression-testing.md) | Regression testing a prompt change | Accepted |
| [0005](./0005-referencing-and-rollback.md) | Referencing and rollback | Accepted |
| [0006](./0006-prompt-artifact-and-reference-format.md) | Prompt artifact and reference format | Accepted |
| [0007](./0007-access-control-and-change-approval.md) | Access control and change approval | Accepted |

## How the accepted decisions fit together

They are the software-delivery lifecycle, applied to a prompt
([prompts-are-code.md](../prompts-are-code.md)):

- **0002** gives the prompt a **version** — immutable, addressable, the thing everything else acts on.
- **0004** **tests** that version, comparatively, against the one it would replace.
- **0003** **promotes** it — an alias moves from staging to production, gated by the test.
- **0005** lets the application **reference** it and lets an operator **roll it back** by re-pointing
  the alias, no deploy.

Version → test → promote → reference/rollback. It is `git` plus CI/CD plus a feature flag, for the
one artifact that usually gets none of them. The load-bearing decision is **0002**: immutability is
what makes testing comparative, promotion safe, and rollback possible — remove it and there is no
stable thing to test against, promote, or return to.

**0006** and **0007** are a different kind of ADR: written after Milestones 3 and 4 already shipped,
documenting the artifact/reference contracts and the access-control boundary as actually built,
rather than designing either fresh. 0006 records where the implementation is narrower than 0002 and
0005 promised (no real authorship field, no compatibility-declaration mechanism); 0007 records that
access control and change approval simply do not exist in this codebase. Neither closes a gap —
both make the gap visible instead of implicit. See
[docs/contracts/README.md](../contracts/README.md#whats-honestly-disclosed-as-incomplete).

## Template

```markdown
# ADR-XXXX: Title

- **Status:** Proposed | Accepted | Superseded by ADR-YYYY
- **Date:** YYYY-MM-DD

## Context

The forces at play: the requirement, the constraints, the options considered and why each
was or was not viable.

## Decision

What was decided, in the active voice. What was deliberately deferred.

## Consequences

**Positive** — what this buys.

**Negative** — what it costs, and what you will have to live with. An ADR with no negative
consequences has not been thought through.
```

## Disagreeing with a decision

Open an issue titled `ADR-XXXX: <your objection>`. Experience from running a prompt registry —
especially a case where the version/alias model got in the way — is the most useful kind.
