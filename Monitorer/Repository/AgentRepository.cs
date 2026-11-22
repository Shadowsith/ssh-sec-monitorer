using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "agent".</summary>
public class AgentRepository
{
    private readonly DbConnectionFactory _factory;

    public AgentRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<AgentDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AgentDb>("SELECT * FROM agent ORDER BY name");
    }

    public async Task<IEnumerable<AgentDb>> SelectEnabled()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<AgentDb>("SELECT * FROM agent WHERE enabled ORDER BY name");
    }

    public async Task<AgentDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AgentDb>("SELECT * FROM agent WHERE id = @id", new { id });
    }

    public async Task<AgentDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AgentDb>(
            "SELECT * FROM agent WHERE lower(name) = lower(@name)", new { name });
    }

    public async Task<AgentDb?> SelectByLocalDir(string localDir)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<AgentDb>(
            "SELECT * FROM agent WHERE local_dir = @localDir", new { localDir });
    }

    public async Task<int> SelectCountBySshCredentialId(int sshCredentialId)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM agent WHERE ssh_credential_id = @sshCredentialId", new { sshCredentialId });
    }

    public async Task<int> Insert(AgentDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO agent (name, description, connection_type, host, port, ssh_credential_id, remote_path, local_dir,
                               poll_interval_seconds, enabled, host_key_fingerprint)
            VALUES (@Name, @Description, @ConnectionType, @Host, @Port, @SshCredentialId, @RemotePath, @LocalDir,
                    @PollIntervalSeconds, @Enabled, @HostKeyFingerprint)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(AgentDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE agent
            SET name = @Name,
                description = @Description,
                connection_type = @ConnectionType,
                host = @Host,
                port = @Port,
                ssh_credential_id = @SshCredentialId,
                remote_path = @RemotePath,
                local_dir = @LocalDir,
                poll_interval_seconds = @PollIntervalSeconds,
                enabled = @Enabled,
                host_key_fingerprint = @HostKeyFingerprint
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    /// <summary>UPDATE — stores the host key fingerprint learned on first connect.</summary>
    public async Task<bool> UpdateHostKeyFingerprint(int id, string? hostKeyFingerprint)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE agent SET host_key_fingerprint = @hostKeyFingerprint WHERE id = @id",
            new { id, hostKeyFingerprint });
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM agent WHERE id = @id", new { id });
        return rows > 0;
    }
}
