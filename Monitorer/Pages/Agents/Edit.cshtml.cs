using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.Agents;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel : PageModel
{
    private readonly AgentService _agents;
    private readonly SshCredentialService _credentials;
    private readonly MonitoringSettings _settings;

    public EditModel(AgentService agents, SshCredentialService credentials, IOptions<MonitoringSettings> settings)
    {
        _agents = agents;
        _credentials = credentials;
        _settings = settings.Value;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public AgentDto Input { get; set; } = new();

    public List<SelectListItem> Credentials { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsNew => Id is null or 0;

    public bool IsSsh => Input.ConnectionType != AgentConnectionTypes.Local;

    public string LocalAgentRoot => _settings.LocalAgentRoot.TrimEnd('/');

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadCredentials();
        if (IsNew)
        {
            Input = new AgentDto { PollIntervalSeconds = _settings.DefaultPollIntervalSeconds };
            return Page();
        }
        var result = await _agents.Get(Id!.Value);
        if (!result.IsSuccess)
            return NotFound();
        Input = result.Value!;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = IsNew ? await _agents.Create(Input) : await _agents.Update(Id!.Value, Input);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            await LoadCredentials();
            return Page();
        }
        TempData["Success"] = $"Agent \"{result.Value!.Name}\" gespeichert.";
        return RedirectToPage("Detail", new { id = result.Value.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var result = await _agents.Delete(Id ?? 0);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? $"Agent \"{result.Value!.Name}\" gelöscht." : result.Message;
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetHostKeyAsync()
    {
        var result = await _agents.ResetHostKey(Id ?? 0);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? "Host-Key zurückgesetzt – er wird beim nächsten Abruf neu übernommen." : result.Message;
        return RedirectToPage("Edit", new { id = Id });
    }

    private async Task LoadCredentials() =>
        Credentials = ((await _credentials.GetAll()).Value ?? [])
            .Select(c => new SelectListItem($"{c.Name} ({c.UserName})", c.Id.ToString()))
            .ToList();
}
