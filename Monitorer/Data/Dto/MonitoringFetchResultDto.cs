namespace Monitorer.Data.Dto;

/// <summary>Result of fetching monitoring.json (via SFTP or from the local agent directory).</summary>
public class MonitoringFetchResultDto
{
    public string Content { get; set; } = "";
    /// <summary>SSH host key (SFTP only).</summary>
    public string? HostKeyFingerprint { get; set; }
}
