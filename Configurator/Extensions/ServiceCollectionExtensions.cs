using Configurator.Data;
using Configurator.Repository;
using Configurator.Services;
using Configurator.ViewModels;
using Dapper;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace Configurator.Extensions;

/// <summary>DI registration of the Configurator.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguratorServices(this IServiceCollection services)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        Directory.CreateDirectory(AppPaths.DataDirectory);

        services.AddLogging();
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite()
                .WithGlobalConnectionString(AppPaths.ConnectionString)
                .ScanIn(typeof(App).Assembly).For.Migrations());

        services.AddSingleton(new DbConnectionFactory(AppPaths.ConnectionString));
        services.AddSingleton<AgentConfigRepository>();
        services.AddSingleton<ConfigYamlService>();
        services.AddSingleton<AgentConfigService>();
        services.AddTransient<MainWindowViewModel>();
        return services;
    }
}
