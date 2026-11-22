using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Pages.SshCredentials;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel : PageModel
{
    private readonly SshCredentialService _credentials;
    private readonly AgentService _agents;
    private readonly VmSetupScriptService _script;
    private readonly IStringLocalizer _l;

    public EditModel(SshCredentialService credentials, AgentService agents, VmSetupScriptService script, IStringLocalizer l)
    {
        _l = l;
        _credentials = credentials;
        _agents = agents;
        _script = script;
    }

    /// <summary>Setup scripts for the read-only SFTP user per system (card "Create user on VM").</summary>
    public List<(VmSetupScriptService.Variant Variant, string Script)> SetupScripts { get; private set; } = [];

    /// <summary>Agents for the test target suggestions (those using this credential first).</summary>
    public List<AgentDto> TestTargets { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public SshCredentialDto Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsNew => Id is null or 0;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadTestTargets();
        if (IsNew)
            return Page();
        var result = await _credentials.Get(Id!.Value);
        if (!result.IsSuccess)
            return NotFound();
        Input = result.Value!;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = IsNew ? await _credentials.Create(Input) : await _credentials.Update(Id!.Value, Input);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            await RestoreFlags();
            await LoadTestTargets();
            return Page();
        }
        TempData["Success"] = _l["credentials.saved", result.Value!.Name].Value;
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var result = await _credentials.Delete(Id ?? 0);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? _l["credentials.deleted", result.Value!.Name].Value : result.Message;
        return result.IsSuccess ? RedirectToPage("Index") : RedirectToPage("Edit", new { id = Id });
    }

    private async Task LoadTestTargets()
    {
        TestTargets = ((await _agents.GetAll()).Value ?? [])
            .Where(a => !a.IsLocal())
            .OrderBy(a => a.SshCredentialId == Id ? 0 : 1)
            .ThenBy(a => a.Name)
            .ToList();
        SetupScripts = await _script.GetAll();
    }

    /// <summary>Never send posted secrets back to the browser; keep the "stored" hints for the form.</summary>
    private async Task RestoreFlags()
    {
        Input.PrivateKey = Input.Passphrase = Input.Password = null;
        if (!IsNew && (await _credentials.Get(Id!.Value)).Value is { } stored)
        {
            Input.HasPrivateKey = stored.HasPrivateKey;
            Input.HasPassword = stored.HasPassword;
        }
    }
}
