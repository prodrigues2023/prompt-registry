using System.Text.Json;

namespace PromptRegistry.Harness;

/// <summary>
/// One assertion about a model's output. Property-based, never exact-match — see ADR-0004.
/// Supported types: contains, not_contains, max_length, matches (regex), is_json.
/// </summary>
public sealed record Assertion(string Type, string? Value = null);

/// <summary>
/// One representative input, tagged with the slice it belongs to. The slice is what makes the
/// gate per-class: a change that degrades any one slice fails, even if the average looks fine.
/// </summary>
public sealed record GoldenCase(
    string Id,
    string Slice,
    Dictionary<string, string> Variables,
    List<Assertion> Assertions);

/// <summary>The golden set is the contract: the inputs a version is judged against, hard cases included.</summary>
public sealed record GoldenSet(string Prompt, List<GoldenCase> Cases)
{
    public static GoldenSet Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GoldenSet>(json, Options)
            ?? throw new InvalidOperationException($"Golden set at '{path}' is empty or invalid.");
    }

    public IReadOnlyList<string> Slices =>
        Cases.Select(c => c.Slice).Distinct().OrderBy(s => s).ToArray();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
