using System.Data;
using Npgsql;

namespace Monitorer.Data;

/// <summary>Hands out a new pooled PostgreSQL connection per call (singleton).</summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString) => _connectionString = connectionString;

    public IDbConnection Create() => new NpgsqlConnection(_connectionString);
}
