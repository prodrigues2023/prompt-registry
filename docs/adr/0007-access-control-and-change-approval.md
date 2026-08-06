# ADR-0007: Access control and change approval

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

Every capability this registry provides — publish, gate, promote, roll back — is exposed as an
open HTTP endpoint with no caller identity attached to it. [ADR-0003](./0003-promotion.md)
mentions, in passing, that promotion "requires the version to have passed its regression tests
and, where policy requires, a human approval — the gate the [governance kit] may mandate for a
risk tier" — naming the idea without committing this repository to building it.

This ADR exists to make that boundary explicit rather than leaving it implied. As built
([src/PromptRegistry.Api](../../src/PromptRegistry.Api)):

- There is no authentication or authorization middleware anywhere in the API — no `AddAuthentication`,
  no `AddAuthorization`, no `[Authorize]` attribute, nothing in `appsettings.json` configuring an
  auth scheme.
- Every endpoint — publish, record a test result, promote, roll back — is reachable by any caller
  with network access to the service.
- The only gate that exists is the **automated** regression check
  ([ADR-0004](./0004-regression-testing.md)): a promotion is rejected with `409 Conflict` if the
  target version's `GateStatus != passed`. This gate is bypassable via `Force = true` (`--force`),
  and because there is no caller identity anywhere in the system, a forced promotion carries no
  record of who forced it or why.
- There is no human-approval workflow of any kind — no request/review/approve state machine, no
  notion of a role, no audit trail beyond the existing `alias_history` table's `promote`/`rollback`
  action log (which records *what* changed and *when*, never *who*).

Options considered:

1. **Build authentication, authorization, and an approval workflow into this registry.** The
   "complete" answer, and a substantial scope expansion — user/role management, an approval state
   machine, and audit logging are each a real subsystem, disproportionate to a reference
   implementation whose point is the prompt lifecycle, not identity and access management.
2. **Document the current state honestly, and defer the policy layer to a system built for it.**
   [context.md](../context.md)'s "Explicitly out of scope" already draws this kind of boundary for
   adjacent concerns — evaluation methodology to `rag-evaluation-toolkit`, prompt security to
   `ai-guardrails-toolkit`. Access control and approval policy is the same shape of concern, and
   [ai-solution-architecture-kit](https://github.com/prodrigues2023/ai-solution-architecture-kit) is
   the sibling repository whose subject is exactly governance and risk-tiered approval policy.
3. **Silently leave the gap undocumented.** Rejected outright — an ADR-numbered slot for this
   decision already existed in [ROADMAP.md](../../ROADMAP.md)'s Milestone 2 and in the
   [ADR index](./README.md); leaving it unwritten would be a silent scope cut disguised as an
   oversight, not a decision anyone could review or object to.

## Decision

**This registry implements no authentication, authorization, or human-approval workflow. The only
enforced gate is the automated regression check, and it is bypassable. Identity-aware access
control and change-approval policy are explicitly out of scope for this repository, owned instead
by whatever system a deployment layers in front of it — the natural fit being
[ai-solution-architecture-kit](https://github.com/prodrigues2023/ai-solution-architecture-kit)'s
risk-tiered governance model.**

- **No endpoint in this API requires a caller identity.** A deployment that needs to restrict who
  can publish, gate, promote, or roll back must add that enforcement in front of this service (a
  gateway, a sidecar, a reverse proxy with its own auth) — this registry does not do it and does
  not pretend to.
- **The regression gate remains the one enforced check**, and `force` remains an unaudited
  bypass — a deployment that needs "who forced this and why" recorded has to add an identity layer
  first; there is nothing here to attach that record to.
- **`alias_history` is an operational log, not an audit trail.** It answers "what changed, when" —
  useful for the rollback drill and for understanding a promotion timeline — but never "who," and
  this ADR does not claim otherwise.
- **A future integration with a governance layer would most naturally add an actor identity to
  every write endpoint and make `Force` require an explicit, logged reason** — named here as the
  shape the next step would take, not committed to as scheduled work.

## Consequences

**Positive**

- The registry stays small and easy to run locally with no identity provider, no user database, no
  role configuration — consistent with [context.md](../context.md)'s "runs on a laptop, no cloud
  account" constraint, which an auth subsystem would immediately threaten.
- The boundary is now explicit and reviewable. Anyone integrating this registry knows, from this
  document, that they must add access control themselves rather than discovering it by noticing an
  unauthenticated `POST` to `/environments/production/prompts/{name}` succeeds.
- Deferring to `ai-solution-architecture-kit` keeps governance policy in one place across that
  repository's other integrations, rather than this registry inventing its own partial,
  inconsistent version of the same concern.

**Negative**

- **As shipped, this registry is not safe to expose without a fronting access-control layer.**
  Anyone with network access can publish, promote to production, or roll back — there is no
  built-in protection against this, and a reader who does not read this ADR could deploy it
  believing otherwise.
- The regression gate's `force` bypass having no audit trail is a real weakness on its own, not
  only in combination with the missing identity layer — even a single-tenant, trusted-network
  deployment loses "who force-promoted this and why" today.
- Deferring to a sibling repository means the two systems must actually be integrated for the
  governance story to be real; until that integration exists, "the natural fit is
  ai-solution-architecture-kit" is a stated intention, not a working capability.
