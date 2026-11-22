namespace Monitorer.Data.Monitoring;

/// <summary>RAM or swap usage in bytes.</summary>
public class MemoryDto
{
    public long? Total { get; set; }
    public long? Used { get; set; }
    public long? Available { get; set; }
    public double? Percent { get; set; }
}
