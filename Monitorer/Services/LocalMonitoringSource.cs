using System.Text;
using Microsoft.Extensions.Localization;
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
    private readonly IStringLocalizer _l;

    public LocalMonitoringSource(IOptions<MonitoringSettings> settings, IStringLocalizer l)
    {
        _settings = settings.Value;
        _l = l;
    }

    public string Root => Path.GetFullPath(_settings.LocalAgentRoot);

    /// <summary>Full path of the agent's file inside the container, e.g. /app/agents/local/monitoring.json.</summary>
    public string PathFor(string localDir) => Path.Combine(Root, localDir, FileName);

    public async Task<MonitoringFetchResultDto> Read(AgentDb agent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agent.LocalDir))
            throw new InvalidOperationException(_l["local.noDir"].Value);

        var root = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var directory = Path.GetFullPath(Path.Combine(root, agent.LocalDir));
        if (!directory.StartsWith(root, StringComparison.Ordinal) || directory.Length == root.Length)
            throw new InvalidOperationException(_l["local.outsideRoot"].Value);

        var dirInfo = new DirectoryInfo(directory);
        if (!dirInfo.Exists)
            throw new InvalidOperationException(_l["local.dirNotFound", directory].Value);
        if (IsLink(dirInfo))
            throw new InvalidOperationException(_l["local.symlink", directory].Value);

        var file = new FileInfo(Path.Combine(directory, FileName));
        if (!file.Exists)
            throw new InvalidOperationException(_l["local.fileNotFound", file.FullName].Value);
        if (IsLink(file))
            throw new InvalidOperationException(_l["local.symlink", file.FullName].Value);
        if (file.Length > _settings.MaxFileBytes)
            throw new InvalidOperationException(_l["fetch.fileTooLarge", FileName, _settings.MaxFileBytes].Value);

        try
        {
            await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16384, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var buffer = new LimitedMemoryStream(_settings.MaxFileBytes, _l["fetch.fileTooLarge", FileName, _settings.MaxFileBytes].Value);
            await stream.CopyToAsync(buffer, ct);
            return new MonitoringFetchResultDto { Content = Encoding.UTF8.GetString(buffer.ToArray()) };
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException(_l["local.noPermission", file.FullName].Value);
        }
    }

    private static bool IsLink(FileSystemInfo info) =>
        info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
}
