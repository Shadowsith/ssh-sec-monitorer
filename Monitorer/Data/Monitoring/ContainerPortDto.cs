namespace Monitorer.Data.Monitoring;

/// <summary>A host port published by a container (docker run -p / compose "ports:").</summary>
public class ContainerPortDto
{
    public string HostIp { get; set; } = "0.0.0.0";
    public int HostPort { get; set; }
    public int ContainerPort { get; set; }
    public string Protocol { get; set; } = "tcp";
    public bool LocalhostOnly { get; set; }

    /// <summary>"8080 → 80/tcp", with the bind address when it is not all interfaces.</summary>
    public string DisplayText =>
        (HostIp is "0.0.0.0" ? "" : HostIp + ":") + $"{HostPort} → {ContainerPort}/{Protocol}";
}
