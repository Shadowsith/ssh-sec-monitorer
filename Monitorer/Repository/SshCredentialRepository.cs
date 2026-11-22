using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "ssh_credential".</summary>
public class SshCredentialRepository
{
    private readonly DbConnectionFactory _factory;

    public SshCredentialRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<SshCredentialDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<SshCredentialDb>("SELECT * FROM ssh_credential ORDER BY name");
    }

    public async Task<SshCredentialDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<SshCredentialDb>(
            "SELECT * FROM ssh_credential WHERE id = @id", new { id });
    }

    public async Task<SshCredentialDb?> SelectByName(string name)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<SshCredentialDb>(
            "SELECT * FROM ssh_credential WHERE lower(name) = lower(@name)", new { name });
    }

    public async Task<int> Insert(SshCredentialDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO ssh_credential (name, user_name, auth_type, private_key_enc, passphrase_enc, password_enc)
            VALUES (@Name, @UserName, @AuthType, @PrivateKeyEnc, @PassphraseEnc, @PasswordEnc)
            RETURNING id
            """,
            row);
    }

    public async Task<bool> Update(SshCredentialDb row)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE ssh_credential
            SET name = @Name,
                user_name = @UserName,
                auth_type = @AuthType,
                private_key_enc = @PrivateKeyEnc,
                passphrase_enc = @PassphraseEnc,
                password_enc = @PasswordEnc,
                updated_at = now()
            WHERE id = @Id
            """,
            row);
        return rows > 0;
    }

    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM ssh_credential WHERE id = @id", new { id });
        return rows > 0;
    }
}
