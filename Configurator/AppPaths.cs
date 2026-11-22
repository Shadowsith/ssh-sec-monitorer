namespace Configurator;

/// <summary>Where the Configurator keeps its local data (SQLite database).</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SshSecMonitorer", "Configurator");

    public static string DatabaseFile => Path.Combine(DataDirectory, "configurator.db");

    public static string ConnectionString => $"Data Source={DatabaseFile}";
}
