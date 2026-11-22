using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>A user as shown in the UI / API - never contains the password hash.</summary>
public class UserDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";
    public string AuthSource { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public UserDto() { }

    public UserDto(UserDb row)
    {
        Id = row.Id;
        UserName = row.UserName;
        DisplayName = row.DisplayName;
        Email = row.Email;
        Role = row.Role;
        AuthSource = row.AuthSource;
        IsActive = row.IsActive;
        CreatedAt = row.CreatedAt;
        LastLoginAt = row.LastLoginAt;
    }
}
