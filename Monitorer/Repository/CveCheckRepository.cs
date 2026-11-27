using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "cve_check".</summary>
public class CveCheckRepository
{
    private readonly DbConnectionFactory _factory;

    public CveCheckRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<CveCheckDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CveCheckDb>("SELECT * FROM cve_check ORDER BY name");
    }

    public async Task<IEnumerable<CveCheckDb>> SelectEnabled()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CveCheckDb>("SELECT * FROM cve_check WHERE enabled ORDER BY name");
    }

    public async Task<CveCheckDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<CveCheckDb>("SELECT * FROM cve_check WHERE id = @id", new { id });
    }

    public async Task<CveCheckDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<CveCheckDb>(
            "SELECT * FROM cve_check WHERE lower(name) = lower(@name)", new { name });
    }

    public async Task<int> Insert(CveCheckDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO cve_check (name, keywords, providers, interval_minutes, enabled, baseline_pending)
            VALUES (@Name, @Keywords, @Providers, @IntervalMinutes, @Enabled, @BaselinePending)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(CveCheckDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE cve_check
            SET name = @Name,
                keywords = @Keywords,
                providers = @Providers,
                interval_minutes = @IntervalMinutes,
                enabled = @Enabled,
                baseline_pending = @BaselinePending,
                kb_last_commit = @KbLastCommit
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    /// <summary>UPDATE — the result of the last run (incl. baseline flag and opencve-kb position).</summary>
    public async Task<bool> UpdateResult(CveCheckDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE cve_check
            SET last_status = @LastStatus,
                last_checked_at = @LastCheckedAt,
                last_message = @LastMessage,
                baseline_pending = @BaselinePending,
                kb_last_commit = @KbLastCommit
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM cve_check WHERE id = @id", new { id });
        return rows > 0;
    }
}
