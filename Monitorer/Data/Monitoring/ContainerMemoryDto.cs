namespace Monitorer.Data.Monitoring;

/// <summary>Memory usage of a container in bytes (page cache excluded by the agent).</summary>
public class ContainerMemoryDto
{
    public long? Usage { get; set; }
    public long? Limit { get; set; }
    public double? Percent { get; set; }
}
