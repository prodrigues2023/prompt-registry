namespace PromptRegistry.Harness;

/// <summary>A prompt-driven model. The registry gates whatever this abstracts over.</summary>
public interface IPromptModel
{
    string Complete(string renderedPrompt, IReadOnlyDictionary<string, string> variables, int runIndex);
}

/// <summary>
/// A local, offline stand-in for an LLM so the harness runs on a laptop with no cloud account
/// (a key constraint from context.md). It is NOT a language model — it is a deterministic-enough
/// simulation whose only job is to make the harness demonstrable:
///
///  - it composes an order summary from the case's data variables;
///  - it honours a brevity instruction in the prompt by dropping the order number and total —
///    the realistic regression a "be extremely brief" rewrite would cause;
///  - it treats variables as data, never as instructions.
///
/// The illustrative non-determinism (an optional closing line) is why the harness runs each case
/// several times and aggregates, rather than trusting a single sample — see ADR-0004.
/// </summary>
public sealed class StubModel(int seed = 1234) : IPromptModel
{
    public string Complete(string renderedPrompt, IReadOnlyDictionary<string, string> variables, int runIndex)
    {
        var rng = new Random(HashCode.Combine(seed, renderedPrompt, runIndex));
        var brief = renderedPrompt.Contains("brief", StringComparison.OrdinalIgnoreCase);

        var customer = variables.GetValueOrDefault("customer", "the customer");
        var orderId = variables.GetValueOrDefault("order_id", "");
        var items = variables.GetValueOrDefault("items", "0");
        var total = variables.GetValueOrDefault("total", "");

        // Brevity drops the identifying fields — the regression the completeness slice is built to catch.
        var body = brief
            ? $"Order for {customer}: {items} items."
            : $"Order {orderId} for {customer}: {items} items, total {total}.";

        // Illustrative variation that never touches the required fields.
        if (!brief && rng.NextDouble() < 0.5)
            body += " Thank you for your purchase.";

        return body;
    }
}
