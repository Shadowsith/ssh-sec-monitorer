namespace Monitorer.Data.Dto;

/// <summary>Everything the main overview page shows.</summary>
public class OverviewDto
{
    public List<AgentOverviewDto> Agents { get; set; } = [];
    public List<LiveCheckDto> LiveChecks { get; set; } = [];
}
