using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>Agent row plus the name of its SSH credential.</summary>
public class AgentDto : AgentDb
{
    public string SshCredentialName { get; set; } = "";

    public AgentDto() { }

    public AgentDto(AgentDb row)
    {
        Id = row.Id;
        Name = row.Name;
        Description = row.Description;
        ConnectionType = row.ConnectionType;
        LocalDir = row.LocalDir;
        Host = row.Host;
        Port = row.Port;
        SshCredentialId = row.SshCredentialId;
        RemotePath = row.RemotePath;
        PollIntervalSeconds = row.PollIntervalSeconds;
        Enabled = row.Enabled;
        HostKeyFingerprint = row.HostKeyFingerprint;
        CreatedAt = row.CreatedAt;
    }
}
