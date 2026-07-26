using System.Text.RegularExpressions;

namespace PromptRegistry.Core;

/// <summary>
/// Renders a prompt template by substituting <c>{{variable}}</c> tokens with case data.
/// Data is data: a value is inserted verbatim, never interpreted as another token or instruction.
/// </summary>
public static partial class PromptTemplate
{
    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex Token();

    public static string Render(string template, IReadOnlyDictionary<string, string> variables) =>
        Token().Replace(template, m =>
            variables.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
}
