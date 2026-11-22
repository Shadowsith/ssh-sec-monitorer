namespace Monitorer.Data.Monitoring;

/// <summary>Current host metrics.</summary>
public class HostCurrentDto
{
    public string? Hostname { get; set; }
    public double? UptimeSeconds { get; set; }
    public double? CpuPercent { get; set; }
    public int? CpuCount { get; set; }
    public List<double>? Load { get; set; }
    public MemoryDto? Memory { get; set; }
    public MemoryDto? Swap { get; set; }
    public List<DiskDto>? Disks { get; set; }
    public List<NetworkInterfaceDto>? Network { get; set; }
    public long? CollectedAt { get; set; }
}
