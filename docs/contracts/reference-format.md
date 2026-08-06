# Reference format

**How an application names a prompt and an environment alias, exactly as
[`PromptReference`](../../src/PromptRegistry.Core/PromptReference.cs) and
[`PromptRegistryClient`](../../src/PromptRegistry.Client/PromptRegistryClient.cs) implement it.**
[ADR-0005](../adr/0005-referencing-and-rollback.md) decided applications resolve an alias, never a
literal or a pinned version; this is the grammar and the resolution behavior that decision compiles
to.

## The reference string

```
prompt://<name>@<environment>
```

- `Scheme` is the literal `prompt://`, and it is **optional on parse** — `PromptReference.TryParse`
  strips it if present but also accepts a bare `<name>@<environment>`.
- Parsing splits on the **last** `@` in the string (`LastIndexOf('@')`), so `name` cannot itself
  contain `@`, but nothing else about `name` or `environment` is validated beyond non-empty —
  neither is checked against an allowed character set or a maximum length.
- `PromptReference.Parse` throws `FormatException` on a malformed string; `TryParse` returns `false`
  instead of throwing, for callers that want to handle an invalid reference without a try/catch.
- `ToString()` always re-renders with the scheme prefix: `prompt://name@environment`.

Concrete example, from [`samples/CheckoutSummarizer`](../../samples/CheckoutSummarizer/Program.cs):

```csharp
const string reference = "prompt://checkout-summary@production";
var name = PromptReference.Parse(reference);
```

**An application only ever holds this string (or the parsed `Name`/`Environment` pair) — never a
version number.** The version is the registry's business; the application's business is which
alias it wants.

## Resolving a reference

`PromptRegistryClient` (constructed from an `HttpClient` and `PromptRegistryClientOptions`):

```csharp
Task<ResolvedPrompt> ResolveAsync(string reference, CancellationToken ct = default);
Task<ResolvedPrompt> ResolveAsync(string name, string environment, CancellationToken ct = default);
```

```csharp
public record ResolvedPrompt(
    string Name, string Environment, int Version,
    string Template, IReadOnlyList<string> Variables, string ContentHash);
```

Behavior, in order:

1. **Cache lookup.** Cache key is `"{name}@{environment}"`, held in an in-memory
   `ConcurrentDictionary<string, CacheEntry>`. A fresh (within `CacheTtl`, default 30s) cached entry
   is returned without a network call.
2. **Registry call.** On a cache miss or stale entry: `GET {BaseUrl}/environments/{environment}/prompts/{name}`
   (both segments URL-escaped), deserialized into `ResolvedPrompt`.
3. **Stale-cache fallback.** If the call fails (`HttpRequestException`, `TaskCanceledException`,
   `JsonException`) and a cached value exists — even an expired one — that stale value is returned
   rather than propagating the failure. This is what keeps a brief registry outage from becoming an
   application outage, per [ADR-0005](../adr/0005-referencing-and-rollback.md)'s "resilient to a
   brief registry outage."
4. **Bundled fallback.** If there is no cached value at all (a cold start during an outage) and a
   `BundledFallback` dictionary entry exists for `"{name}@{environment}"`, that literal, baked-in
   `ResolvedPrompt` is returned.
5. **Failure.** If none of the above produce a value, `ResolveAsync` throws `PromptResolutionException`.

The sample app registers its bundled fallback explicitly:

```csharp
BundledFallback = { ["checkout-summary@production"] =
    new ResolvedPrompt("checkout-summary", "production", 0,
        "[bundled fallback] Summarise the order for {{customer}}.",
        new[] { "customer" }, "sha256:bundled") }
```

`Version = 0` and a hash of `sha256:bundled` are the concrete markers a bundled fallback carries —
neither corresponds to a real published version, which is deliberate: a consumer that logs or
displays the resolved version can tell "served from the live registry" apart from "served from the
cold-start fallback" without any extra signal.

## The endpoint this compiles to

`GET /environments/{environment}/prompts/{name}` — one HTTP route
([`src/PromptRegistry.Api/Program.cs`](../../src/PromptRegistry.Api/Program.cs)) serves every
reference resolution. It returns the `ResolvedPrompt` for whatever version the `{environment}`
alias currently points at, or `404` if no such alias/name combination exists — the client library
above is the only thing standing between an application and this one route.

## What is deliberately not part of this contract

- **No pinned-version reference syntax.** There is no `prompt://name@version` or
  `prompt://name#7` form — only an alias (an environment name) is resolvable through this path, by
  design: pinning a version in application code is exactly the coupling
  [ADR-0005](../adr/0005-referencing-and-rollback.md) exists to avoid.
- **No compatibility declaration mechanism.** ADR-0005 names "the application declares which
  prompts and versions it is compatible with" as part of the decision, but no such declaration
  exists in the client library or the API today — the same kind of gap documented for `Metadata` in
  [prompt-artifact-schema.md](./prompt-artifact-schema.md), recorded rather than silently assumed
  away.

## Related

- [Prompt artifact schema](./prompt-artifact-schema.md) — what `ResolvedPrompt` is a view of
- [ADR-0005](../adr/0005-referencing-and-rollback.md), [ADR-0006](../adr/0006-prompt-artifact-and-reference-format.md)
