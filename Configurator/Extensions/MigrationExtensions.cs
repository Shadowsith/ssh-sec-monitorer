using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace Configurator.Extensions;

/// <summary>Applies all pending FluentMigrator migrations to the local SQLite database.</summary>
public static class MigrationExtensions
{
    public static IServiceProvider MigrateDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        return services;
    }
}
