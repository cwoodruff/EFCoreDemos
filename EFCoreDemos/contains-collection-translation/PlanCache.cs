using Microsoft.Data.SqlClient;

namespace contains_collection_translation;

/// <summary>Reads (and tidies up) SQL Server's plan cache entries for the demo's tagged queries.</summary>
public static class PlanCache
{
    /// <summary>
    /// Number of cached plans whose SQL text contains <paramref name="tag"/>, and how many times they were reused.
    /// The tag is the query's TagWith comment, which SQL Server keeps as part of the statement text.
    /// </summary>
    public static async Task<(int Plans, long UseCount)> CountAsync(string connectionString, string tag)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), COALESCE(SUM(CAST(cp.usecounts AS bigint)), 0)
            FROM sys.dm_exec_cached_plans AS cp
            CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
            WHERE cp.cacheobjtype = N'Compiled Plan'
              AND st.text LIKE @pattern
              AND st.text NOT LIKE N'%dm_exec_cached_plans%';
            """;
        command.Parameters.AddWithValue("@pattern", "%" + tag + "%");

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.GetInt64(1));
    }

    /// <summary>
    /// Evicts only the plans whose SQL text contains <paramref name="tag"/> (DBCC FREEPROCCACHE with a plan
    /// handle), so the demo never flushes anything else out of the server's cache.
    /// </summary>
    public static async Task<int> EvictAsync(string connectionString, string tag)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @handle varbinary(64), @evicted int = 0;
            DECLARE plans CURSOR LOCAL FAST_FORWARD FOR
                SELECT cp.plan_handle
                FROM sys.dm_exec_cached_plans AS cp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
                WHERE st.text LIKE @pattern
                  AND st.text NOT LIKE N'%dm_exec_cached_plans%';
            OPEN plans;
            FETCH NEXT FROM plans INTO @handle;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                DBCC FREEPROCCACHE (@handle) WITH NO_INFOMSGS;
                SET @evicted += 1;
                FETCH NEXT FROM plans INTO @handle;
            END
            CLOSE plans;
            DEALLOCATE plans;
            SELECT @evicted;
            """;
        command.Parameters.AddWithValue("@pattern", "%" + tag + "%");

        return (int)(await command.ExecuteScalarAsync())!;
    }
}
