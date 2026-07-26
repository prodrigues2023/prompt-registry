namespace PromptRegistry.Core;

/// <summary>
/// A stable reference an application uses instead of a string literal: <c>prompt://name@environment</c>.
/// The application names a prompt and an environment; the registry resolves that to a concrete version.
/// This format is a contract — every application that depends on the registry depends on it.
/// </summary>
public readonly record struct PromptReference(string Name, string Environment)
{
    public const string Scheme = "prompt://";

    public override string ToString() => $"{Scheme}{Name}@{Environment}";

    public static PromptReference Parse(string reference)
    {
        if (!TryParse(reference, out var result))
            throw new FormatException(
                $"Invalid prompt reference '{reference}'. Expected form: prompt://name@environment");
        return result;
    }

    public static bool TryParse(string reference, out PromptReference result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(reference)) return false;

        var body = reference.StartsWith(Scheme, StringComparison.Ordinal)
            ? reference[Scheme.Length..]
            : reference;

        var at = body.LastIndexOf('@');
        if (at <= 0 || at == body.Length - 1) return false;

        var name = body[..at];
        var env = body[(at + 1)..];
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(env)) return false;

        result = new PromptReference(name, env);
        return true;
    }
}
