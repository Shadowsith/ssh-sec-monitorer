namespace Monitorer.Data.Monitoring;

/// <summary>"agent" block of monitoring.json.</summary>
public class MonitoringAgentDto
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, int?>? Intervals { get; set; }
}
