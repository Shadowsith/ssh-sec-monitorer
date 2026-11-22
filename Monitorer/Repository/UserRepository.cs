using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Repository;

/// <summary>SQL for table "app_user".</summary>
public class UserRepository
{
    private readonly DbConnectionFactory _factory;

    public UserRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>SELECT — all users, ordered by user name.</summary>
    public async Task<IEnumerable<UserDb>> Select()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<UserDb>("SELECT * FROM app_user ORDER BY user_name");
    }

    /// <summary>SELECT — one user by id.</summary>
    public async Task<UserDb?> SelectById(int id)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<UserDb>(
            "SELECT * FROM app_user WHERE id = @id", new { id });
    }

    /// <summary>SELECT — one user by user name (case-insensitive).</summary>
    public async Task<UserDb?> SelectByUserName(string userName)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<UserDb>(
            "SELECT * FROM app_user WHERE lower(user_name) = lower(@userName)", new { userName });
    }

    /// <summary>SELECT COUNT — number of users.</summary>
    public async Task<int> SelectCount()
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM app_user");
    }

    /// <summary>SELECT COUNT — number of active admins.</summary>
    public async Task<int> SelectActiveAdminCount()
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM app_user WHERE role = @role AND is_active",
            new { role = UserRoles.Admin });
    }

    /// <summary>INSERT — returns the generated id.</summary>
    public async Task<int> Insert(UserDb user)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO app_user (user_name, display_name, email, password_hash, role, auth_source, is_active)
            VALUES (@UserName, @DisplayName, @Email, @PasswordHash, @Role, @AuthSource, @IsActive)
            RETURNING id
            """,
            user);
    }

    /// <summary>UPDATE — all editable fields.</summary>
    public async Task<bool> Update(UserDb user)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE app_user
            SET display_name = @DisplayName,
                email = @Email,
                password_hash = @PasswordHash,
                role = @Role,
                is_active = @IsActive
            WHERE id = @Id
            """,
            user);
        return rows > 0;
    }

    /// <summary>UPDATE — last_login_at.</summary>
    public async Task<bool> UpdateLastLoginAt(int id, DateTime lastLoginAt)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE app_user SET last_login_at = @lastLoginAt WHERE id = @id", new { id, lastLoginAt });
        return rows > 0;
    }

    /// <summary>DELETE — returns true if the row existed.</summary>
    public async Task<bool> Delete(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM app_user WHERE id = @id", new { id });
        return rows > 0;
    }
}
