using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Pages.Docker;

/// <summary>Docker view: one accordion item per VM, its containers as cards.</summary>
public class IndexModel : PageModel
{
    private readonly DockerOverviewService _docker;
    private readonly KnownContainerService _known;
    private readonly IStringLocalizer _l;

    public IndexModel(DockerOverviewService docker, KnownContainerService known, IStringLocalizer l)
    {
        _l = l;
        _docker = docker;
        _known = known;
    }

    public List<DockerHostDto> Hosts { get; private set; } = [];

    public int RunningTotal => Hosts.Sum(h => h.RunningCount);

    public int ContainerTotal => Hosts.Sum(h => h.TotalCount);

    public int MissingTotal => Hosts.Sum(h => h.MissingContainers.Count);

    public async Task OnGetAsync() => Hosts = (await _docker.Get()).Value ?? [];

    /// <summary>Admin: forget one container that no longer exists.</summary>
    public async Task<IActionResult> OnPostForgetAsync(int knownId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var result = await _known.Delete(knownId);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? _l["containers.forgotten", result.Value!.Name].Value : result.Message;
        return RedirectToPage();
    }

    /// <summary>Admin: forget all missing containers of one VM.</summary>
    public async Task<IActionResult> OnPostForgetMissingAsync(int agentId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var result = await _known.DeleteMissing(agentId);
        TempData["Success"] = _l["docker.forgotAll", result.Value?.Count ?? 0].Value;
        return RedirectToPage();
    }
}
