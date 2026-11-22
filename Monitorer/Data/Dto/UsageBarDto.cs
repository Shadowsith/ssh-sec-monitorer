namespace Monitorer.Data.Dto;

/// <summary>A labelled percentage bar in the views (CPU, RAM, disk, ...).</summary>
public class UsageBarDto
{
    public string Label { get; }
    public double? Percent { get; }
    public string? Detail { get; }
    public double WarnPercent { get; }
    public double CriticalPercent { get; }

    public UsageBarDto(string label, double? percent, string? detail = null, double warnPercent = 85, double criticalPercent = 95)
    {
        Label = label;
        Percent = percent;
        Detail = detail;
        WarnPercent = warnPercent;
        CriticalPercent = criticalPercent;
    }

    public UsageBarDto(string label, decimal? percent, string? detail = null, double warnPercent = 85, double criticalPercent = 95)
        : this(label, percent is null ? null : (double)percent.Value, detail, warnPercent, criticalPercent) { }

    public string CssClass => Percent switch
    {
        null => "secondary",
        var p when p >= CriticalPercent => "danger",
        var p when p >= WarnPercent => "warning",
        _ => "success"
    };
}
