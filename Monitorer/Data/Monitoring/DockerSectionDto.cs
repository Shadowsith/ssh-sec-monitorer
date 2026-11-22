namespace Monitorer.Data.Monitoring;

/// <summary>"docker" block of monitoring.json.</summary>
public class DockerSectionDto
{
    public bool Available { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string?>? Engine { get; set; }
    public List<ContainerDto> Containers { get; set; } = [];
    /// <summary>Keys: "&lt;container&gt;.cpu_percent", "&lt;container&gt;.memory_percent".</summary>
    public Dictionary<string, List<double[]>>? History { get; set; }
}
