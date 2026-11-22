using System.Data;
using Microsoft.Data.Sqlite;

namespace Configurator.Data;

/// <summary>Hands out a new SQLite connection per call.</summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString) => _connectionString = connectionString;

    public IDbConnection Create() => new SqliteConnection(_connectionString);
}
