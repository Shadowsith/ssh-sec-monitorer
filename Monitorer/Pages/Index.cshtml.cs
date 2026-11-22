using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages;

/// <summary>Main overview: status tiles and diagrams (Apache ECharts). Agents and live checks have their own pages.</summary>
public class IndexModel : PageModel
{
    private readonly OverviewService _overview;
    private readonly OverviewChartService _charts;
    private readonly AlertRuleService _alerts;

    public IndexModel(OverviewService overview, OverviewChartService charts, AlertRuleService alerts)
    {
        _overview = overview;
        _charts = charts;
        _alerts = alerts;
    }

    public OverviewDto Overview { get; private set; } = new();

    public OverviewChartsDto Charts { get; private set; } = new();

    public int ActiveAlerts { get; private set; }

    /// <summary>Chart data for overview.js; System.Text.Json escapes &lt; &gt; &amp; so it is safe inside a script block.</summary>
    public string ChartsJson => JsonSerializer.Serialize(Charts, JsonSerializerOptions.Web);

    public int CountByStatus(params string[] statuses) =>
        Overview.Agents.Count(a => statuses.Contains(a.Status))
        + Overview.LiveChecks.Count(c => c.Enabled && statuses.Contains(c.LastStatus));

    public async Task OnGetAsync()
    {
        Overview = (await _overview.Get()).Value ?? new OverviewDto();
        Charts = (await _charts.Get()).Value ?? new OverviewChartsDto();
        ActiveAlerts = (await _alerts.GetActive()).Value?.Count(a => !a.IsPending) ?? 0;
    }
}
