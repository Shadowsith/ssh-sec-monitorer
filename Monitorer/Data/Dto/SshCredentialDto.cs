using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>
/// SSH credential for the UI / API. Secrets are write-only: they are accepted on create / update
/// and never sent back (empty secret on update = keep the stored one).
/// </summary>
public class SshCredentialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string UserName { get; set; } = "";
    public string AuthType { get; set; } = SshAuthTypes.Key;
    public string? PrivateKey { get; set; }
    public string? Passphrase { get; set; }
    public string? Password { get; set; }
    public bool HasPrivateKey { get; set; }
    public bool HasPassword { get; set; }
    public int UsedByAgents { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SshCredentialDto() { }

    public SshCredentialDto(SshCredentialDb row)
    {
        Id = row.Id;
        Name = row.Name;
        UserName = row.UserName;
        AuthType = row.AuthType;
        HasPrivateKey = row.PrivateKeyEnc is not null;
        HasPassword = row.PasswordEnc is not null;
        CreatedAt = row.CreatedAt;
        UpdatedAt = row.UpdatedAt;
    }
}
