namespace Monitorer.Data.Dto;

/// <summary>
/// POST api/ssh-credentials/test: test a (possibly unsaved) SSH credential against a host.
/// With CredentialId, empty secrets fall back to the stored ones.
/// </summary>
public class SshConnectionTestDto
{
    public int? CredentialId { get; set; }
    public string UserName { get; set; } = "";
    public string AuthType { get; set; } = SshAuthTypes.Key;
    public string? PrivateKey { get; set; }
    public string? Passphrase { get; set; }
    public string? Password { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    /// <summary>Optional: also read and check this monitoring.json.</summary>
    public string? RemotePath { get; set; }
    /// <summary>Optional: expected SHA256 host key (pinned fingerprint of an agent).</summary>
    public string? HostKeyFingerprint { get; set; }
}
