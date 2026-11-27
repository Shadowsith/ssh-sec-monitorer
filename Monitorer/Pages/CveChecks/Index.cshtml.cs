using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Pages.CveChecks;

public class IndexModel : PageModel
{
    private readonly CveCheckService _checks;
    private readonly CveCheckRunnerService _runner;
    private readonly CveFindingService _findings;
    private readonly IStringLocalizer _l;

    public IndexModel(CveCheckService checks, CveCheckRunnerService runner, CveFindingService findings, IStringLocalizer l)
    {
        _checks = checks;
        _runner = runner;
        _findings = findings;
        _l = l;
    }

    /// <summary>?baseline=true also lists the CVEs stored silently on first runs.</summary>
    [BindProperty(SupportsGet = true, Name = "baseline")]
    public bool ShowBaseline { get; set; }

    public List<CveCheckDto> Checks { get; private set; } = [];

    public List<CveFindingDto> Findings { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Checks = (await _checks.GetAll()).Value ?? [];
        Findings = (await _findings.GetForPage(ShowBaseline)).Value ?? [];
    }

    public async Task<IActionResult> OnPostRunAsync(int id, CancellationToken ct)
    {
        var result = await _runner.RunNow(id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? _l["cveChecks.ran", result.Value!.Name, _l[MonitorStatus.TextKey(result.Value.LastStatus)].Value, result.Value.LastMessage ?? ""].Value
            : result.Message;
        return RedirectToPage(new { baseline = ShowBaseline ? true : (bool?)null });
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(string cveId)
    {
        var result = await _findings.Acknowledge(cveId, User.GetDisplayName());
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? _l["cveChecks.acknowledged", cveId].Value : result.Message;
        return RedirectToPage(new { baseline = ShowBaseline ? true : (bool?)null });
    }
}
