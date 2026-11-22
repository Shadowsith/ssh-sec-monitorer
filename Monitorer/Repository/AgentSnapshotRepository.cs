using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "agent_snapshot" (one row per agent).</summary>
public class AgentSnapshotRepository
{
    private readonly DbConnectionFactory _factory;

    public AgentSnapshotRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>SELECT — all snapshots without the (large) payload.</summary>
    public async Task<IEnumerable<AgentSnapshotDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AgentSnapshotDb>(
            """
            SELECT agent_id, last_attempt_at, fetched_at, generated_at, status, message, NULL::text AS payload,
                   cpu_percent, memory_percent, disk_percent_max, containers_running, containers_total
            FROM agent_snapshot
            """);
    }

    /// <summary>SELECT — all snapshots including the payload (Docker overview).</summary>
    public async Task<IEnumerable<AgentSnapshotDb>> SelectWithPayload()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AgentSnapshotDb>(
            "SELECT agent_id, last_attempt_at, fetched_at, generated_at, status, message, payload::text AS payload, " +
            "cpu_percent, memory_percent, disk_percent_max, containers_running, containers_total FROM agent_snapshot");
    }

    /// <summary>SELECT — the snapshot of one agent including the payload.</summary>
    public async Task<AgentSnapshotDb?> SelectByAgentId(int agentId)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AgentSnapshotDb>(
            "SELECT agent_id, last_attempt_at, fetched_at, generated_at, status, message, payload::text AS payload, " +
            "cpu_percent, memory_percent, disk_percent_max, containers_running, containers_total " +
            "FROM agent_snapshot WHERE agent_id = @agentId",
            new { agentId });
    }

    /// <summary>INSERT … ON CONFLICT UPDATE — writes the latest snapshot of an agent.</summary>
    public async Task Upsert(AgentSnapshotDb row)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO agent_snapshot (agent_id, last_attempt_at, fetched_at, generated_at, status, message, payload,
                                        cpu_percent, memory_percent, disk_percent_max, containers_running, containers_total)
            VALUES (@AgentId, @LastAttemptAt, @FetchedAt, @GeneratedAt, @Status, @Message, @Payload::jsonb,
                    @CpuPercent, @MemoryPercent, @DiskPercentMax, @ContainersRunning, @ContainersTotal)
            ON CONFLICT (agent_id) DO UPDATE
            SET last_attempt_at = EXCLUDED.last_attempt_at,
                fetched_at = EXCLUDED.fetched_at,
                generated_at = EXCLUDED.generated_at,
                status = EXCLUDED.status,
                message = EXCLUDED.message,
                payload = EXCLUDED.payload,
                cpu_percent = EXCLUDED.cpu_percent,
                memory_percent = EXCLUDED.memory_percent,
                disk_percent_max = EXCLUDED.disk_percent_max,
                containers_running = EXCLUDED.containers_running,
                containers_total = EXCLUDED.containers_total
            """,
            row);
    }

    /// <summary>UPDATE — only the attempt / status after a failed fetch; keeps the last good payload.</summary>
    public async Task<bool> UpdateFailure(int agentId, DateTime lastAttemptAt, string status, string message)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE agent_snapshot
            SET last_attempt_at = @lastAttemptAt, status = @status, message = @message
            WHERE agent_id = @agentId
            """,
            new { agentId, lastAttemptAt, status, message });
        return rows > 0;
    }

    public async Task<bool> Delete(int agentId)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM agent_snapshot WHERE agent_id = @agentId", new { agentId });
        return rows > 0;
    }
}
