namespace Monitorer.Data.Monitoring;

/// <summary>"host" block: current values plus history series of [unix_ts, value] points.</summary>
public class HostSectionDto
{
    public HostCurrentDto? Current { get; set; }
    public Dictionary<string, List<double[]>>? History { get; set; }
}
