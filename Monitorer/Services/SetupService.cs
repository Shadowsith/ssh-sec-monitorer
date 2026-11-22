using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>
/// Initial setup: without LDAP an admin user must exist before the application can be used.
/// The admin is either seeded from Setup:AdminUserName / Setup:AdminPassword or created on the setup page.
/// </summary>
public class SetupService
{
    private readonly UserRepository _users;
    private readonly UserService _userService;
    private readonly SetupState _state;
    private readonly SetupSettings _setup;
    private readonly LdapSettings _ldap;
    private readonly ILogger<SetupService> _logger;

    public SetupService(UserRepository users, UserService userService, SetupState state,
        IOptions<SetupSettings> setup, IOptions<LdapSettings> ldap, ILogger<SetupService> logger)
    {
        _users = users;
        _userService = userService;
        _state = state;
        _setup = setup.Value;
        _ldap = ldap.Value;
        _logger = logger;
    }

    public async Task<bool> IsSetupRequired()
    {
        if (_state.Completed)
            return false;
        if (_ldap.Enabled || await _users.SelectCount() > 0)
        {
            _state.MarkCompleted();
            return false;
        }
        return true;
    }

    public async Task<ServiceResult<UserDto>> CreateInitialAdmin(SetupAdminDto dto)
    {
        if (!await IsSetupRequired())
            return ServiceResult<UserDto>.NotAllowed("Die Ersteinrichtung ist bereits abgeschlossen.");
        if (dto.Password != dto.PasswordRepeat)
            return ServiceResult<UserDto>.InvalidValue("Die Passwörter stimmen nicht überein.");

        var result = await _userService.Create(new UserCreateDto
        {
            UserName = dto.UserName,
            DisplayName = dto.DisplayName,
            Password = dto.Password,
            Role = UserRoles.Admin
        });
        if (result.IsSuccess)
        {
            _state.MarkCompleted();
            _logger.LogInformation("Initial admin {UserName} created via setup page", dto.UserName);
        }
        return result;
    }

    /// <summary>Called on startup: creates the admin from the configuration if no user exists yet.</summary>
    public async Task SeedAdminFromSettings()
    {
        if (string.IsNullOrWhiteSpace(_setup.AdminUserName) || string.IsNullOrEmpty(_setup.AdminPassword))
            return;
        if (await _users.SelectCount() > 0)
            return;

        var result = await _userService.Create(new UserCreateDto
        {
            UserName = _setup.AdminUserName,
            DisplayName = _setup.AdminUserName,
            Password = _setup.AdminPassword,
            Role = UserRoles.Admin
        });
        if (result.IsSuccess)
        {
            _state.MarkCompleted();
            _logger.LogInformation("Initial admin {UserName} created from configuration", _setup.AdminUserName);
        }
        else
        {
            _logger.LogWarning("Initial admin from configuration not created: {Message}", result.Message);
        }
    }
}
