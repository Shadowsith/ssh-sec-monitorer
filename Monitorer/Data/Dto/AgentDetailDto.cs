using Monitorer.Data.Db;
using Monitorer.Data.Monitoring;

namespace Monitorer.Data.Dto;

/// <summary>Detail view of an agent: settings, snapshot summary and the parsed monitoring.json.</summary>
public class AgentDetailDto
{
    public AgentDto Agent { get; set; } = new();
    public AgentSnapshotDb? Snapshot { get; set; }
    public MonitoringFileDto? Monitoring { get; set; }
    public List<string> Findings { get; set; } = [];
    /// <summary>Containers seen earlier that are gone now (shown greyed out).</summary>
    public List<KnownContainerDb> MissingContainers { get; set; } = [];
}
