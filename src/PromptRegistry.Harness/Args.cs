namespace PromptRegistry.Harness;

public sealed record Options(
    string Prompt,
    int Candidate,
    string GoldenPath,
    string BaselineEnv,
    int Runs,
    double Tolerance,
    string Registry,
    bool Gate);

/// <summary>Tiny flag parser — no dependency, just the options promptcheck needs.</summary>
public static class Args
{
    public static Options? Parse(string[] args)
    {
        var map = new Dictionary<string, string>();
        var flags = new HashSet<string>();

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) return null;
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                map[key] = args[++i];
            else
                flags.Add(key);
        }

        if (!map.TryGetValue("prompt", out var prompt)) return null;
        if (!map.TryGetValue("candidate", out var candidateStr) || !int.TryParse(candidateStr, out var candidate)) return null;
        if (!map.TryGetValue("golden", out var golden)) return null;

        return new Options(
            Prompt: prompt,
            Candidate: candidate,
            GoldenPath: golden,
            BaselineEnv: map.GetValueOrDefault("baseline-env", "production"),
            Runs: int.TryParse(map.GetValueOrDefault("runs"), out var r) ? r : 5,
            Tolerance: double.TryParse(map.GetValueOrDefault("tolerance"), System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0.0,
            Registry: map.GetValueOrDefault("registry", "http://localhost:8080"),
            Gate: flags.Contains("gate"));
    }

    public static void PrintUsage() => Console.Error.WriteLine(
        """
        promptcheck — run a prompt version against its golden set and gate on regression.

          --prompt <name>          prompt to test                 (required)
          --candidate <version>    version under test             (required)
          --golden <path>          golden set JSON                (required)
          --baseline-env <env>     version to compare against     (default: production)
          --runs <n>               runs per case                  (default: 5)
          --tolerance <0..1>       allowed per-slice drop         (default: 0.0)
          --registry <url>         registry base URL              (default: http://localhost:8080)
          --gate                   write the verdict back as the promotion gate

        Exit code: 0 = no regression, 1 = regression, 2 = usage/lookup error.
        """);
}
