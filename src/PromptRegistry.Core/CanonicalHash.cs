using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PromptRegistry.Core;

/// <summary>
/// Content identity for a prompt. Two versions with the same template, the same set of
/// variables, and the same metadata produce the same hash — so "did the prompt actually
/// change?" has a definite, whitespace- and order-stable answer instead of a judgement call.
/// </summary>
public static class CanonicalHash
{
    public static string Compute(
        string template,
        IEnumerable<string> variables,
        IReadOnlyDictionary<string, string> metadata)
    {
        var canonical = new
        {
            template,
            variables = variables.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
            metadata = metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                               .ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        var json = JsonSerializer.Serialize(canonical);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant()[..16];
    }

    public static string Compute(PublishRequest req) =>
        Compute(
            req.Template,
            req.Variables ?? Array.Empty<string>(),
            req.Metadata ?? new Dictionary<string, string>());
}
