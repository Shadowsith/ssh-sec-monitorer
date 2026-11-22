namespace Monitorer.Data.Dto;

/// <summary>Login request (page form and POST api/auth/login).</summary>
public class LoginDto
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}
