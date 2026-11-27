using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "cert_check".</summary>
public class CertCheckRepository
{
    private readonly DbConnectionFactory _factory;

    public CertCheckRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<CertCheckDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CertCheckDb>("SELECT * FROM cert_check ORDER BY name");
    }

    public async Task<IEnumerable<CertCheckDb>> SelectEnabled()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CertCheckDb>("SELECT * FROM cert_check WHERE enabled ORDER BY name");
    }

    public async Task<CertCheckDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<CertCheckDb>("SELECT * FROM cert_check WHERE id = @id", new { id });
    }

    public async Task<CertCheckDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<CertCheckDb>(
            "SELECT * FROM cert_check WHERE lower(name) = lower(@name)", new { name });
    }

    public async Task<int> Insert(CertCheckDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO cert_check (name, host, port, protocol, server_name, interval_seconds, timeout_seconds, enabled)
            VALUES (@Name, @Host, @Port, @Protocol, @ServerName, @IntervalSeconds, @TimeoutSeconds, @Enabled)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(CertCheckDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE cert_check
            SET name = @Name,
                host = @Host,
                port = @Port,
                protocol = @Protocol,
                server_name = @ServerName,
                interval_seconds = @IntervalSeconds,
                timeout_seconds = @TimeoutSeconds,
                enabled = @Enabled
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    /// <summary>UPDATE — the result of the last check run (certificate data included).</summary>
    public async Task<bool> UpdateResult(CertCheckDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE cert_check
            SET last_status = @LastStatus,
                last_checked_at = @LastCheckedAt,
                last_message = @LastMessage,
                not_before = @NotBefore,
                not_after = @NotAfter,
                subject = @Subject,
                issuer = @Issuer,
                thumbprint = @Thumbprint,
                chain_valid = @ChainValid
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM cert_check WHERE id = @id", new { id });
        return rows > 0;
    }
}
