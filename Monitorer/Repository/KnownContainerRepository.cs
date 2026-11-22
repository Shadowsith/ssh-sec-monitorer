using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "known_container".</summary>
public class KnownContainerRepository
{
    private readonly DbConnectionFactory _factory;

    public KnownContainerRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<KnownContainerDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<KnownContainerDb>("SELECT * FROM known_container ORDER BY agent_id, name");
    }

    public async Task<IEnumerable<KnownContainerDb>> SelectByAgentId(int agentId)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<KnownContainerDb>(
            "SELECT * FROM known_container WHERE agent_id = @agentId ORDER BY name", new { agentId });
    }

    public async Task<KnownContainerDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<KnownContainerDb>(
            "SELECT * FROM known_container WHERE id = @id", new { id });
    }

    /// <summary>INSERT … ON CONFLICT UPDATE — marks a container as seen now (first_seen_at is kept).</summary>
    public async Task Upsert(KnownContainerDb row)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO known_container (agent_id, name, image, last_state, first_seen_at, last_seen_at)
            VALUES (@AgentId, @Name, @Image, @LastState, @FirstSeenAt, @LastSeenAt)
            ON CONFLICT (agent_id, name) DO UPDATE
            SET image = EXCLUDED.image,
                last_state = EXCLUDED.last_state,
                last_seen_at = EXCLUDED.last_seen_at
            """,
            row);
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync("DELETE FROM known_container WHERE id = @id", new { id }) > 0;
    }
}
