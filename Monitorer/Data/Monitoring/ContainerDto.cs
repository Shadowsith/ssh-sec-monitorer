namespace Monitorer.Data.Monitoring;

/// <summary>One container with state, stats and log summary.</summary>
public class ContainerDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Image { get; set; }
    public string? State { get; set; }
    public string? Health { get; set; }
    public int? ExitCode { get; set; }
    public int? RestartCount { get; set; }
    public string? StartedAt { get; set; }
    public double? CpuPercent { get; set; }
    public ContainerMemoryDto? Memory { get; set; }
    public ContainerNetworkDto? Network { get; set; }
    public ContainerLogsDto? Logs { get; set; }
    /// <summary>Published host ports; null if the agent does not report ports (docker.ports.enabled: false).</summary>
    public List<ContainerPortDto>? Ports { get; set; }
    /// <summary>Disk I/O; null if the agent does not report it (docker.block_io.enabled: false).</summary>
    public ContainerBlockIoDto? BlockIo { get; set; }

    public bool IsRunning => State == "running";
    public bool IsUnhealthy => Health == "unhealthy";

    /// <summary>Bootstrap color for state badges / card borders.</summary>
    public string StateCssClass =>
        IsUnhealthy || State is "restarting" or "dead" ? "danger"
        : IsRunning ? (Health is null or "healthy" ? "success" : "warning")
        : "secondary";

    public string StateText => State + (Health is null ? "" : " · " + Health);

    public DateTime? StartedAtUtc =>
        DateTime.TryParse(StartedAt, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
        && t.Year > 1 ? t : null;
}
