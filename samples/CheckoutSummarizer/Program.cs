using PromptRegistry.Client;
using PromptRegistry.Core;

// An example consumer: a checkout-summarising app that knows one thing — the reference
// prompt://checkout-summary@production. It never names a version. Run it, then promote or roll
// back in another terminal and watch the version it resolves change live — with no redeploy.

var baseUrl = new Uri(Environment.GetEnvironmentVariable("REGISTRY_URL") ?? "http://localhost:8080");
const string reference = "prompt://checkout-summary@production";
var name = PromptReference.Parse(reference);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var client = new PromptRegistryClient(http, new PromptRegistryClientOptions
{
    BaseUrl = baseUrl,
    CacheTtl = TimeSpan.FromSeconds(2),
    // Bundled fallback: what this app serves if the registry is down at cold start.
    BundledFallback = new Dictionary<string, ResolvedPrompt>
    {
        [$"{name.Name}@{name.Environment}"] = new(
            name.Name, name.Environment, 0,
            "[bundled fallback] Summarise the order for {{customer}}.",
            new[] { "customer" }, "sha256:bundled")
    }
});

Console.WriteLine($"Resolving {reference} every 2s. Ctrl+C to stop.\n");
int? last = null;

while (true)
{
    try
    {
        var p = await client.ResolveAsync(reference);
        if (p.Version != last)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] resolved v{p.Version}  ({p.ContentHash})");
            Console.WriteLine($"           template: {p.Template}\n");
            last = p.Version;
        }
    }
    catch (PromptResolutionException ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {ex.Message}");
    }
    await Task.Delay(2000);
}
