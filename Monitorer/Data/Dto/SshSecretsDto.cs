namespace Monitorer.Data.Dto;

/// <summary>Decrypted SSH secrets - only used internally for a connection, never sent to a client.</summary>
public class SshSecretsDto
{
    public string UserName { get; set; } = "";
    public string AuthType { get; set; } = "";
    public string? PrivateKey { get; set; }
    public string? Passphrase { get; set; }
    public string? Password { get; set; }
}
