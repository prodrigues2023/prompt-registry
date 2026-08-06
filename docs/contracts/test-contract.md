# Test contract

**How a golden set and its assertions attach to a prompt, and what "the gate" actually checks —
as [`promptcheck`](../../src/PromptRegistry.Harness) implements it.** [ADR-0004](../adr/0004-regression-testing.md)
decided a candidate version is compared against the currently-promoted baseline, per slice, before
promotion; this is the concrete file format and evaluation mechanics behind that decision.

## The golden set

A golden set is a plain JSON file, loaded by [`GoldenSet.Load`](../../src/PromptRegistry.Harness/GoldenSet.cs)
(`camelCase`, standard `System.Text.Json` web defaults):

```json
{
  "prompt": "checkout-summary",
  "cases": [
    {
      "id": "typical-1",
      "slice": "typical",
      "variables": { "customer": "Ana Silva", "order_id": "AC-1042", "items": "3", "total": "$248.90" },
      "assertions": [
        { "type": "contains", "value": "Ana Silva" },
        { "type": "max_length", "value": "200" }
      ]
    }
  ]
}
```

| Field | Type | Notes |
| --- | --- | --- |
| `prompt` | `string` | The prompt name this golden set is meant for. **Informational only** — nothing checks it matches the `--prompt` the harness was actually invoked with. |
| `cases[].id` | `string` | Case identifier, used in reports. |
| `cases[].slice` | `string` | Groups cases for per-slice scoring — the unit [ADR-0004](../adr/0004-regression-testing.md)'s "is it at least as good, everywhere" checks, not just an overall average. |
| `cases[].variables` | `map<string,string>` | Rendered into the template's `{{tokens}}` before evaluation. |
| `cases[].assertions[].type` | `string` | One of `contains`, `not_contains`, `max_length`, `matches` (regex), `is_json`. An unrecognized type throws `NotSupportedException` — there is no silent skip. |
| `cases[].assertions[].value` | `string?` | The assertion's argument (the substring, the pattern, the max length as a string, etc.). |

**A golden set is not attached to a `PromptVersion` in the registry.** There is no `golden_set_id`
column, no foreign key, nothing in [`db/migrations/001_init.sql`](../../db/migrations/001_init.sql)
associating a version with a set of test cases. The file lives on disk, referenced by path
(`--golden <path>`) at harness invocation time, and is linked to a prompt only by the `prompt` field
and by naming convention (`samples/golden/checkout-summary.golden.json`) — not by any
registry-enforced attachment. Anyone running `promptcheck` chooses which file to test against; the
registry has no opinion.

## Evaluation

[`Evaluator.Evaluate`](../../src/PromptRegistry.Harness/Evaluator.cs) — for each case: render the
template with that case's `variables`, run it `runs` times (default 5) through `IPromptModel`
(the only implementation is `StubModel` — an offline, deterministic-per-seed stand-in; no real
model is called anywhere in this repository, consistent with [context.md](../context.md)'s
"stubbed model, no cloud account" constraint), and score the case as the fraction of runs where
**every** assertion passed. A slice's score is the mean of its cases' scores; the overall score is
the mean of slice scores.

```csharp
public sealed record CaseResult(string Id, string Slice, double Score, IReadOnlyList<string> Failures);
public sealed record SliceScore(string Slice, double Score, int Cases);
public sealed record EvaluationResult(int Version, IReadOnlyList<SliceScore> Slices, IReadOnlyList<CaseResult> Cases, double Overall);
```

## The gate

[`RegressionGate.Evaluate`](../../src/PromptRegistry.Harness/RegressionGate.cs) compares a
candidate's `EvaluationResult` against a baseline's, **per slice**:

```csharp
public sealed record SliceDelta(string Slice, double Candidate, double? Baseline, double? Delta, bool Regressed);
public sealed record GateVerdict(bool Passed, string Reason, IReadOnlyList<SliceDelta> Slices);
```

A slice `Regressed` if `Candidate < Baseline - tolerance` (default `tolerance = 0.0`, i.e. any drop
at all fails). The gate `Passed` only if **no slice regressed** — one degraded slice blocks
promotion even if the overall average improved, which is the entire point of scoring per slice
rather than a single blended number ([ADR-0004](../adr/0004-regression-testing.md)). A version with
no baseline (nothing promoted yet for that environment) auto-passes.

## Recording the result

`promptcheck --gate` posts to `POST /prompts/{name}/versions/{version}/test`:

```csharp
new {
    passed = verdict.Passed,
    details = new {
        harness = "promptcheck", runs, tolerance,
        baseline_version, candidate_overall,
        slices = [...{ Slice, Candidate, Baseline, Delta, Regressed }],
        reason,
    }
}
```

This is exactly what lands in `PromptVersion.TestResults` — an opaque JSON blob whose shape is
`promptcheck`'s own report, not a registry-defined schema. `GateStatus` moves from `untested` to
`passed` or `failed` based on the posted `passed` boolean. A `PromoteRequest` for a version whose
`GateStatus != "passed"` is rejected with HTTP `409 Conflict` — unless the caller sets `Force = true`
(`--force`), which promotes anyway with no record of who forced it or why (see
[ADR-0007](../adr/0007-access-control-and-change-approval.md) for why: there is no caller identity
anywhere in this system to attach to that record).

## Related

- [Prompt artifact schema](./prompt-artifact-schema.md) — `GateStatus` and `TestResults`
- [ADR-0004](../adr/0004-regression-testing.md), [ADR-0006](../adr/0006-prompt-artifact-and-reference-format.md)
- [`scripts/regression.sh`](../../scripts/regression.sh) — an end-to-end run of this contract:
  publish, gate, promote, publish a regression, gate blocks it, promotion attempt returns 409
