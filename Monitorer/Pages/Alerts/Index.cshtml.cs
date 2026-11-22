using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.Alerts;

/// <summary>Alert rules (mail alarms) and the currently active alerts.</summary>
[Authorize(Roles = UserRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly AlertRuleService _rules;
    private readonly AlertEngineService _engine;
    private readonly IStringLocalizer _l;

    public IndexModel(AlertRuleService rules, AlertEngineService engine, IOptions<MailSettings> mail, IStringLocalizer l)
    {
        _l = l;
        _rules = rules;
        _engine = engine;
        Mail = mail.Value;
    }

    public MailSettings Mail { get; }

    public bool MailReady => Mail.Enabled && !string.IsNullOrWhiteSpace(Mail.Host);

    public List<AlertRuleDto> Rules { get; private set; } = [];

    public List<ActiveAlertDto> Active { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Rules = (await _rules.GetAll()).Value ?? [];
        Active = (await _rules.GetActive()).Value ?? [];
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, bool enabled)
    {
        var result = await _rules.UpdateEnabled(id, enabled);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? _l[enabled ? "alerts.ruleEnabledMessage" : "alerts.ruleDisabledMessage", result.Value!.Name].Value
            : result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestAsync(int id, CancellationToken ct)
    {
        var result = await _engine.SendTest(id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _l["alerts.testSent"].Value : result.Message;
        return RedirectToPage();
    }
}
