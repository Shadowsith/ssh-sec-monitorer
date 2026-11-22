namespace Monitorer.Data.Monitoring;

/// <summary>Disk I/O of a container: current rates (bytes/s) and, if enabled, totals since start.</summary>
public class ContainerBlockIoDto
{
    /// <summary>false if the host's cgroups report no block I/O for the container.</summary>
    public bool Available { get; set; } = true;
    public double? ReadBps { get; set; }
    public double? WriteBps { get; set; }
    /// <summary>null when docker.block_io.totals is false.</summary>
    public long? ReadTotal { get; set; }
    public long? WriteTotal { get; set; }

    public bool HasTotals => ReadTotal is not null || WriteTotal is not null;
}
