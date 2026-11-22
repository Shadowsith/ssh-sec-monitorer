namespace Monitorer.Data.Dto;

/// <summary>Successful login: the JWT and some user info.</summary>
public class AuthDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}
