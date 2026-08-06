# Contracts

**Written after Milestones 3 and 4 already shipped — these document the formats the running
registry, client, and harness actually use, not a fresh design.** See
[ADR-0006](../adr/0006-prompt-artifact-and-reference-format.md) for why Milestone 2 was retrofitted
rather than skipped outright once the gap was noticed.

| Contract | Specifies |
| --- | --- |
| [Prompt artifact schema](./prompt-artifact-schema.md) | The exact `PromptVersion` record, its immutability guarantees (and their real limits), and its content hash |
| [Reference format](./reference-format.md) | The `prompt://name@environment` grammar, and the client's cache/fallback resolution behavior |
| [Test contract](./test-contract.md) | The golden-set JSON format, the per-slice evaluation, and what the regression gate actually checks |

Backed by [ADR-0006](../adr/0006-prompt-artifact-and-reference-format.md) (artifact and reference
format) and [ADR-0007](../adr/0007-access-control-and-change-approval.md) (access control — which
turns out not to exist, disclosed rather than assumed).

## How these compose

- The **prompt artifact schema** is what `POST /prompts/{name}/versions` writes and every other
  endpoint reads back.
- The **reference format** is how an application names the alias that resolves to one of those
  artifacts — never the artifact itself, never a pinned version.
- The **test contract** is what moves an artifact's `GateStatus`, which is what
  `PUT /environments/{env}/prompts/{name}` checks before allowing a promotion.

A reviewer checks the actual API (`src/PromptRegistry.Api`), client
(`src/PromptRegistry.Client`), and harness (`src/PromptRegistry.Harness`) against these three
documents field by field — the same test named in
[aws-serverless-blueprints](https://github.com/prodrigues2023/aws-serverless-blueprints/tree/main/docs/contracts)'s
contracts and every other repository in this portfolio, applied here retroactively instead of
before the code was written.

## What's honestly disclosed as incomplete

Two things this milestone's write-up surfaced rather than fixed:

- **Authorship and change-message fields** [ADR-0002](../adr/0002-prompt-as-artifact.md) describes
  are not real fields — see [prompt-artifact-schema.md](./prompt-artifact-schema.md)'s disclosure.
- **Access control and change approval do not exist** in this codebase at all —
  [ADR-0007](../adr/0007-access-control-and-change-approval.md) is a full accounting of that gap,
  not a description of enforced behavior.

## Related

- [docs/adr](../adr) — the decisions these contracts implement
- [ROADMAP.md](../../ROADMAP.md) — Milestones 3 and 4 build and validate against these contracts,
  in that order, ahead of this document
