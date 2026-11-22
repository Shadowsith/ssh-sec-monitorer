namespace Monitorer.Data.Monitoring;

/// <summary>Traffic of one network interface (totals in bytes, rates in bytes/s).</summary>
public class NetworkInterfaceDto
{
    public string Interface { get; set; } = "";
    public long? RxTotal { get; set; }
    public long? TxTotal { get; set; }
    public double? RxBps { get; set; }
    public double? TxBps { get; set; }
}
