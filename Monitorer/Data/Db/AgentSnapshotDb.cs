namespace Monitorer.Data.Db;

/// <summary>Table "agent_snapshot": the latest fetched monitoring.json per agent plus a summary.</summary>
public class AgentSnapshotDb
{
    public int AgentId { get; set; }                    // agent_id (PK, FK agent)
    public DateTime LastAttemptAt { get; set; }         // last_attempt_at
    public DateTime? FetchedAt { get; set; }            // fetched_at (last successful fetch)
    public DateTime? GeneratedAt { get; set; }          // generated_at (timestamp written by the agent)
    public string Status { get; set; } = MonitorStatus.Unknown; // status
    public string? Message { get; set; }                // message
    public string? Payload { get; set; }                // payload (jsonb)
    public decimal? CpuPercent { get; set; }            // cpu_percent
    public decimal? MemoryPercent { get; set; }         // memory_percent
    public decimal? DiskPercentMax { get; set; }        // disk_percent_max
    public int? ContainersRunning { get; set; }         // containers_running
    public int? ContainersTotal { get; set; }           // containers_total
    public string? AgentVersion { get; set; }           // agent_version (agent.version from monitoring.json)
}
