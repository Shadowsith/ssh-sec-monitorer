using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Monitorer.Data;

/// <summary>Creates the application database on startup if it does not exist yet.</summary>
public class DatabaseInitializer
{
    private readonly DbConfig _config;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IOptions<DbConfig> config, ILogger<DatabaseInitializer> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public void EnsureDatabase()
    {
        var databaseName = new NpgsqlConnectionStringBuilder(_config.ConnectionString).Database
                           ?? throw new InvalidOperationException("DbConfig:ConnectionString has no database.");

        using var conn = new NpgsqlConnection(_config.MasterConnectionString);
        var exists = conn.ExecuteScalar<int?>(
            "SELECT 1 FROM pg_database WHERE datname = @databaseName", new { databaseName });
        if (exists is not null)
            return;

        // identifiers cannot be parameters - quote the name safely
        var quoted = "\"" + databaseName.Replace("\"", "\"\"") + "\"";
        conn.Execute($"CREATE DATABASE {quoted}");
        _logger.LogInformation("Database {Database} created", databaseName);
    }
}
