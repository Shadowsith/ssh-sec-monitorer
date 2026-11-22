using Configurator.Data;
using Configurator.Data.Db;
using Dapper;

namespace Configurator.Repository;

/// <summary>SQL for table "agent_config".</summary>
public class AgentConfigRepository
{
    private readonly DbConnectionFactory _factory;

    public AgentConfigRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<AgentConfigDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AgentConfigDb>("SELECT * FROM agent_config ORDER BY name COLLATE NOCASE");
    }

    public async Task<AgentConfigDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AgentConfigDb>("SELECT * FROM agent_config WHERE id = @id", new { id });
    }

    public async Task<AgentConfigDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AgentConfigDb>(
            "SELECT * FROM agent_config WHERE name = @name COLLATE NOCASE", new { name });
    }

    public async Task<int> Insert(AgentConfigDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO agent_config (name, description, settings_json, created_at, updated_at)
            VALUES (@Name, @Description, @SettingsJson, @CreatedAt, @UpdatedAt)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(AgentConfigDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE agent_config
            SET name = @Name, description = @Description, settings_json = @SettingsJson, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync("DELETE FROM agent_config WHERE id = @id", new { id }) > 0;
    }
}
