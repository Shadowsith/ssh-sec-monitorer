namespace Monitorer.Data.Monitoring;

/// <summary>Log summary of a container over the configured window.</summary>
public class ContainerLogsDto
{
    public int WindowSeconds { get; set; }
    public int ScannedLines { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<string> Tail { get; set; } = [];
    public long? CollectedAt { get; set; }
}
