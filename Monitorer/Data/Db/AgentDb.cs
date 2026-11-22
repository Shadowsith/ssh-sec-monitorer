namespace Monitorer.Data.Db;

/// <summary>Table "agent": a VM whose monitoring.json is fetched via SFTP, or a local agent read from a mounted directory.</summary>
public class AgentDb
{
    public int Id { get; set; }                         // id
    public string Name { get; set; } = "";              // name
    public string Description { get; set; } = "";       // description
    public string ConnectionType { get; set; } = AgentConnectionTypes.Ssh; // connection_type (Ssh | Local)
    public string? Host { get; set; }                   // host (Ssh)
    public int? Port { get; set; } = 22;                // port (Ssh)
    public int? SshCredentialId { get; set; }           // ssh_credential_id (Ssh)
    public string? RemotePath { get; set; } = "/opt/sshsecmonitorer/agent/monitoring.json"; // remote_path (Ssh)
    public string? LocalDir { get; set; }               // local_dir (Local): sub directory below Monitoring:LocalAgentRoot
    public int PollIntervalSeconds { get; set; } = 60;  // poll_interval_seconds
    public bool Enabled { get; set; } = true;           // enabled
    public string? HostKeyFingerprint { get; set; }     // host_key_fingerprint (Ssh, SHA256, trust on first use)
    public DateTime CreatedAt { get; set; }             // created_at
}
