using Monitorer.Data.Db;
using Monitorer.Data.Monitoring;

namespace Monitorer.Data.Dto;

/// <summary>Everything the alert rules look at, loaded once per evaluation run.</summary>
public class AlertContextDto
{
    public List<AgentDb> Agents { get; set; } = new();
    public Dictionary<int, AgentSnapshotDb> Snapshots { get; set; } = new();
    public Dictionary<int, MonitoringFileDto> Monitoring { get; set; } = new();
    public Dictionary<int, List<KnownContainerDb>> MissingContainers { get; set; } = new();
    public List<LiveCheckDb> LiveChecks { get; set; } = new();
    public List<CertCheckDb> CertChecks { get; set; } = new();
    /// <summary>Open (not acknowledged) CVE findings of enabled CVE checks.</summary>
    public List<CveFindingDto> CveFindings { get; set; } = new();
}
