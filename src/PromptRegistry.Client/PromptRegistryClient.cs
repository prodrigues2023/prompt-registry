using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using PromptRegistry.Core;

namespace PromptRegistry.Client;

public sealed class PromptRegistryClientOptions
{
    /// <summary>Base URL of the registry API, e.g. http://localhost:8080.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>How long a resolved version is trusted before a refresh is attempted.</summary>
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Prompts bundled with the application, keyed by "name@environment". Served when the registry
    /// is unreachable at cold start and nothing is cached yet — so a registry outage degrades to
    /// last-known-good instead of an incident.
    /// </summary>
    public IReadOnlyDictionary<string, ResolvedPrompt> BundledFallback { get; init; }
        = new Dictionary<string, ResolvedPrompt>();
}

/// <summary>
/// The library an application uses to resolve prompts by reference. Resolve-by-alias, an in-memory
/// cache with TTL, a stale-serve on transient registry failure, and a bundled fallback for cold start.
/// The application only ever knows <c>prompt://name@environment</c> — never a version literal.
/// </summary>
public sealed class PromptRegistryClient(HttpClient http, PromptRegistryClientOptions options)
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public Task<ResolvedPrompt> ResolveAsync(string reference, CancellationToken ct = default)
    {
        var parsed = PromptReference.Parse(reference);
        return ResolveAsync(parsed.Name, parsed.Environment, ct);
    }

    public async Task<ResolvedPrompt> ResolveAsync(string name, string environment, CancellationToken ct = default)
    {
        var key = $"{name}@{environment}";

        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Value;

        try
        {
            var url = new Uri(options.BaseUrl, $"/environments/{Uri.EscapeDataString(environment)}/prompts/{Uri.EscapeDataString(name)}");
            var resolved = await http.GetFromJsonAsync<ResolvedPrompt>(url, JsonOpts, ct)
                ?? throw new PromptResolutionException($"Registry returned no body for {key}.");
            _cache[key] = new CacheEntry(resolved, DateTimeOffset.UtcNow + options.CacheTtl);
            return resolved;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Registry unreachable or slow: serve the last good value if we have one...
            if (cached.Value is { } stale) return stale;
            // ...otherwise fall back to what shipped with the application.
            if (options.BundledFallback.TryGetValue(key, out var bundled)) return bundled;
            throw new PromptResolutionException(
                $"Could not resolve {key}: registry unreachable and no cached or bundled value.", ex);
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly record struct CacheEntry(ResolvedPrompt Value, DateTimeOffset ExpiresAt);
}

public sealed class PromptResolutionException : Exception
{
    public PromptResolutionException(string message) : base(message) { }
    public PromptResolutionException(string message, Exception inner) : base(message, inner) { }
}
