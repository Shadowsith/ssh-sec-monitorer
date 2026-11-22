using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Pages.Agents;

/// <summary>Single view of one agent: host metrics, history charts, containers and logs.</summary>
public class DetailModel : PageModel
{
    private readonly AgentService _agents;
    private readonly AgentPollService _poll;
    private readonly KnownContainerService _known;
    private readonly IStringLocalizer _l;

    public DetailModel(AgentService agents, AgentPollService poll, KnownContainerService known,
        IOptions<MonitoringSettings> settings, IStringLocalizer l)
    {
        _l = l;
        _known = known;
        _agents = agents;
        _poll = poll;
        Settings = settings.Value;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public AgentDetailDto Detail { get; private set; } = new();

    public MonitoringSettings Settings { get; }

    /// <summary>Next scheduled fetch (spread over the interval by the PollScheduler).</summary>
    public DateTime? NextPollAt { get; private set; }

    /// <summary>History series for the charts (agent-detail.js), safely encoded for a JSON script block.</summary>
    public string HistoryJson => JsonSerializer.Serialize(new
    {
        host = Detail.Monitoring?.Host?.History,
        docker = Detail.Monitoring?.Docker?.History
    });

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await _agents.GetDetail(Id);
        if (!result.IsSuccess)
            return NotFound();
        Detail = result.Value!;
        NextPollAt = _poll.NextPollAt(Detail.Agent, Detail.Snapshot?.LastAttemptAt);
        return Page();
    }

    /// <summary>Admin: forget a container that no longer exists.</summary>
    public async Task<IActionResult> OnPostForgetAsync(int knownId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var result = await _known.Delete(knownId);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? _l["containers.forgotten", result.Value!.Name].Value : result.Message;
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostPollAsync(CancellationToken ct)
    {
        var result = await _poll.PollNow(Id, ct);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }
        TempData["Success"] = _l["agents.polled", _l[MonitorStatus.TextKey(result.Value!.Status)].Value].Value;
        return RedirectToPage(new { id = Id });
    }
}
