# ADR-0005: Referencing and rollback

- **Status:** Accepted
- **Date:** 2026-07-23

## Context

The registry's value to a running application comes down to two operations: how the application
gets the prompt it should use, and how someone undoes a bad prompt when it is live. Both are about
the boundary between the application and the registry, and both are what a string-literal prompt
gets wrong.

With an embedded prompt, the application *is* the prompt — getting it is trivial and changing it is
a deploy. The registry inverts this: the application references a prompt it does not contain, which
makes rollback a registry operation instead of a code operation. But that inversion introduces its
own questions. When does the application resolve the reference — at build, at startup, per request?
What happens when the registry is unavailable? And what exactly does rollback re-point?

Options considered for referencing:

1. **Resolve at build — bake the prompt into the artifact.** Fast and available at runtime, and it
   makes rollback a rebuild-and-deploy — the coupling the registry exists to remove.
2. **Resolve per request — fetch the prompt every call.** Always current, and it puts the registry
   in the hot path of every request, adding latency and a hard runtime dependency.
3. **Resolve by alias, cached, with a refresh.** The application resolves an environment alias
   ([ADR-0003](./0003-promotion.md)) to a version, caches it, and refreshes on an interval or a
   signal. Current within the refresh window, resilient to a brief registry outage.
4. **Push — the registry notifies applications of changes.** Lowest latency to propagate, most
   infrastructure, and it inverts the dependency in a way most applications do not need.

## Decision

**Applications resolve an environment alias to a version, cache it, and refresh; rollback is
re-pointing the alias to a previous version, which propagates on the next refresh with no
application deploy.**

- **The application references an alias, never a literal or a pinned version.** It asks the registry
  for `summarize@production` and gets back the current version's text
  ([ADR-0002](./0002-prompt-as-artifact.md), [ADR-0003](./0003-promotion.md)).
- **The resolved prompt is cached with a refresh.** The application does not hit the registry per
  request; it resolves periodically (or on a change signal) and serves from cache in between. This
  keeps the registry out of the hot path and survives a short registry outage on the last known
  good value.
- **Rollback is re-pointing the alias.** A bad version in production is undone by pointing
  `production` back at the previous version. The change takes effect on the applications' next
  refresh — seconds, not a deploy. This is the capability that most justifies the whole registry.
- **The application declares which prompts and versions it is compatible with**, so a prompt version
  requiring an input the application does not provide ([ADR-0002](./0002-prompt-as-artifact.md)) is a
  resolvable mismatch — the application can refuse an incompatible version rather than run it broken.
- **There is always a safe fallback.** If the registry is unreachable and the cache is empty (cold
  start), the application falls back to a bundled last-known-good version rather than failing — the
  registry improves the prompt, it does not become a single point of total failure.

## Consequences

**Positive**

- Rollback is a seconds-long registry operation, not a code deploy. The single most valuable
  property of the registry — undo a bad prompt now, without a release — falls directly out of
  alias-plus-cache.
- The registry is out of the request hot path. Caching means normal traffic does not depend on the
  registry's latency or availability per call, only on the periodic refresh.
- The bundled fallback means the registry is an enhancement, not a new hard dependency that can take
  the application down. A registry outage degrades to "prompts do not update", not "the application
  is down".

**Negative**

- **The refresh window is a staleness window.** After a rollback, applications keep serving the bad
  version until their next refresh — so "rollback in seconds" is really "rollback in up-to-one-
  refresh-interval", and shrinking that interval to make rollback faster increases the registry load
  it was meant to avoid. The trade-off between propagation speed and registry load is unavoidable.
- Caching means different application instances can briefly run different versions — one refreshed,
  one not — so during a promotion or rollback the fleet is momentarily inconsistent. For most prompts
  this is harmless; for one where consistency matters it is a real edge the model does not eliminate.
- The compatibility declaration is another contract to maintain, and one that is easy to get subtly
  wrong. An application that declares compatibility too loosely will resolve a version it cannot
  actually run correctly; too tightly, and a harmless new version is needlessly refused. The mismatch
  it prevents is real, and so is the friction of keeping the declaration accurate.
- The bundled fallback can drift from the registry. A last-known-good baked into the artifact months
  ago may be far behind production's current version, so a cold start during a registry outage serves
  a very stale prompt — safe from a total-failure standpoint, surprising from a behaviour standpoint,
  and a subtle source of "why is this instance acting old".
