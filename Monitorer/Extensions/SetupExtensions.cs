using Monitorer.Services;

namespace Monitorer.Extensions;

/// <summary>Startup helpers for the initial admin user.</summary>
public static class SetupExtensions
{
    public static async Task<IHost> SeedInitialAdmin(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SetupService>().SeedAdminFromSettings();
        return host;
    }
}
