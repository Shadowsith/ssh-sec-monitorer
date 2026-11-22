using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.Agents;

/// <summary>All agents (VMs) as cards with status and usage.</summary>
public class IndexModel : PageModel
{
    private readonly OverviewService _overview;

    public IndexModel(OverviewService overview, IOptions<MonitoringSettings> settings)
    {
        _overview = overview;
        Settings = settings.Value;
    }

    public List<AgentOverviewDto> Agents { get; private set; } = [];

    public MonitoringSettings Settings { get; }

    public async Task OnGetAsync() => Agents = (await _overview.Get()).Value?.Agents ?? [];
}
