using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "live_check".</summary>
public class LiveCheckRepository
{
    private readonly DbConnectionFactory _factory;

    public LiveCheckRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<LiveCheckDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<LiveCheckDb>("SELECT * FROM live_check ORDER BY name");
    }

    public async Task<IEnumerable<LiveCheckDb>> SelectEnabled()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<LiveCheckDb>("SELECT * FROM live_check WHERE enabled ORDER BY name");
    }

    public async Task<LiveCheckDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<LiveCheckDb>("SELECT * FROM live_check WHERE id = @id", new { id });
    }

    public async Task<LiveCheckDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<LiveCheckDb>(
            "SELECT * FROM live_check WHERE lower(name) = lower(@name)", new { name });
    }

    public async Task<int> Insert(LiveCheckDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO live_check (name, check_type, target, interval_seconds, timeout_seconds, expected_status_code,
                                    ignore_tls_errors, mqtt_user_name, mqtt_password_enc, enabled)
            VALUES (@Name, @CheckType, @Target, @IntervalSeconds, @TimeoutSeconds, @ExpectedStatusCode,
                    @IgnoreTlsErrors, @MqttUserName, @MqttPasswordEnc, @Enabled)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(LiveCheckDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE live_check
            SET name = @Name,
                check_type = @CheckType,
                target = @Target,
                interval_seconds = @IntervalSeconds,
                timeout_seconds = @TimeoutSeconds,
                expected_status_code = @ExpectedStatusCode,
                ignore_tls_errors = @IgnoreTlsErrors,
                mqtt_user_name = @MqttUserName,
                mqtt_password_enc = @MqttPasswordEnc,
                enabled = @Enabled
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    /// <summary>UPDATE — the result of the last check run.</summary>
    public async Task<bool> UpdateResult(LiveCheckDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE live_check
            SET last_status = @LastStatus,
                last_checked_at = @LastCheckedAt,
                last_response_ms = @LastResponseMs,
                last_message = @LastMessage
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM live_check WHERE id = @id", new { id });
        return rows > 0;
    }
}
