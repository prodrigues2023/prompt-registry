using PromptRegistry.Core;
using PromptRegistry.Harness;
using Xunit;

namespace PromptRegistry.Tests;

public class PromptTemplateTests
{
    [Fact]
    public void Substitutes_tokens_and_leaves_data_uninterpreted()
    {
        var vars = new Dictionary<string, string> { ["customer"] = "Ana {{order_id}}", ["order_id"] = "AC-1" };
        // The value of {{customer}} is inserted verbatim; the {{order_id}} inside it is NOT re-expanded.
        Assert.Equal("Hi Ana {{order_id}}", PromptTemplate.Render("Hi {{customer}}", vars));
    }
}

public class EvaluatorTests
{
    // A model whose output is fixed, so assertion scoring is deterministic and isolated.
    private sealed class FixedModel(string output) : IPromptModel
    {
        public string Complete(string prompt, IReadOnlyDictionary<string, string> vars, int run) => output;
    }

    private static GoldenSet OneCase(string slice, params Assertion[] assertions) =>
        new("p", [new GoldenCase("c1", slice, new Dictionary<string, string>(), assertions.ToList())]);

    [Fact]
    public void Passing_assertions_score_full()
    {
        var e = new Evaluator(new FixedModel("Order AC-1 total $10"), runs: 3);
        var r = e.Evaluate(1, "t", OneCase("typical", new Assertion("contains", "AC-1")));
        Assert.Equal(1.0, r.Slices.Single().Score);
    }

    [Fact]
    public void Failing_assertion_scores_zero_and_names_the_failure()
    {
        var e = new Evaluator(new FixedModel("Order for Ana"), runs: 3);
        var r = e.Evaluate(1, "t", OneCase("completeness", new Assertion("contains", "AC-1")));
        Assert.Equal(0.0, r.Slices.Single().Score);
        Assert.Contains("contains(AC-1)", r.Cases.Single().Failures);
    }

    [Fact]
    public void Stub_model_regresses_completeness_under_a_brevity_prompt()
    {
        var golden = GoldenSet.Load(GoldenPath());
        var e = new Evaluator(new StubModel(), runs: 5);

        var full = e.Evaluate(1, "Summarise the order for {{customer}}, with the order number and total.", golden);
        var brief = e.Evaluate(2, "Summarise the order for {{customer}} - be extremely brief.", golden);

        Assert.Equal(1.0, Slice(full, "completeness"));
        Assert.Equal(0.0, Slice(brief, "completeness"));
        // The regression is confined to completeness — the other slices survive the rewrite.
        Assert.Equal(1.0, Slice(brief, "typical"));
        Assert.Equal(1.0, Slice(brief, "edge"));
    }

    private static double Slice(EvaluationResult r, string slice) => r.Slices.Single(s => s.Slice == slice).Score;

    private static string GoldenPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "samples", "golden", "checkout-summary.golden.json")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(dir!, "samples", "golden", "checkout-summary.golden.json");
    }
}

public class RegressionGateTests
{
    private static EvaluationResult Result(int version, params (string slice, double score)[] slices) =>
        new(version, slices.Select(s => new SliceScore(s.slice, s.score, 1)).ToList(), [], slices.Average(s => s.score));

    [Fact]
    public void No_baseline_passes_by_default()
    {
        var v = RegressionGate.Evaluate(Result(1, ("typical", 1.0)), baseline: null);
        Assert.True(v.Passed);
    }

    [Fact]
    public void A_slice_dropping_below_baseline_fails()
    {
        var candidate = Result(2, ("typical", 1.0), ("completeness", 0.0));
        var baseline = Result(1, ("typical", 1.0), ("completeness", 1.0));
        var v = RegressionGate.Evaluate(candidate, baseline);
        Assert.False(v.Passed);
        Assert.Contains("completeness", v.Reason);
    }

    [Fact]
    public void No_regression_passes()
    {
        var candidate = Result(2, ("typical", 1.0), ("completeness", 1.0));
        var baseline = Result(1, ("typical", 1.0), ("completeness", 1.0));
        Assert.True(RegressionGate.Evaluate(candidate, baseline).Passed);
    }

    [Fact]
    public void A_drop_within_tolerance_passes()
    {
        var candidate = Result(2, ("typical", 0.95));
        var baseline = Result(1, ("typical", 1.0));
        Assert.True(RegressionGate.Evaluate(candidate, baseline, tolerance: 0.1).Passed);
        Assert.False(RegressionGate.Evaluate(candidate, baseline, tolerance: 0.0).Passed);
    }
}
