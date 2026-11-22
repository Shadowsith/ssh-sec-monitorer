namespace Monitorer.Data.Dto;

/// <summary>Current usage of one VM for the overview bar chart.</summary>
public class ChartUsageDto
{
    public int AgentId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal? Cpu { get; set; }
    public decimal? Memory { get; set; }
    public decimal? Disk { get; set; }
}
