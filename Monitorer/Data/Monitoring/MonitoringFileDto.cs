namespace Monitorer.Data.Monitoring;

/// <summary>Root of monitoring.json as written by the agent (snake_case JSON, schema_version 1).</summary>
public class MonitoringFileDto
{
    public int SchemaVersion { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public MonitoringAgentDto? Agent { get; set; }
    public HostSectionDto? Host { get; set; }
    public DockerSectionDto? Docker { get; set; }
}
