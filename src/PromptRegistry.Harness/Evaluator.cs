using System.Text.Json;
using System.Text.RegularExpressions;
using PromptRegistry.Core;

namespace PromptRegistry.Harness;

public sealed record CaseResult(string Id, string Slice, double Score, IReadOnlyList<string> Failures);
public sealed record SliceScore(string Slice, double Score, int Cases);
public sealed record EvaluationResult(int Version, IReadOnlyList<SliceScore> Slices, IReadOnlyList<CaseResult> Cases, double Overall);

/// <summary>
/// Runs a prompt version against a golden set. Each case is rendered and completed <c>runs</c> times;
/// a case scores the fraction of runs that pass every assertion; a slice averages its cases. Handling
/// non-determinism by repetition, and scoring by property rather than exact match, are both ADR-0004.
/// </summary>
public sealed class Evaluator(IPromptModel model, int runs = 5)
{
    public EvaluationResult Evaluate(int version, string template, GoldenSet golden)
    {
        var cases = new List<CaseResult>();

        foreach (var c in golden.Cases)
        {
            var rendered = PromptTemplate.Render(template, c.Variables);
            var passes = 0;
            var failures = new HashSet<string>();

            for (var r = 0; r < runs; r++)
            {
                var output = model.Complete(rendered, c.Variables, r);
                var caseFailures = Check(output, c.Assertions);
                if (caseFailures.Count == 0) passes++;
                else foreach (var f in caseFailures) failures.Add(f);
            }

            cases.Add(new CaseResult(c.Id, c.Slice, (double)passes / runs, failures.ToList()));
        }

        var slices = cases
            .GroupBy(c => c.Slice)
            .Select(g => new SliceScore(g.Key, g.Average(x => x.Score), g.Count()))
            .OrderBy(s => s.Slice)
            .ToList();

        var overall = slices.Count == 0 ? 0 : slices.Average(s => s.Score);
        return new EvaluationResult(version, slices, cases, overall);
    }

    private static List<string> Check(string output, IEnumerable<Assertion> assertions)
    {
        var failures = new List<string>();
        foreach (var a in assertions)
        {
            var ok = a.Type.ToLowerInvariant() switch
            {
                "contains" => output.Contains(a.Value ?? "", StringComparison.OrdinalIgnoreCase),
                "not_contains" => !output.Contains(a.Value ?? "", StringComparison.OrdinalIgnoreCase),
                "max_length" => output.Length <= int.Parse(a.Value ?? "0"),
                "matches" => Regex.IsMatch(output, a.Value ?? ""),
                "is_json" => IsJson(output),
                _ => throw new NotSupportedException($"Unknown assertion type '{a.Type}'.")
            };
            if (!ok) failures.Add($"{a.Type}({a.Value})");
        }
        return failures;
    }

    private static bool IsJson(string s)
    {
        try { using var _ = JsonDocument.Parse(s); return true; }
        catch (JsonException) { return false; }
    }
}
