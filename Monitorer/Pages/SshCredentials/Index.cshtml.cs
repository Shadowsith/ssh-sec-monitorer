using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.SshCredentials;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly SshCredentialService _credentials;

    public IndexModel(SshCredentialService credentials) => _credentials = credentials;

    public List<SshCredentialDto> Credentials { get; private set; } = [];

    public async Task OnGetAsync() => Credentials = (await _credentials.GetAll()).Value ?? [];
}
