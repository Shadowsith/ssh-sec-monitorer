using Monitorer.Data.Db;
using Monitorer.Data.Monitoring;

namespace Monitorer.Data.Dto;

/// <summary>One VM in the Docker view: agent, snapshot status and its Docker section.</summary>
public class DockerHostDto
{
    public AgentDto Agent { get; set; } = new();
    public AgentSnapshotDb? Snapshot { get; set; }
    public DockerSectionDto? Docker { get; set; }
    /// <summary>Containers seen earlier that are gone now (shown greyed out).</summary>
    public List<KnownContainerDb> MissingContainers { get; set; } = [];

    public string Status => Agent.Enabled ? Snapshot?.Status ?? MonitorStatus.Unknown : "Disabled";

    public int RunningCount => Docker?.Containers.Count(c => c.IsRunning) ?? 0;
    public int TotalCount => Docker?.Containers.Count ?? 0;
    public int ErrorCount => Docker?.Containers.Sum(c => c.Logs?.ErrorCount ?? 0) ?? 0;
    public int WarningCount => Docker?.Containers.Sum(c => c.Logs?.WarningCount ?? 0) ?? 0;

    /// <summary>VMs with problems start expanded.</summary>
    public bool HasProblems => Status is not (MonitorStatus.Ok or "Disabled")
                               || Docker?.Containers.Any(c => c.StateCssClass is "danger" or "warning") == true
                               || MissingContainers.Count > 0;
}
