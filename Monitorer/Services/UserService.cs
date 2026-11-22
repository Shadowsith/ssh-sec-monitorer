using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>User management: local users (password hash) and LDAP users (created on first login).</summary>
public class UserService
{
    private readonly UserRepository _repo;
    private readonly AuthSettings _auth;
    private readonly PasswordHasher<UserDb> _hasher = new();
    private readonly IStringLocalizer _l;

    public UserService(UserRepository repo, IOptions<AuthSettings> auth, IStringLocalizer l)
    {
        _l = l;
        _repo = repo;
        _auth = auth.Value;
    }

    public async Task<ServiceResult<List<UserDto>>> GetAll()
    {
        var users = await _repo.Select();
        return ServiceResult<List<UserDto>>.Ok(users.Select(u => new UserDto(u)).ToList());
    }

    public async Task<ServiceResult<UserDto>> Create(UserCreateDto dto)
    {
        var error = ValidateUserName(dto.UserName) ?? ValidatePassword(dto.Password)
                    ?? (UserRoles.IsValid(dto.Role) ? null : _l["users.invalidRole"].Value);
        if (error is not null)
            return ServiceResult<UserDto>.InvalidValue(error);

        if (await _repo.SelectByUserName(dto.UserName.Trim()) is not null)
            return ServiceResult<UserDto>.Conflict(_l["users.nameExists"].Value);

        var user = new UserDb
        {
            UserName = dto.UserName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.UserName.Trim() : dto.DisplayName.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            Role = dto.Role,
            AuthSource = AuthSources.Local,
            IsActive = true
        };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);
        user.Id = await _repo.Insert(user);
        user.CreatedAt = DateTime.UtcNow;
        return ServiceResult<UserDto>.Created(new UserDto(user));
    }

    public async Task<ServiceResult<UserDto>> UpdatePassword(int id, string password)
    {
        var user = await _repo.SelectById(id);
        if (user is null)
            return ServiceResult<UserDto>.NotFound(_l["users.notFound", id].Value);
        if (user.AuthSource != AuthSources.Local)
            return ServiceResult<UserDto>.NotAllowed(_l["users.ldapPassword"].Value);
        var error = ValidatePassword(password);
        if (error is not null)
            return ServiceResult<UserDto>.InvalidValue(error);

        user.PasswordHash = _hasher.HashPassword(user, password);
        await _repo.Update(user);
        return ServiceResult<UserDto>.Changed(new UserDto(user));
    }

    /// <summary>PATCH …/role: changes the role of a local user.</summary>
    public async Task<ServiceResult<UserDto>> UpdateRole(int id, string role, int currentUserId)
    {
        if (!UserRoles.IsValid(role))
            return ServiceResult<UserDto>.InvalidValue(_l["users.invalidRole"].Value);
        var user = await _repo.SelectById(id);
        if (user is null)
            return ServiceResult<UserDto>.NotFound(_l["users.notFound", id].Value);
        if (user.Role == role)
            return ServiceResult<UserDto>.Changed(new UserDto(user));
        if (user.AuthSource == AuthSources.Ldap)
            return ServiceResult<UserDto>.NotAllowed(_l["users.ldapRole"].Value);
        if (id == currentUserId)
            return ServiceResult<UserDto>.NotAllowed(_l["users.ownRole"].Value);
        if (user.Role == UserRoles.Admin && user.IsActive && await _repo.SelectActiveAdminCount() <= 1)
            return ServiceResult<UserDto>.NotAllowed(_l["users.lastAdminRole"].Value);

        user.Role = role;
        await _repo.Update(user);
        return ServiceResult<UserDto>.Changed(new UserDto(user));
    }

    public async Task<ServiceResult<UserDto>> UpdateActive(int id, bool isActive, int currentUserId)
    {
        var user = await _repo.SelectById(id);
        if (user is null)
            return ServiceResult<UserDto>.NotFound(_l["users.notFound", id].Value);
        if (!isActive && id == currentUserId)
            return ServiceResult<UserDto>.NotAllowed(_l["users.ownDisable"].Value);
        if (!isActive && user.Role == UserRoles.Admin && user.IsActive && await _repo.SelectActiveAdminCount() <= 1)
            return ServiceResult<UserDto>.NotAllowed(_l["users.lastAdminDisable"].Value);

        user.IsActive = isActive;
        await _repo.Update(user);
        return ServiceResult<UserDto>.Changed(new UserDto(user));
    }

    public async Task<ServiceResult<UserDto>> Delete(int id, int currentUserId)
    {
        var user = await _repo.SelectById(id);
        if (user is null)
            return ServiceResult<UserDto>.NotFound(_l["users.notFound", id].Value);
        if (id == currentUserId)
            return ServiceResult<UserDto>.NotAllowed(_l["users.ownDelete"].Value);
        if (user.Role == UserRoles.Admin && user.IsActive && await _repo.SelectActiveAdminCount() <= 1)
            return ServiceResult<UserDto>.NotAllowed(_l["users.lastAdminDelete"].Value);

        await _repo.Delete(id);
        return ServiceResult<UserDto>.Ok(new UserDto(user));
    }

    // ------------------------------------------------------------ internal (used by other services)

    public bool VerifyPassword(UserDb user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
            return false;
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public async Task<bool> IsActive(int id) => (await _repo.SelectById(id))?.IsActive == true;

    /// <summary>Creates or updates the local row of an LDAP user after a successful LDAP login.</summary>
    public async Task<UserDb> UpsertLdapUser(LdapUserDto ldapUser)
    {
        var role = ldapUser.IsAdmin ? UserRoles.Admin : UserRoles.User;
        var user = await _repo.SelectByUserName(ldapUser.UserName);
        if (user is null)
        {
            user = new UserDb
            {
                UserName = ldapUser.UserName,
                DisplayName = ldapUser.DisplayName,
                Email = ldapUser.Email,
                Role = role,
                AuthSource = AuthSources.Ldap,
                IsActive = true
            };
            user.Id = await _repo.Insert(user);
            return user;
        }

        user.DisplayName = ldapUser.DisplayName;
        user.Email = ldapUser.Email;
        user.Role = role;
        await _repo.Update(user);
        return user;
    }

    public string? ValidatePassword(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < _auth.MinPasswordLength
            ? _l["users.passwordTooShort", _auth.MinPasswordLength].Value
            : null;

    public string? ValidateUserName(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Trim().Length > 100)
            return _l["users.nameLength", 100].Value;
        return userName.Trim().All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '@')
            ? null
            : _l["users.nameChars"].Value;
    }
}
