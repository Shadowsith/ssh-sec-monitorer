using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "alert_rule".</summary>
public class AlertRuleRepository
{
    private readonly DbConnectionFactory _factory;

    public AlertRuleRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<AlertRuleDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AlertRuleDb>("SELECT * FROM alert_rule ORDER BY name");
    }

    public async Task<IEnumerable<AlertRuleDb>> SelectEnabled()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AlertRuleDb>("SELECT * FROM alert_rule WHERE enabled ORDER BY name");
    }

    public async Task<AlertRuleDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AlertRuleDb>("SELECT * FROM alert_rule WHERE id = @id", new { id });
    }

    public async Task<AlertRuleDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AlertRuleDb>(
            "SELECT * FROM alert_rule WHERE lower(name) = lower(@name)", new { name });
    }

    public async Task<int> Insert(AlertRuleDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO alert_rule (name, scenario, agent_id, live_check_id, cert_check_id, container_pattern,
                                    threshold_minutes, threshold_days, min_status, ignore_clean_exit, delay_minutes, repeat_minutes, notify_recovery,
                                    recipients, enabled)
            VALUES (@Name, @Scenario, @AgentId, @LiveCheckId, @CertCheckId, @ContainerPattern,
                    @ThresholdMinutes, @ThresholdDays, @MinStatus, @IgnoreCleanExit, @DelayMinutes, @RepeatMinutes, @NotifyRecovery,
                    @Recipients, @Enabled)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(AlertRuleDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE alert_rule
            SET name = @Name,
                scenario = @Scenario,
                agent_id = @AgentId,
                live_check_id = @LiveCheckId,
                cert_check_id = @CertCheckId,
                container_pattern = @ContainerPattern,
                threshold_minutes = @ThresholdMinutes,
                threshold_days = @ThresholdDays,
                min_status = @MinStatus,
                ignore_clean_exit = @IgnoreCleanExit,
                delay_minutes = @DelayMinutes,
                repeat_minutes = @RepeatMinutes,
                notify_recovery = @NotifyRecovery,
                recipients = @Recipients,
                enabled = @Enabled
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync("DELETE FROM alert_rule WHERE id = @id", new { id }) > 0;
    }
}
