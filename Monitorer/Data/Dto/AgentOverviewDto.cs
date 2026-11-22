using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>One tile / row of the main overview.</summary>
public class AgentOverviewDto
{
    public AgentDto Agent { get; set; } = new();
    public AgentSnapshotDb? Snapshot { get; set; }

    public string Status => Agent.Enabled ? Snapshot?.Status ?? MonitorStatus.Unknown : "Disabled";
}
