namespace Monitorer.Data.Dto;

/// <summary>Data for the ECharts diagrams on the overview page (serialized into a JSON block).</summary>
public class OverviewChartsDto
{
    /// <summary>Number of agents per status (Ok, Warning, Critical, Unreachable, Unknown, Disabled).</summary>
    public Dictionary<string, int> AgentStatus { get; set; } = new();
    /// <summary>Number of live checks per status (Up, Down, Unknown, Disabled).</summary>
    public Dictionary<string, int> LiveCheckStatus { get; set; } = new();
    /// <summary>Current CPU / RAM / max. disk usage per VM.</summary>
    public List<ChartUsageDto> Usage { get; set; } = [];
    /// <summary>CPU history per VM: name → [[unix_ts, percent], …].</summary>
    public Dictionary<string, List<double[]>> CpuHistory { get; set; } = new();
    /// <summary>RAM history per VM: name → [[unix_ts, percent], …].</summary>
    public Dictionary<string, List<double[]>> MemoryHistory { get; set; } = new();
    /// <summary>Running / stopped / missing containers per VM.</summary>
    public List<ChartContainerCountDto> Containers { get; set; } = [];
    /// <summary>Last response time per live check.</summary>
    public List<ChartLiveCheckDto> LiveChecks { get; set; } = [];
    public double CpuWarnPercent { get; set; }
    public double CpuCriticalPercent { get; set; }
}
