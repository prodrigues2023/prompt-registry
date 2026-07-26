using PromptRegistry.Drills;

// Milestone 4 validation drills. Each prints what it observes and self-asserts (exit 0 = pass).
//
//   drills rollback [--registry http://localhost:8080]   (needs a running registry)
//   drills fleet                                          (self-contained, no server)
//   drills fallback                                       (self-contained, no server)

var command = args.FirstOrDefault();

return command switch
{
    "rollback" => await RollbackDrill.RunAsync(RegistryArg(args)) ? 0 : 1,
    "fleet" => await FleetDrill.RunAsync() ? 0 : 1,
    "fallback" => await FallbackDrill.RunAsync() ? 0 : 1,
    _ => Usage()
};

static Uri RegistryArg(string[] args)
{
    var i = Array.IndexOf(args, "--registry");
    var url = i >= 0 && i + 1 < args.Length ? args[i + 1]
        : Environment.GetEnvironmentVariable("REGISTRY_URL") ?? "http://localhost:8080";
    return new Uri(url);
}

static int Usage()
{
    Console.Error.WriteLine("usage: drills <rollback|fleet|fallback> [--registry <url>]");
    return 2;
}
