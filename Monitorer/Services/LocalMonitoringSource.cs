using System.Text;
using Microsoft.Extensions.Options;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;

namespace Monitorer.Services;

/// <summary>
/// Reads the monitoring.json of a local agent (agent on the Monitorer host) from the read-only mounted
/// directory Monitoring:LocalAgentRoot/&lt;local_dir&gt;/monitoring.json.
/// Hardening: the path must stay below the root, symlinks (directory and file) are refused - otherwise a
/// compromised agent could point the Monitorer at e.g. /app/keys - and the size is limited.
/// </summary>
public class LocalMonitoringSource
{
    public const string FileName = "monitoring.json";

    private readonly MonitoringSettings _settings;

    public LocalMonitoringSource(IOptions<MonitoringSettings> settings) => _settings = settings.Value;

    public string Root => Path.GetFullPath(_settings.LocalAgentRoot);

    /// <summary>Full path of the agent's file inside the container, e.g. /app/agents/local/monitoring.json.</summary>
    public string PathFor(string localDir) => Path.Combine(Root, localDir, FileName);

    public async Task<MonitoringFetchResultDto> Read(AgentDb agent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agent.LocalDir))
            throw new InvalidOperationException("Für den lokalen Agent ist kein Ordner eingetragen.");

        var root = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var directory = Path.GetFullPath(Path.Combine(root, agent.LocalDir));
        if (!directory.StartsWith(root, StringComparison.Ordinal) || directory.Length == root.Length)
            throw new InvalidOperationException("Der Ordner des lokalen Agents liegt nicht unter Monitoring:LocalAgentRoot.");

        var dirInfo = new DirectoryInfo(directory);
        if (!dirInfo.Exists)
            throw new InvalidOperationException(
                $"Ordner {directory} nicht gefunden – ist das Volume des lokalen Agents im Monitorer gemountet?");
        if (IsLink(dirInfo))
            throw new InvalidOperationException($"{directory} ist ein symbolischer Link – aus Sicherheitsgründen abgelehnt.");

        var file = new FileInfo(Path.Combine(directory, FileName));
        if (!file.Exists)
            throw new InvalidOperationException($"{file.FullName} nicht gefunden – läuft der lokale Agent?");
        if (IsLink(file))
            throw new InvalidOperationException($"{file.FullName} ist ein symbolischer Link – aus Sicherheitsgründen abgelehnt.");
        if (file.Length > _settings.MaxFileBytes)
            throw new InvalidOperationException($"{FileName} ist {file.Length} Bytes groß (max. {_settings.MaxFileBytes}).");

        try
        {
            await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16384, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var buffer = new LimitedMemoryStream(_settings.MaxFileBytes);
            await stream.CopyToAsync(buffer, ct);
            return new MonitoringFetchResultDto { Content = Encoding.UTF8.GetString(buffer.ToArray()) };
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Keine Leserechte auf {file.FullName} – der Agent muss die Datei mit 0644 schreiben (output_file_mode).");
        }
    }

    private static bool IsLink(FileSystemInfo info) =>
        info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
}
