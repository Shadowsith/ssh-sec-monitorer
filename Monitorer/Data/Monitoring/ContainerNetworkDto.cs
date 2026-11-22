namespace Monitorer.Data.Monitoring;

/// <summary>Network totals of a container in bytes.</summary>
public class ContainerNetworkDto
{
    public long? RxTotal { get; set; }
    public long? TxTotal { get; set; }
}
