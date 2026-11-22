namespace Monitorer.Data.Dto;

/// <summary>PATCH api/users/{id}/password - sets a new password for a local user.</summary>
public class UserPasswordDto
{
    public string Password { get; set; } = "";
}
