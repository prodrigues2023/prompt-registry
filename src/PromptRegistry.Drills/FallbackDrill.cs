using PromptRegistry.Client;
using PromptRegistry.Core;

namespace PromptRegistry.Drills;

/// <summary>
/// Proves the client degrades instead of failing when the registry is unreachable:
///  - at cold start, with nothing cached, it serves the version bundled with the application;
///  - after one good fetch, if the registry then goes down, it serves the last-known-good (stale)
///    rather than the bundled fallback.
/// A registry outage becomes degraded-but-up, not an incident.
/// </summary>
public static class FallbackDrill
{
    private static readonly Uri Base = new("http://registry.invalid");
    private const string Name = "checkout-summary";
    private const string Env = "production";

    private static readonly ResolvedPrompt Bundled = new(
        Name, Env, 0, "[bundled] Summarise the order for {{customer}}.", ["customer"], "sha256:bundled");

    public static async Task<bool> RunAsync()
    {
        Console.WriteLine("fallback drill — the registry is down; the app must not be.\n");

        // 1) Cold start, registry unreachable, nothing cached -> bundled version serves.
        var down = new PromptRegistryClient(
            new HttpClient(new FakeRegistry(FakeRegistry.Unreachable)) { BaseAddress = Base },
            Options(bundled: true));
        var cold = await down.ResolveAsync(Name, Env);
        var servedBundled = cold is { Version: 0, ContentHash: "sha256:bundled" };
        Console.WriteLine($"  cold start, registry down    -> served v{cold.Version} ({cold.ContentHash})  [{Ok(servedBundled)}] expected bundled");

        // 2) One good fetch, then the registry goes down -> stale last-known-good serves (not bundled).
        var up = true;
        var flaky = new PromptRegistryClient(
            new HttpClient(new FakeRegistry(_ => up
                ? FakeRegistry.Ok(new ResolvedPrompt(Name, Env, 7, "prod v7 for {{customer}}", ["customer"], "sha256:v7"))
                : FakeRegistry.Unreachable(_))) { BaseAddress = Base },
            Options(bundled: true, ttl: TimeSpan.Zero));

        var first = await flaky.ResolveAsync(Name, Env);   // fetches v7, caches it
        up = false;                                         // registry goes down
        var second = await flaky.ResolveAsync(Name, Env);   // TTL is zero -> tries, fails -> serves stale v7
        var servedStale = first is { Version: 7 } && second is { Version: 7, ContentHash: "sha256:v7" };
        Console.WriteLine($"  fetched v7, then registry down -> served v{second.Version} ({second.ContentHash})  [{Ok(servedStale)}] expected stale v7, not bundled");

        var passed = servedBundled && servedStale;
        Console.WriteLine($"\n{(passed ? "PASS" : "FAIL")}: an outage degrades to bundled (cold) or stale (warm), never a hard failure.");
        return passed;
    }

    private static PromptRegistryClientOptions Options(bool bundled, TimeSpan? ttl = null) => new()
    {
        BaseUrl = Base,
        CacheTtl = ttl ?? TimeSpan.FromSeconds(30),
        BundledFallback = bundled
            ? new Dictionary<string, ResolvedPrompt> { [$"{Name}@{Env}"] = Bundled }
            : new Dictionary<string, ResolvedPrompt>()
    };

    private static string Ok(bool b) => b ? "ok" : "XX";
}
