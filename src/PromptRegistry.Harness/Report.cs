using System.Globalization;

namespace PromptRegistry.Harness;

/// <summary>Prints the per-slice comparison and the verdict as a plain-text table.</summary>
public static class Report
{
    public static void Print(
        Options opts, GoldenSet golden,
        EvaluationResult candidate, EvaluationResult? baseline, GateVerdict verdict)
    {
        Console.WriteLine();
        Console.WriteLine($"promptcheck — {opts.Prompt}  candidate v{candidate.Version}"
            + (baseline is not null ? $" vs baseline v{baseline.Version}" : " (no baseline)"));
        Console.WriteLine($"golden set: {golden.Cases.Count} cases across {golden.Slices.Count} slices, {opts.Runs} runs each");
        Console.WriteLine(new string('-', 62));
        Console.WriteLine($"{"slice",-16}{"candidate",12}{"baseline",12}{"delta",10}   verdict");
        Console.WriteLine(new string('-', 62));

        foreach (var s in verdict.Slices)
        {
            var baseCol = s.Baseline is { } b ? Pct(b) : "  -";
            var deltaCol = s.Delta is { } d ? (d >= 0 ? "+" : "") + Pct(d) : "  -";
            var mark = s.Regressed ? "REGRESSED" : "ok";
            Console.WriteLine($"{s.Slice,-16}{Pct(s.Candidate),12}{baseCol,12}{deltaCol,10}   {mark}");
        }

        Console.WriteLine(new string('-', 62));
        Console.WriteLine($"{(verdict.Passed ? "PASS" : "FAIL")}: {verdict.Reason}");

        // Name the failing cases so a real regression is actionable, not just a red number.
        if (!verdict.Passed)
        {
            var regressedSlices = verdict.Slices.Where(s => s.Regressed).Select(s => s.Slice).ToHashSet();
            foreach (var c in candidate.Cases.Where(c => regressedSlices.Contains(c.Slice) && c.Failures.Count > 0))
                Console.WriteLine($"  - {c.Slice}/{c.Id}: failed {string.Join(", ", c.Failures)}");
        }
    }

    private static string Pct(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
