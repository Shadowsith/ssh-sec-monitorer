namespace Monitorer.Data.Dto;

/// <summary>Initial admin user created on the setup page.</summary>
public class SetupAdminDto
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Password { get; set; } = "";
    public string PasswordRepeat { get; set; } = "";
}
