namespace Monitorer.Data.Dto;

/// <summary>Result of one live check run.</summary>
public class LiveCheckResultDto
{
    public string Status { get; set; } = MonitorStatus.Unknown;
    public int? ResponseMs { get; set; }
    public string? Message { get; set; }
}
