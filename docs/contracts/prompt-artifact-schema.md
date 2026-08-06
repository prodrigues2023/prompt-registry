# Prompt artifact schema

**The exact record a published prompt version is, as implemented.** [ADR-0002](../adr/0002-prompt-as-artifact.md)
decided a prompt is a named entity with immutable versions under it; this document is that shape,
written from the running code and schema in [`src/PromptRegistry.Core`](../../src/PromptRegistry.Core)
and [`db/migrations/001_init.sql`](../../db/migrations/001_init.sql) — not designed fresh, described
as built, since Milestone 3 shipped before this milestone was written up.

## `PromptVersion`

```csharp
public record PromptVersion(
    string Name,
    int Version,
    string Template,
    IReadOnlyList<string> Variables,
    IReadOnlyDictionary<string, string> Metadata,
    string ContentHash,
    string GateStatus,
    object? TestResults,
    DateTimeOffset CreatedAt);
```

| Field | Type | Set by | Notes |
| --- | --- | --- | --- |
| `Name` | `string` | Caller, at first publish | Convention `namespace.service` (e.g. `checkout.order-summary`) — the catalog view derives namespace/service by splitting on `.`, but nothing enforces the convention at write time. |
| `Version` | `int` | Registry | `coalesce(max(version), 0) + 1` for the name. Never client-supplied — a publish request cannot choose its own version number. |
| `Template` | `string` | Caller | Raw text with `{{variable}}` tokens, substituted by simple regex (`\{\{\s*(\w+)\s*\}\}`) — no nested templating, no escaping; an unmatched token is left verbatim rather than replaced with empty string. |
| `Variables` | `string[]` | Caller | Declared slot names. **Not cross-checked against what the template actually contains** — a declared variable absent from the template, or a template token absent from `Variables`, is not an error. |
| `Metadata` | `map<string,string>` | Caller | Free-form flat string map. Convention (seen in samples and dev seed data) uses `author` and `team` keys, but no key is enforced or required — see the disclosure below. |
| `ContentHash` | `string` | Registry | `"sha256:" + first 16 hex chars` of a SHA-256 over the canonical JSON of `{template, variables (sorted), metadata (sorted)}` — see [`CanonicalHash.cs`](../../src/PromptRegistry.Core/CanonicalHash.cs). Two versions with identical template + variables + metadata (key order doesn't matter) hash identically. |
| `GateStatus` | `string` | Registry, via the test-result endpoint | One of `untested` \| `passed` \| `failed`. Starts `untested` at publish; moves once, via `POST /prompts/{name}/versions/{version}/test` — see [test-contract.md](./test-contract.md). |
| `TestResults` | `object?` (opaque JSON) | Registry, via the test-result endpoint | Whatever `details` payload the caller of the test endpoint posts — for `promptcheck`, this is the harness's own report shape; see [test-contract.md](./test-contract.md). |
| `CreatedAt` | `DateTimeOffset` | Registry (Postgres `now()`) | Set at insert, returned by `returning created_at`. |

## Immutability, as actually enforced

- The only write path for a version is `INSERT` — [`PromptStore.PublishAsync`](../../src/PromptRegistry.Api/PromptStore.cs)
  has no update or delete code path for `prompt_versions` anywhere.
- The database enforces `unique (name, version)`, preventing a duplicate version number for a name.
- **Immutability past that point is a code-discipline convention, not a database guarantee** — there
  is no trigger, no `REVOKE UPDATE`, no check constraint stopping a row from being altered outside
  the application. The schema comment in `001_init.sql` says as much: "append-only ... never
  UPDATEd or DELETEd." This is worth stating plainly rather than implying a stronger guarantee than
  what's actually enforced.
- The only table that is genuinely, deliberately mutable is `aliases` — an environment-to-version
  pointer, explicitly the one exception: "aliases is the ONLY mutable table." Promotion and
  rollback ([ADR-0003](../adr/0003-promotion.md), [ADR-0005](../adr/0005-referencing-and-rollback.md))
  both work by moving an alias row, never by touching a version.

## Where this schema disagrees with ADR-0002 — disclosed, not silently fixed

[ADR-0002](../adr/0002-prompt-as-artifact.md) states: *"A version carries its own metadata: who
published it, when, the message describing the change, and its test results."* As built, only
**when** (`CreatedAt`) and **test results** (`TestResults`/`GateStatus`) are real, enforced fields.
**Who published it** and **the message describing the change** are not fields at all — `author`
only appears as an unenforced, optional key inside the free-form `Metadata` map in sample data and
demo scripts, never validated or required by any code path. A publish request with no `author` key,
or a completely empty `Metadata`, is accepted without complaint.

This is a real gap between the decision and the implementation, not a design choice — recorded here
rather than quietly resolved, so a future milestone can either add the fields ADR-0002 promised or
supersede that ADR to match what was actually built.

## Related

- [Reference format](./reference-format.md) — how an application names a `PromptVersion` (or, more
  precisely, the alias that resolves to one)
- [Test contract](./test-contract.md) — what populates `GateStatus` and `TestResults`
- [ADR-0002](../adr/0002-prompt-as-artifact.md), [ADR-0006](../adr/0006-prompt-artifact-and-reference-format.md)
