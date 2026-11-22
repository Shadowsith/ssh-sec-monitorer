namespace Monitorer.Data.Db;

/// <summary>Table "app_user".</summary>
public class UserDb
{
    public int Id { get; set; }                         // id
    public string UserName { get; set; } = "";          // user_name
    public string DisplayName { get; set; } = "";       // display_name
    public string? Email { get; set; }                  // email
    public string? PasswordHash { get; set; }           // password_hash (null for LDAP users)
    public string Role { get; set; } = UserRoles.User;  // role
    public string AuthSource { get; set; } = AuthSources.Local; // auth_source
    public bool IsActive { get; set; } = true;          // is_active
    public DateTime CreatedAt { get; set; }             // created_at
    public DateTime? LastLoginAt { get; set; }          // last_login_at
}
