using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.CertChecks;

public class IndexModel : PageModel
{
    private readonly CertCheckService _checks;
    private readonly CertCheckRunnerService _runner;
    private readonly MonitoringSettings _settings;
    private readonly IStringLocalizer _l;

    public IndexModel(CertCheckService checks, CertCheckRunnerService runner, IOptions<MonitoringSettings> settings,
        IStringLocalizer l)
    {
        _checks = checks;
        _runner = runner;
        _settings = settings.Value;
        _l = l;
    }

    public List<CertCheckDto> Checks { get; private set; } = [];

    public int WarnDays => _settings.CertWarnDays;

    public int CriticalDays => _settings.CertCriticalDays;

    public async Task OnGetAsync()
    {
        Checks = (await _checks.GetAll()).Value ?? [];
        ViewData["CertWarnDays"] = WarnDays;
        ViewData["CertCriticalDays"] = CriticalDays;
    }

    public async Task<IActionResult> OnPostRunAsync(int id, CancellationToken ct)
    {
        var result = await _runner.RunNow(id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? _l["certChecks.ran", result.Value!.Name, _l[MonitorStatus.TextKey(result.Value.LastStatus)].Value, result.Value.LastMessage ?? ""].Value
            : result.Message;
        return RedirectToPage();
    }
}
