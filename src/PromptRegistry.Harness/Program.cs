using System.Net;
using System.Net.Http.Json;
using PromptRegistry.Harness;

// promptcheck — run a candidate prompt version against its golden set, compare it to the version
// it would replace, and (optionally) write the verdict back as the promotion gate.
//
//   promptcheck --prompt <name> --candidate <version> --golden <path>
//               [--baseline-env production] [--runs 5] [--tolerance 0.0]
//               [--registry http://localhost:8080] [--gate]
//
// Exit code 0 = no regression, 1 = regression (so CI can gate on it).

var opts = Args.Parse(args);
if (opts is null) { Args.PrintUsage(); return 2; }

using var http = new HttpClient { BaseAddress = new Uri(opts.Registry), Timeout = TimeSpan.FromSeconds(10) };
var golden = GoldenSet.Load(opts.GoldenPath);
var evaluator = new Evaluator(new StubModel(), opts.Runs);

// Candidate: the version under test.
var candidate = await GetVersionAsync(http, opts.Prompt, opts.Candidate);
if (candidate is null) { Console.Error.WriteLine($"Candidate v{opts.Candidate} of '{opts.Prompt}' not found."); return 2; }
var candidateResult = evaluator.Evaluate(candidate.Version, candidate.Template, golden);

// Baseline: whatever the target environment currently serves (the version being replaced).
EvaluationResult? baselineResult = null;
var baseline = await ResolveAsync(http, opts.Prompt, opts.BaselineEnv);
if (baseline is not null && baseline.Version != candidate.Version)
    baselineResult = evaluator.Evaluate(baseline.Version, baseline.Template, golden);

var verdict = RegressionGate.Evaluate(candidateResult, baselineResult, opts.Tolerance);

Report.Print(opts, golden, candidateResult, baselineResult, verdict);

if (opts.Gate)
{
    var details = new
    {
        harness = "promptcheck",
        runs = opts.Runs,
        tolerance = opts.Tolerance,
        baseline_version = baselineResult?.Version,
        candidate_overall = candidateResult.Overall,
        slices = verdict.Slices.Select(s => new { s.Slice, s.Candidate, s.Baseline, s.Delta, s.Regressed }),
        reason = verdict.Reason
    };
    var resp = await http.PostAsJsonAsync(
        $"/prompts/{opts.Prompt}/versions/{opts.Candidate}/test",
        new { passed = verdict.Passed, details });
    Console.WriteLine(resp.IsSuccessStatusCode
        ? $"\nGate result written to the registry: {(verdict.Passed ? "PASSED" : "FAILED")}."
        : $"\nFailed to write gate result: {(int)resp.StatusCode} {resp.StatusCode}.");
}

return verdict.Passed ? 0 : 1;

// --- registry access (minimal DTOs; we only need version + template) ----------------------

static async Task<VersionDto?> GetVersionAsync(HttpClient http, string name, int version)
{
    var resp = await http.GetAsync($"/prompts/{name}/versions/{version}");
    return resp.StatusCode == HttpStatusCode.NotFound ? null
        : await resp.EnsureSuccess().Content.ReadFromJsonAsync<VersionDto>();
}

static async Task<VersionDto?> ResolveAsync(HttpClient http, string name, string env)
{
    var resp = await http.GetAsync($"/environments/{env}/prompts/{name}");
    return resp.StatusCode == HttpStatusCode.NotFound ? null
        : await resp.EnsureSuccess().Content.ReadFromJsonAsync<VersionDto>();
}

file sealed record VersionDto(int Version, string Template);

file static class HttpExtensions
{
    public static HttpResponseMessage EnsureSuccess(this HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return r;
    }
}
