using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.LiveChecks;

public class IndexModel : PageModel
{
    private readonly LiveCheckService _checks;
    private readonly LiveCheckRunnerService _runner;
    private readonly IStringLocalizer _l;

    public IndexModel(LiveCheckService checks, LiveCheckRunnerService runner, IStringLocalizer l)
    {
        _l = l;
        _checks = checks;
        _runner = runner;
    }

    public List<LiveCheckDto> Checks { get; private set; } = [];

    public async Task OnGetAsync() => Checks = (await _checks.GetAll()).Value ?? [];

    public async Task<IActionResult> OnPostRunAsync(int id, CancellationToken ct)
    {
        var result = await _runner.RunNow(id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? _l["liveChecks.ran", result.Value!.Name, _l[MonitorStatus.TextKey(result.Value.LastStatus)].Value, result.Value.LastMessage ?? ""].Value
            : result.Message;
        return RedirectToPage();
    }
}
