namespace Monitorer.Data.Dto;

/// <summary>A user found and authenticated in LDAP / AD.</summary>
public class LdapUserDto
{
    public string Dn { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
}
