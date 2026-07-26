using System.Net;
using System.Text;
using System.Text.Json;
using PromptRegistry.Core;

namespace PromptRegistry.Drills;

/// <summary>
/// An in-process stand-in for the registry's HTTP endpoint, so the fleet and fallback drills run
/// with no server and no Docker. The responder decides each reply — including throwing, to simulate
/// an unreachable registry.
/// </summary>
public sealed class FakeRegistry(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Yield();
        return responder(request); // may throw to simulate an outage
    }

    /// <summary>A 200 response carrying a resolved prompt, as the real endpoint would return.</summary>
    public static HttpResponseMessage Ok(ResolvedPrompt prompt) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(prompt, Web), Encoding.UTF8, "application/json")
    };

    /// <summary>A responder that always fails, as if the registry were unreachable.</summary>
    public static HttpResponseMessage Unreachable(HttpRequestMessage _) =>
        throw new HttpRequestException("simulated: registry unreachable");
}
