using FluentMigrator.Runner;
using Monitorer.Data;

namespace Monitorer.Extensions;

/// <summary>Creates the database if needed and applies all pending FluentMigrator migrations.</summary>
public static class MigrationManager
{
    public static IHost MigrateDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().EnsureDatabase();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        return host;
    }
}
