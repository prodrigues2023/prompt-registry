using Npgsql;
using PromptRegistry.Api;
using PromptRegistry.Core;

var builder = WebApplication.CreateBuilder(args);

var connString = Environment.GetEnvironmentVariable("PG_CONN")
    ?? "Host=localhost;Port=5432;Username=registry;Password=registry;Database=registry";

builder.Services.AddSingleton(NpgsqlDataSource.Create(connString));
builder.Services.AddSingleton<PromptStore>();

var app = builder.Build();

// Apply migrations at startup so `make up` is genuinely one command.
var migrationsDir = Environment.GetEnvironmentVariable("MIGRATIONS_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "db", "migrations");
if (Directory.Exists(migrationsDir))
    await app.Services.GetRequiredService<PromptStore>().MigrateAsync(migrationsDir);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// --- Versions (append-only) ---------------------------------------------------------------

app.MapPost("/prompts/{name}/versions", async (string name, PublishRequest req, PromptStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Template))
        return Results.BadRequest(new { error = "template is required" });
    var created = await store.PublishAsync(name, req);
    return Results.Created($"/prompts/{name}/versions/{created.Version}", created);
});

app.MapGet("/prompts/{name}/versions", async (string name, PromptStore store) =>
    Results.Ok(await store.HistoryAsync(name)));

app.MapGet("/prompts/{name}/versions/{version:int}", async (string name, int version, PromptStore store) =>
    await store.GetAsync(name, version) is { } v ? Results.Ok(v) : Results.NotFound());

app.MapPost("/prompts/{name}/versions/{version:int}/test",
    async (string name, int version, TestResultRequest req, PromptStore store) =>
    await store.RecordTestAsync(name, version, req) is { } v ? Results.Ok(v) : Results.NotFound());

app.MapGet("/prompts/{name}/aliases", async (string name, PromptStore store) =>
    Results.Ok((await store.AliasesAsync(name)).Select(a => new { environment = a.Environment, version = a.Version })));

// --- Environment aliases (the only mutable pointer) ---------------------------------------

app.MapPut("/environments/{env}/prompts/{name}",
    async (string env, string name, PromoteRequest req, PromptStore store) =>
{
    try
    {
        var (from, to) = await store.PromoteAsync(name, env, req.Version, req.Force);
        return Results.Ok(new { name, environment = env, from, to, action = "promote" });
    }
    catch (PromoteBlockedException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/environments/{env}/prompts/{name}/rollback",
    async (string env, string name, PromptStore store) =>
{
    var moved = await store.RollbackAsync(name, env);
    return moved is { } m
        ? Results.Ok(new { name, environment = env, from = m.from, to = m.to, action = "rollback" })
        : Results.BadRequest(new { error = "no previous version to roll back to" });
});

// The endpoint applications actually call, via the client library, to resolve prompt://name@env.
app.MapGet("/environments/{env}/prompts/{name}",
    async (string env, string name, PromptStore store) =>
    await store.ResolveAsync(name, env) is { } r ? Results.Ok(r) : Results.NotFound());

app.Run();

public partial class Program { }
