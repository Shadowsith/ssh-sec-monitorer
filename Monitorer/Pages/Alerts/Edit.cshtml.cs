using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Pages.Alerts;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel : PageModel
{
    private readonly AlertRuleService _rules;
    private readonly AgentService _agents;
    private readonly LiveCheckService _liveChecks;

    public EditModel(AlertRuleService rules, AgentService agents, LiveCheckService liveChecks)
    {
        _rules = rules;
        _agents = agents;
        _liveChecks = liveChecks;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public AlertRuleDto Input { get; set; } = new();

    public List<SelectListItem> Agents { get; private set; } = [];

    public List<SelectListItem> LiveChecks { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsNew => Id is null or 0;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadLists();
        if (IsNew)
        {
            Input = new AlertRuleDto { DelayMinutes = 5, RepeatMinutes = 60, ThresholdMinutes = 10, MinStatus = MonitorStatus.Critical };
            return Page();
        }
        var result = await _rules.Get(Id!.Value);
        if (!result.IsSuccess)
            return NotFound();
        Input = result.Value!;
        Input.ThresholdMinutes ??= 10;
        Input.MinStatus ??= MonitorStatus.Critical;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = IsNew ? await _rules.Create(Input) : await _rules.Update(Id!.Value, Input);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            await LoadLists();
            return Page();
        }
        TempData["Success"] = $"Alarm-Regel \"{result.Value!.Name}\" gespeichert.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var result = await _rules.Delete(Id ?? 0);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? $"Alarm-Regel \"{result.Value!.Name}\" gelöscht." : result.Message;
        return RedirectToPage("Index");
    }

    private async Task LoadLists()
    {
        Agents = ((await _agents.GetAll()).Value ?? [])
            .Select(a => new SelectListItem($"{a.Name} ({a.Target()})", a.Id.ToString()))
            .ToList();
        LiveChecks = ((await _liveChecks.GetAll()).Value ?? [])
            .Select(c => new SelectListItem($"{c.Name} ({c.CheckType})", c.Id.ToString()))
            .ToList();
    }
}
