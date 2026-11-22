using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Pages.Users;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly UserService _users;

    public IndexModel(UserService users, IOptions<AuthSettings> auth, IOptions<LdapSettings> ldap)
    {
        _users = users;
        MinPasswordLength = auth.Value.MinPasswordLength;
        LdapEnabled = ldap.Value.Enabled;
    }

    public List<UserDto> Users { get; private set; } = [];

    [BindProperty]
    public UserCreateDto NewUser { get; set; } = new();

    public int MinPasswordLength { get; }

    public bool LdapEnabled { get; }

    public async Task OnGetAsync() => Users = (await _users.GetAll()).Value ?? [];

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var result = await _users.Create(NewUser);
        return Done(result, $"Benutzer \"{NewUser.UserName}\" angelegt.");
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id, bool isActive) =>
        Done(await _users.UpdateActive(id, isActive, User.GetUserId()), isActive ? "Benutzer aktiviert." : "Benutzer deaktiviert.");

    public async Task<IActionResult> OnPostRoleAsync(int id, string role) =>
        Done(await _users.UpdateRole(id, role, User.GetUserId()), $"Rolle auf {role} geändert.");

    public async Task<IActionResult> OnPostPasswordAsync(int id, string password) =>
        Done(await _users.UpdatePassword(id, password), "Passwort geändert.");

    public async Task<IActionResult> OnPostDeleteAsync(int id) =>
        Done(await _users.Delete(id, User.GetUserId()), "Benutzer gelöscht.");

    private IActionResult Done(ServiceResult<UserDto> result, string successMessage)
    {
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? successMessage : result.Message;
        return RedirectToPage();
    }
}
