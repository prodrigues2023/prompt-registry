namespace PromptRegistry.Core;

/// <summary>
/// An immutable, versioned prompt artifact. Once published, a (Name, Version) pair
/// never changes — that immutability is what makes test, promote, and rollback possible.
/// </summary>
public record PromptVersion(
    string Name,
    int Version,
    string Template,
    IReadOnlyList<string> Variables,
    IReadOnlyDictionary<string, string> Metadata,
    string ContentHash,
    string GateStatus,          // untested | passed | failed
    object? TestResults,
    DateTimeOffset CreatedAt);

/// <summary>Body of a publish request. The registry assigns the version number.</summary>
public record PublishRequest(
    string Template,
    IReadOnlyList<string>? Variables,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>Records the outcome of running a version against its golden set.</summary>
public record TestResultRequest(bool Passed, object? Details);

/// <summary>Body of a promotion request: point an environment alias at a version.</summary>
public record PromoteRequest(int Version, bool Force = false);

/// <summary>
/// What an application receives when it resolves a reference like <c>prompt://checkout-summary@production</c>.
/// The concrete version the alias currently points to.
/// </summary>
public record ResolvedPrompt(
    string Name,
    string Environment,
    int Version,
    string Template,
    IReadOnlyList<string> Variables,
    string ContentHash);

/// <summary>Gate statuses a version can hold.</summary>
public static class GateStatus
{
    public const string Untested = "untested";
    public const string Passed = "passed";
    public const string Failed = "failed";
}
