using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using PromptRegistry.Core;

namespace PromptRegistry.Api;

/// <summary>Raised when a promotion is blocked because the version has not passed its gate.</summary>
public sealed class PromoteBlockedException(string message) : Exception(message);

/// <summary>
/// All data access. Publishing appends; promoting and rolling back move an alias pointer.
/// Nothing here ever mutates a published version.
/// </summary>
public sealed class PromptStore(NpgsqlDataSource db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task MigrateAsync(string migrationsDir)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using (var cmd = new NpgsqlCommand(
            "create table if not exists schema_migrations (id text primary key, applied_at timestamptz not null default now())",
            conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var file in Directory.EnumerateFiles(migrationsDir, "*.sql").OrderBy(f => f))
        {
            var id = Path.GetFileName(file);
            await using var check = new NpgsqlCommand("select 1 from schema_migrations where id = @id", conn);
            check.Parameters.AddWithValue("id", id);
            if (await check.ExecuteScalarAsync() is not null) continue;

            var sql = await File.ReadAllTextAsync(file);
            await using var apply = new NpgsqlCommand(sql, conn);
            await apply.ExecuteNonQueryAsync();
            await using var mark = new NpgsqlCommand("insert into schema_migrations (id) values (@id)", conn);
            mark.Parameters.AddWithValue("id", id);
            await mark.ExecuteNonQueryAsync();
        }
    }

    public async Task<PromptVersion> PublishAsync(string name, PublishRequest req)
    {
        var variables = req.Variables ?? Array.Empty<string>();
        var metadata = req.Metadata ?? new Dictionary<string, string>();
        var hash = CanonicalHash.Compute(req.Template, variables, metadata);

        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var next = new NpgsqlCommand(
            "select coalesce(max(version), 0) + 1 from prompt_versions where name = @name", conn, tx);
        next.Parameters.AddWithValue("name", name);
        var version = Convert.ToInt32(await next.ExecuteScalarAsync());

        await using var insert = new NpgsqlCommand(
            """
            insert into prompt_versions (name, version, template, variables, metadata, content_hash, gate_status)
            values (@name, @version, @template, @variables, @metadata, @hash, 'untested')
            returning created_at
            """, conn, tx);
        insert.Parameters.AddWithValue("name", name);
        insert.Parameters.AddWithValue("version", version);
        insert.Parameters.AddWithValue("template", req.Template);
        insert.Parameters.Add(new NpgsqlParameter("variables", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(variables) });
        insert.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(metadata) });
        insert.Parameters.AddWithValue("hash", hash);
        var createdAt = (DateTime)(await insert.ExecuteScalarAsync())!;

        await tx.CommitAsync();

        return new PromptVersion(name, version, req.Template, variables.ToArray(),
            metadata.ToDictionary(kv => kv.Key, kv => kv.Value), hash, GateStatus.Untested, null, createdAt);
    }

    public async Task<PromptVersion?> GetAsync(string name, int version)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"{SelectVersion} where name = @name and version = @version", conn);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("version", version);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<PromptVersion>> HistoryAsync(string name)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"{SelectVersion} where name = @name order by version desc", conn);
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<PromptVersion>();
        while (await reader.ReadAsync()) list.Add(Map(reader));
        return list;
    }

    public async Task<PromptVersion?> RecordTestAsync(string name, int version, TestResultRequest req)
    {
        var status = req.Passed ? GateStatus.Passed : GateStatus.Failed;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            update prompt_versions set gate_status = @status, test_results = @results
            where name = @name and version = @version
            """, conn);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.Add(new NpgsqlParameter("results", NpgsqlDbType.Jsonb)
        { Value = JsonSerializer.Serialize(req.Details) });
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("version", version);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows == 0 ? null : await GetAsync(name, version);
    }

    /// <summary>Point an environment alias at a version. Blocked unless the version passed its gate.</summary>
    public async Task<(int? from, int to)> PromoteAsync(string name, string env, int version, bool force)
    {
        var target = await GetAsync(name, version)
            ?? throw new PromoteBlockedException($"Version {version} of '{name}' does not exist.");

        if (!force && target.GateStatus != GateStatus.Passed)
            throw new PromoteBlockedException(
                $"Version {version} of '{name}' has gate status '{target.GateStatus}'. " +
                "A version must pass its golden-set test before promotion (or use force).");

        return await MoveAliasAsync(name, env, version, "promote");
    }

    /// <summary>Re-point an alias to the version it held before the most recent move.</summary>
    public async Task<(int? from, int to)?> RollbackAsync(string name, string env)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var prev = new NpgsqlCommand(
            """
            select from_version from alias_history
            where name = @name and environment = @env
            order by changed_at desc limit 1
            """, conn);
        prev.Parameters.AddWithValue("name", name);
        prev.Parameters.AddWithValue("env", env);
        var result = await prev.ExecuteScalarAsync();
        if (result is null or DBNull)
            return null; // nothing to roll back to

        var to = Convert.ToInt32(result);
        return await MoveAliasAsync(name, env, to, "rollback");
    }

    private async Task<(int? from, int to)> MoveAliasAsync(string name, string env, int version, string action)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var cur = new NpgsqlCommand(
            "select version from aliases where name = @name and environment = @env", conn, tx);
        cur.Parameters.AddWithValue("name", name);
        cur.Parameters.AddWithValue("env", env);
        var currentObj = await cur.ExecuteScalarAsync();
        int? from = currentObj is null or DBNull ? null : Convert.ToInt32(currentObj);

        await using var upsert = new NpgsqlCommand(
            """
            insert into aliases (name, environment, version, updated_at)
            values (@name, @env, @version, now())
            on conflict (name, environment) do update set version = @version, updated_at = now()
            """, conn, tx);
        upsert.Parameters.AddWithValue("name", name);
        upsert.Parameters.AddWithValue("env", env);
        upsert.Parameters.AddWithValue("version", version);
        await upsert.ExecuteNonQueryAsync();

        await using var log = new NpgsqlCommand(
            """
            insert into alias_history (name, environment, from_version, to_version, action)
            values (@name, @env, @from, @to, @action)
            """, conn, tx);
        log.Parameters.AddWithValue("name", name);
        log.Parameters.AddWithValue("env", env);
        log.Parameters.AddWithValue("from", (object?)from ?? DBNull.Value);
        log.Parameters.AddWithValue("to", version);
        log.Parameters.AddWithValue("action", action);
        await log.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return (from, version);
    }

    public async Task<IReadOnlyList<(string Environment, int Version)>> AliasesAsync(string name)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "select environment, version from aliases where name = @name order by environment", conn);
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<(string, int)>();
        while (await reader.ReadAsync())
            list.Add((reader.GetString(0), reader.GetInt32(1)));
        return list;
    }

    public async Task<ResolvedPrompt?> ResolveAsync(string name, string env)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            select v.version, v.template, v.variables::text, v.content_hash
            from aliases a
            join prompt_versions v on v.name = a.name and v.version = a.version
            where a.name = @name and a.environment = @env
            """, conn);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("env", env);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var version = reader.GetInt32(0);
        var template = reader.GetString(1);
        var variables = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new();
        var hash = reader.GetString(3);
        return new ResolvedPrompt(name, env, version, template, variables, hash);
    }

    private const string SelectVersion =
        "select name, version, template, variables::text, metadata::text, content_hash, gate_status, test_results::text, created_at from prompt_versions";

    private static PromptVersion Map(NpgsqlDataReader r)
    {
        var variables = JsonSerializer.Deserialize<List<string>>(r.GetString(3)) ?? new();
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(4)) ?? new();
        object? testResults = r.IsDBNull(7) ? null : JsonSerializer.Deserialize<JsonElement>(r.GetString(7));
        return new PromptVersion(
            r.GetString(0), r.GetInt32(1), r.GetString(2), variables, metadata,
            r.GetString(5), r.GetString(6), testResults, r.GetFieldValue<DateTime>(8));
    }
}
