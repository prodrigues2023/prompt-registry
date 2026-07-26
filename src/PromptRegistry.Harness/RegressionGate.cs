namespace PromptRegistry.Harness;

public sealed record SliceDelta(string Slice, double Candidate, double? Baseline, double? Delta, bool Regressed);
public sealed record GateVerdict(bool Passed, string Reason, IReadOnlyList<SliceDelta> Slices);

/// <summary>
/// The comparative gate (ADR-0004): a candidate passes if it does not regress the baseline — the
/// version it would replace — on <em>any</em> slice, beyond a small tolerance. The question is never
/// "is the candidate good" but "is it at least as good as what it replaces, everywhere".
/// With no baseline (the first version), there is nothing to regress against, so it passes.
/// </summary>
public static class RegressionGate
{
    public static GateVerdict Evaluate(EvaluationResult candidate, EvaluationResult? baseline, double tolerance = 0.0)
    {
        if (baseline is null)
        {
            var noBase = candidate.Slices
                .Select(s => new SliceDelta(s.Slice, s.Score, null, null, false))
                .ToList();
            return new GateVerdict(true, "No baseline to compare against — the first version passes by default.", noBase);
        }

        var baselineBySlice = baseline.Slices.ToDictionary(s => s.Slice, s => s.Score);
        var deltas = new List<SliceDelta>();
        var regressedSlices = new List<string>();

        foreach (var s in candidate.Slices)
        {
            var b = baselineBySlice.TryGetValue(s.Slice, out var bs) ? bs : 0.0;
            var delta = s.Score - b;
            var regressed = delta < -tolerance;
            if (regressed) regressedSlices.Add(s.Slice);
            deltas.Add(new SliceDelta(s.Slice, s.Score, b, delta, regressed));
        }

        return regressedSlices.Count == 0
            ? new GateVerdict(true, "No slice regressed beyond tolerance — promotion allowed.", deltas)
            : new GateVerdict(false,
                $"Regression on slice(s) {string.Join(", ", regressedSlices)}: below baseline beyond tolerance {tolerance:0.##}.",
                deltas);
    }
}
