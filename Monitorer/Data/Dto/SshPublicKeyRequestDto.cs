namespace Monitorer.Data.Dto;

/// <summary>POST api/ssh-credentials/public-key: entered private key, or (empty) the stored one of CredentialId.</summary>
public class SshPublicKeyRequestDto
{
    public int? CredentialId { get; set; }
    public string? PrivateKey { get; set; }
    public string? Passphrase { get; set; }
}
