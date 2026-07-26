using PromptRegistry.Client;
using PromptRegistry.Core;

namespace PromptRegistry.Drills;

/// <summary>
/// Proves the honest consequence of caching by alias (ADR-0005): when a version changes, instances
/// that refresh at different moments briefly disagree, then converge once every cache has refreshed.
/// The registry does not promise instant fleet-wide consistency; it promises eventual consistency
/// bounded by the cache TTL — and this shows both the window and the convergence.
///
/// Instance A holds a zero TTL (always re-reads); instance B caches for a short window. When a new
/// version is promoted, A sees it immediately while B still serves the old one — the disagreement —
/// until B's TTL elapses and both agree.
/// </summary>
public static class FleetDrill
{
    private static readonly Uri Base = new("http://registry.invalid");
    private const string Name = "checkout-summary";
    private const string Env = "production";

    public static async Task<bool> RunAsync()
    {
        Console.WriteLine("fleet-consistency drill — two instances, one alias move, a brief disagreement.\n");

        var current = 1;
        ResolvedPrompt Version(int v) => new(Name, Env, v, $"prod v{v} for {{{{customer}}}}", ["customer"], $"sha256:v{v}");
        HttpMessageHandler Handler() => new FakeRegistry(_ => FakeRegistry.Ok(Version(current)));

        var a = new PromptRegistryClient(new HttpClient(Handler()) { BaseAddress = Base },
            new PromptRegistryClientOptions { BaseUrl = Base, CacheTtl = TimeSpan.Zero });
        var b = new PromptRegistryClient(new HttpClient(Handler()) { BaseAddress = Base },
            new PromptRegistryClientOptions { BaseUrl = Base, CacheTtl = TimeSpan.FromMilliseconds(300) });

        // Both instances start on v1.
        var a0 = await a.ResolveAsync(Name, Env);
        var b0 = await b.ResolveAsync(Name, Env);
        Console.WriteLine($"  before promotion:  A=v{a0.Version}  B=v{b0.Version}   (agree)");

        // A new version is promoted — one alias move at the registry.
        current = 2;

        // Immediately after: A re-reads (v2); B is still inside its cache window (v1). They disagree.
        var aHot = await a.ResolveAsync(Name, Env);
        var bHot = await b.ResolveAsync(Name, Env);
        var disagreed = aHot.Version == 2 && bHot.Version == 1;
        Console.WriteLine($"  just after promote: A=v{aHot.Version}  B=v{bHot.Version}   ({(disagreed ? "DISAGREE — the expected window" : "no disagreement")})");

        // Once B's TTL elapses, it refreshes and the fleet converges.
        await Task.Delay(350);
        var aEnd = await a.ResolveAsync(Name, Env);
        var bEnd = await b.ResolveAsync(Name, Env);
        var converged = aEnd.Version == 2 && bEnd.Version == 2;
        Console.WriteLine($"  after B's TTL:     A=v{aEnd.Version}  B=v{bEnd.Version}   ({(converged ? "converged" : "still diverged")})");

        var passed = disagreed && converged;
        Console.WriteLine($"\n{(passed ? "PASS" : "FAIL")}: consistency is eventual, bounded by the cache TTL — disagreement window, then convergence.");
        return passed;
    }
}
