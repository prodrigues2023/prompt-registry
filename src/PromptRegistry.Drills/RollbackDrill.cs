using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using PromptRegistry.Client;
using PromptRegistry.Core;

namespace PromptRegistry.Drills;

/// <summary>
/// Measures how fast a rollback reaches a running consumer — the claim that a rollback is a pointer
/// move that takes effect without redeploying the application. Against a live registry: put a bad
/// version in production, confirm a consumer serves it, roll back, and time how long until the same
/// consumer serves the good version again. The bound is the client's cache TTL — seconds — versus
/// the minutes a code redeploy would take.
/// </summary>
public static class RollbackDrill
{
    private const string Name = "drill-rollback";
    private const string Env = "production";
    private static readonly TimeSpan ConsumerTtl = TimeSpan.FromSeconds(1);

    public static async Task<bool> RunAsync(Uri registry)
    {
        Console.WriteLine($"rollback drill — how fast does a rollback reach a consumer? (registry: {registry})\n");
        using var http = new HttpClient { BaseAddress = registry, Timeout = TimeSpan.FromSeconds(5) };

        try { await http.GetAsync("/health"); }
        catch { Console.Error.WriteLine("registry not reachable — run `make up` first."); return false; }

        // Arrange: a good version live in production, then a bad version force-promoted over it.
        var good = await Publish(http, "Order {{order_id}} for {{customer}}, total {{total}}.");
        await MarkTest(http, good, passed: true);
        await Promote(http, good, force: false);

        var bad = await Publish(http, "Order for {{customer}} - be extremely brief.");
        await Promote(http, bad, force: true); // simulate a human override putting a bad version live
        Console.WriteLine($"  set up: v{good} good (in production), v{bad} bad force-promoted over it");

        // A running consumer, caching by alias, currently observes the bad version.
        var consumer = new PromptRegistryClient(
            new HttpClient { BaseAddress = registry, Timeout = TimeSpan.FromSeconds(3) },
            new PromptRegistryClientOptions { BaseUrl = registry, CacheTtl = ConsumerTtl });
        var warm = await consumer.ResolveAsync(Name, Env);
        Console.WriteLine($"  consumer currently serves v{warm.Version} (the bad one: {warm.Version == bad})");

        // Act: roll back, and time how long until the consumer serves the good version.
        var sw = Stopwatch.StartNew();
        (await http.PostAsync($"/environments/{Env}/prompts/{Name}/rollback", null)).EnsureSuccessStatusCode();

        int seen;
        do
        {
            seen = (await consumer.ResolveAsync(Name, Env)).Version;
            if (seen != good) await Task.Delay(25);
        } while (seen != good && sw.ElapsedMilliseconds < 10_000);
        sw.Stop();

        var recovered = seen == good;
        Console.WriteLine($"\n  rollback issued -> consumer served v{good} again after {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  bounded by the {ConsumerTtl.TotalSeconds:0}s consumer cache TTL — no application redeploy, no restart.");

        var passed = recovered && sw.ElapsedMilliseconds < 5_000;
        Console.WriteLine($"\n{(passed ? "PASS" : "FAIL")}: a rollback propagates within the cache TTL — seconds, not the minutes a redeploy costs.");
        return passed;
    }

    private static async Task<int> Publish(HttpClient http, string template)
    {
        var resp = await http.PostAsJsonAsync($"/prompts/{Name}/versions",
            new { template, variables = new[] { "customer", "order_id", "total" }, metadata = new { } });
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("version").GetInt32();
    }

    private static async Task MarkTest(HttpClient http, int version, bool passed) =>
        (await http.PostAsJsonAsync($"/prompts/{Name}/versions/{version}/test", new { passed, details = new { } }))
            .EnsureSuccessStatusCode();

    private static async Task Promote(HttpClient http, int version, bool force) =>
        (await http.PutAsJsonAsync($"/environments/{Env}/prompts/{Name}", new { version, force }))
            .EnsureSuccessStatusCode();
}
