namespace Monitorer.Data;

/// <summary>Status values of agents and live checks (stored as text).</summary>
public static class MonitorStatus
{
    public const string Unknown = "Unknown";
    public const string Ok = "Ok";
    public const string Warning = "Warning";
    public const string Critical = "Critical";
    public const string Unreachable = "Unreachable";

    // live checks
    public const string Up = "Up";
    public const string Down = "Down";

    /// <summary>Higher = worse; used to combine several findings.</summary>
    public static int Severity(string status) => status switch
    {
        Ok or Up => 0,
        Unknown => 1,
        Warning => 2,
        Critical => 3,
        Unreachable or Down => 4,
        _ => 1
    };

    /// <summary>Bootstrap color class for badges.</summary>
    public static string CssClass(string? status) => status switch
    {
        Ok or Up => "success",
        Warning => "warning",
        Critical or Down or Unreachable => "danger",
        _ => "secondary"
    };
}
