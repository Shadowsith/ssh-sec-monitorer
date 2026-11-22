namespace Monitorer.Data.Dto;

/// <summary>Creates a local user.</summary>
public class UserCreateDto
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string Password { get; set; } = "";
    public string Role { get; set; } = UserRoles.User;
}
