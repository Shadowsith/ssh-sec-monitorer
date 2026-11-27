namespace Monitorer.Data;

/// <summary>Severity of a found CVE, from the CVSS score or the source's own rating.</summary>
public static class CveSeverity
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    /// <summary>CVSS v3 / v4 qualitative rating.</summary>
    public static string? FromScore(decimal? score) => score switch
    {
        null or <= 0 => null,
        < 4.0m => Low,
        < 7.0m => Medium,
        < 9.0m => High,
        _ => Critical
    };

    /// <summary>BSI ratings (niedrig / mittel / hoch / kritisch) and plain English words.</summary>
    public static string? FromText(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "niedrig" or "low" => Low,
        "mittel" or "medium" or "moderate" => Medium,
        "hoch" or "high" or "important" => High,
        "kritisch" or "critical" => Critical,
        _ => null
    };

    public static string CssClass(string? severity) => severity switch
    {
        Critical => "danger",
        High => "warning",
        Medium => "info",
        Low => "secondary",
        _ => "light border"
    };

    public static int Rank(string? severity) => severity switch
    {
        Critical => 4,
        High => 3,
        Medium => 2,
        Low => 1,
        _ => 0
    };
}
