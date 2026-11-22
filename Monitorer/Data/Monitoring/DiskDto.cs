namespace Monitorer.Data.Monitoring;

/// <summary>Usage of one mounted filesystem in bytes.</summary>
public class DiskDto
{
    public string Mount { get; set; } = "";
    public long? Total { get; set; }
    public long? Used { get; set; }
    public long? Free { get; set; }
    public double? Percent { get; set; }
}
