# Context and scope

## The problem

A prompt is the highest-leverage line in an AI application — change it and the system behaves
differently — and it is routinely the least engineered thing in the codebase. It lives as a
string literal inside a function, edited in place when someone wants different behaviour, deployed
with the code, and forgotten.

That means none of the things the team does for code apply to it. There is no version to name, so
"which prompt was live when this went wrong?" has no answer. There is no test, so a wording change
that quietly degrades one class of inputs ships unnoticed. There is no promotion path, so the
prompt that was validated in staging is edited again before production. And there is no rollback,
so undoing a bad prompt means a code change, a review, and a redeploy — minutes to hours during
which the bad prompt is live.

The gap this repository fills is giving a prompt the same lifecycle the surrounding code already
has: an identity, a version, a test, a promotion, a rollback. A registry is the mechanism that
makes a prompt a first-class artifact instead of a string.

## Users

| User | Need |
| --- | --- |
| Engineer | Change a prompt without a code deploy, and know a test caught a regression |
| Prompt author (non-engineer) | Iterate on a prompt safely, with a promotion path and a rollback |
| Application | Reference a prompt by name and version, not carry a literal |
| Reviewer / committee | A prompt change is a reviewable, versioned event, not a silent edit |

## In scope

- A prompt as a versioned, immutable artifact with a stable identity ([ADR-0002](./adr/0002-prompt-as-artifact.md))
- Applications referencing a prompt by name and version or alias ([ADR-0005](./adr/0005-referencing-and-rollback.md))
- Promotion of a prompt version through environments, like code ([ADR-0003](./adr/0003-promotion.md))
- Regression testing a prompt change against a golden set ([ADR-0004](./adr/0004-regression-testing.md))
- Rollback to a previous version without an application deploy ([ADR-0005](./adr/0005-referencing-and-rollback.md))

## Explicitly out of scope

Deliberate exclusions:

- **Prompt authoring quality.** How to *write* a good prompt is a craft this registry does not
  teach. It manages the lifecycle of whatever prompt you wrote.
- **The evaluation methodology.** *How* to judge one prompt version better than another —
  the metrics, the golden set, the judge — is owned by the
  [rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit). This registry
  *runs* that judgement as a promotion gate; it does not define it.
- **A/B testing and traffic splitting in production.** Serving version A to some users and B to
  others is a delivery concern layered on top of the registry, not the registry itself.
- **Being a prompt IDE.** Authoring tooling, autocomplete, and playgrounds are a separate product.
  This is the registry those tools would publish to.
- **Prompt security.** Whether a prompt is robust to injection is the
  [ai-guardrails-toolkit](https://github.com/prodrigues2023/ai-guardrails-toolkit)'s subject. A
  prompt is not a security control ([prompts-are-code.md](./prompts-are-code.md)).

## Key constraints

1. **Runs on a laptop.** The registry and a sample integration come up with one command, with a
   local store and a stubbed model, no cloud account required.
2. **A prompt version is immutable.** Once published, a version never changes; a new wording is a
   new version — see [ADR-0002](./adr/0002-prompt-as-artifact.md).
3. **Applications reference, they do not embed.** An application carries a prompt name and a
   version or alias, never the prompt text — see [ADR-0005](./adr/0005-referencing-and-rollback.md).
4. **A change is testable before it ships.** A new version can be run against a golden set and
   compared to the current one before it is promoted — see [ADR-0004](./adr/0004-regression-testing.md).
5. **Rollback needs no deploy.** Pointing an alias back to a previous version takes effect without
   an application release — see [ADR-0005](./adr/0005-referencing-and-rollback.md).

## Related documents

- [Why prompts are code](./prompts-are-code.md) — the argument the whole registry rests on
- [Diagrams](./diagrams) — the prompt lifecycle and the promotion flow
- [ADRs](./adr) — the decisions and their reasoning
