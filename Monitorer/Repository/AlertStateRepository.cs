using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "alert_state".</summary>
public class AlertStateRepository
{
    private readonly DbConnectionFactory _factory;

    public AlertStateRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<AlertStateDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AlertStateDb>("SELECT * FROM alert_state ORDER BY first_detected_at");
    }

    public async Task<int> Insert(AlertStateDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO alert_state (rule_id, subject_key, subject_name, message, first_detected_at, last_sent_at, send_count)
            VALUES (@RuleId, @SubjectKey, @SubjectName, @Message, @FirstDetectedAt, @LastSentAt, @SendCount)
            RETURNING id
            """,
            row);
    }

    /// <summary>UPDATE — message and send bookkeeping of an active alert.</summary>
    public async Task<bool> Update(AlertStateDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE alert_state
            SET subject_name = @SubjectName,
                message = @Message,
                last_sent_at = @LastSentAt,
                send_count = @SendCount
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync("DELETE FROM alert_state WHERE id = @id", new { id }) > 0;
    }

    public async Task<int> DeleteByRuleId(int ruleId)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync("DELETE FROM alert_state WHERE rule_id = @ruleId", new { ruleId });
    }
}
