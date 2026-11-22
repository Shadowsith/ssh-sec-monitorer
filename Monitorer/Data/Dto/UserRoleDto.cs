namespace Monitorer.Data.Dto;

/// <summary>PATCH api/users/{id}/role - sets the role of a local user.</summary>
public class UserRoleDto
{
    public string Role { get; set; } = UserRoles.User;
}
