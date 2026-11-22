namespace Monitorer.Data.Db;

/// <summary>Table "ssh_credential". Secrets are encrypted with ASP.NET Data Protection.</summary>
public class SshCredentialDb
{
    public int Id { get; set; }                         // id
    public string Name { get; set; } = "";              // name
    public string UserName { get; set; } = "";          // user_name
    public string AuthType { get; set; } = SshAuthTypes.Key; // auth_type
    public string? PrivateKeyEnc { get; set; }          // private_key_enc
    public string? PassphraseEnc { get; set; }          // passphrase_enc
    public string? PasswordEnc { get; set; }            // password_enc
    public DateTime CreatedAt { get; set; }             // created_at
    public DateTime UpdatedAt { get; set; }             // updated_at
}
