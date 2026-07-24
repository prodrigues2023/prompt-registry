# ADR-0003: Promotion through environments

- **Status:** Accepted
- **Date:** 2026-07-23

## Context

A new prompt version should not go straight to production any more than new code should. It needs a
place to be exercised against real-ish traffic before it affects users. That means the same
environment progression code has — a version validated in a lower environment before it is trusted
in a higher one — applied to prompts.

The question is how an environment selects a prompt version. If every environment hard-codes a
version number, promotion is a code change in each environment, which reintroduces the coupling the
registry exists to remove. If environments select by "latest", there is no promotion at all — every
environment gets every change instantly, including production, which is the opposite of a gate.

Options considered:

1. **Hard-code the version per environment.** Explicit, and promotion becomes a config edit and
   deploy in each environment — the coupling the registry was meant to break.
2. **Every environment uses "latest".** No promotion, no gate; a new version hits production the
   instant it is published.
3. **Environment-scoped aliases.** Each environment points a stable alias (e.g. `production`) at a
   chosen version; promotion is re-pointing the higher environment's alias at the version the lower
   one validated.
4. **A full release-management workflow engine.** Maximum control, far more machinery than
   selecting which version an environment serves.

## Decision

**Each environment resolves a stable alias to a version, and promotion is re-pointing an
environment's alias at the version a lower environment validated.**

- **An environment references an alias, not a version number.** The application in production
  resolves `summarize@production`; the registry maps that alias to a specific immutable version
  ([ADR-0002](./0002-prompt-as-artifact.md)). The application does not know or care which version
  number is behind the alias.
- **Promotion is re-pointing the alias.** A new version is published and the `staging` alias points
  at it; once it passes its tests there ([ADR-0004](./0004-regression-testing.md)), the `production`
  alias is re-pointed at the same version. The version does not change — only which environments
  trust it.
- **Promotion is gated.** Re-pointing the production alias requires the version to have passed its
  regression tests and, where policy requires, a human approval — the gate the
  [governance kit](https://github.com/prodrigues2023/ai-solution-architecture-kit) may mandate for
  a risk tier.
- **What is tested is what is promoted.** Because promotion moves an alias to an *existing,
  immutable* version, production runs the exact version staging validated — the prompt analogue of
  build-once-promote.

## Consequences

**Positive**

- Promotion is decoupled from deployment. Advancing a prompt to production is re-pointing an alias,
  not editing and redeploying an application — so a validated prompt reaches production in seconds,
  and a non-engineer can do it.
- What was tested is what runs. The production alias points at the same immutable version staging
  exercised, eliminating the "revalidated then edited again" gap.
- The gate is a real control point. Regression tests and approval sit exactly where promotion
  happens, so a prompt cannot reach production without passing them.

**Negative**

- **An alias is a level of indirection that can hide what is live.** "Production is on
  `summarize@production`" does not tell you which version that is without resolving the alias, and
  an operator debugging an incident has one more lookup between them and the actual prompt text. The
  indirection that enables fast rollback also obscures the current state.
- Alias re-pointing is instant and global for that environment. There is no gradual rollout in this
  model — the moment production's alias moves, every production request uses the new version. Teams
  wanting a canary or a percentage rollout need A/B layering on top (out of scope,
  [context.md](../context.md)), and without it a bad promotion affects 100% of traffic at once.
- Environment-scoped aliases multiply as environments and prompts do. A dozen prompts across four
  environments is forty-eight alias-to-version mappings to manage, and a stale or mis-pointed alias
  is a silent way to run the wrong prompt in the wrong place.
- The gate is only as good as the tests behind it ([ADR-0004](./0004-regression-testing.md)). A
  promotion that passes a weak or unrepresentative regression set is a green light with nothing
  behind it — the promotion structure guarantees a checkpoint, not that the checkpoint is meaningful.
