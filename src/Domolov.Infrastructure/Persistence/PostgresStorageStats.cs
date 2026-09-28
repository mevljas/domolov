using Domolov.Application.Abstractions;
using Npgsql;

namespace Domolov.Infrastructure.Persistence;

/// <summary>Reads database and table sizes from PostgreSQL catalogs.</summary>
public sealed class PostgresStorageStats(NpgsqlDataSource dataSource) : IStorageStats
{
    private const string Sql = """
        SELECT pg_database_size(current_database());
        SELECT c.relname,
               pg_total_relation_size(c.oid) AS bytes,
               GREATEST(c.reltuples, 0)::bigint AS rows
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind = 'r' AND n.nspname = current_schema()
        ORDER BY bytes DESC
        LIMIT 20;
        """;

    public async Task<DatabaseStorage> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var cmd = dataSource.CreateCommand(Sql);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        long databaseBytes = 0;
        if (await reader.ReadAsync(cancellationToken))
        {
            databaseBytes = reader.GetInt64(0);
        }

        var tables = new List<TableStorage>();
        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(
                    new TableStorage(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2))
                );
            }
        }

        return new DatabaseStorage(databaseBytes, tables);
    }
}
