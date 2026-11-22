using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.LiveChecks;

public class IndexModel : PageModel
{
    private readonly LiveCheckService _checks;
    private readonly LiveCheckRunnerService _runner;

    public IndexModel(LiveCheckService checks, LiveCheckRunnerService runner)
    {
        _checks = checks;
        _runner = runner;
    }

    public List<LiveCheckDto> Checks { get; private set; } = [];

    public async Task OnGetAsync() => Checks = (await _checks.GetAll()).Value ?? [];

    public async Task<IActionResult> OnPostRunAsync(int id, CancellationToken ct)
    {
        var result = await _runner.RunNow(id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"\"{result.Value!.Name}\": {result.Value.LastStatus} – {result.Value.LastMessage}"
            : result.Message;
        return RedirectToPage();
    }
}
