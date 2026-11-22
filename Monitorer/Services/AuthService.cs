using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>Login with a local account or, if enabled, via LDAP / AD. Creates the JWT.</summary>
public class AuthService
{
    private const string InvalidCredentials = "Benutzername oder Passwort ist falsch.";

    private readonly UserRepository _users;
    private readonly UserService _userService;
    private readonly LdapService _ldap;
    private readonly TokenService _tokens;
    private readonly LoginThrottleService _throttle;
    private readonly ILogger<AuthService> _logger;

    public AuthService(UserRepository users, UserService userService, LdapService ldap, TokenService tokens,
        LoginThrottleService throttle, ILogger<AuthService> logger)
    {
        _users = users;
        _userService = userService;
        _ldap = ldap;
        _tokens = tokens;
        _throttle = throttle;
        _logger = logger;
    }

    public async Task<ServiceResult<AuthDto>> Login(LoginDto dto, string? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrEmpty(dto.Password))
            return ServiceResult<AuthDto>.InvalidValue("Benutzername und Passwort sind erforderlich.");

        var userName = dto.UserName.Trim();
        if (_throttle.IsLocked(userName, remoteIp))
            return ServiceResult<AuthDto>.Locked("Zu viele Fehlversuche. Bitte später erneut versuchen.");

        var user = await _users.SelectByUserName(userName);
        if (user is not null && user.AuthSource == AuthSources.Local)
        {
            if (!_userService.VerifyPassword(user, dto.Password))
                user = null;
        }
        else if (_ldap.Enabled)
        {
            var ldapUser = await _ldap.Authenticate(userName, dto.Password);
            user = ldapUser is null ? null : await _userService.UpsertLdapUser(ldapUser);
        }
        else
        {
            user = null;
        }

        if (user is null)
        {
            _throttle.RegisterFailure(userName, remoteIp);
            _logger.LogWarning("Failed login for {UserName} from {RemoteIp}", userName, remoteIp);
            return ServiceResult<AuthDto>.Unauthorized(InvalidCredentials);
        }
        if (!user.IsActive)
            return ServiceResult<AuthDto>.Forbidden("Der Benutzer ist deaktiviert.");

        _throttle.RegisterSuccess(userName, remoteIp);
        await _users.UpdateLastLoginAt(user.Id, DateTime.UtcNow);
        _logger.LogInformation("User {UserName} logged in ({AuthSource})", user.UserName, user.AuthSource);
        return ServiceResult<AuthDto>.Ok(_tokens.CreateToken(user));
    }
}
