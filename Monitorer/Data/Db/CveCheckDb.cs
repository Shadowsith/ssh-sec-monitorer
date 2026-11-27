namespace Monitorer.Data.Db;

/// <summary>Table "cve_check": keywords (libs / languages / tools) searched in CVE sources.</summary>
public class CveCheckDb
{
    public int Id { get; set; }                         // id
    public string Name { get; set; } = "";              // name
    public string Keywords { get; set; } = "";          // keywords (one per line)
    public string Providers { get; set; } = "";         // providers ("Bsi,OpenCve")
    public int IntervalMinutes { get; set; } = 60;      // interval_minutes
    public bool Enabled { get; set; } = true;           // enabled
    public bool BaselinePending { get; set; } = true;   // baseline_pending (next run stores without alarm)
    public string? KbLastCommit { get; set; }           // kb_last_commit (opencve-kb SHA already processed)
    public string LastStatus { get; set; } = MonitorStatus.Unknown; // last_status
    public DateTime? LastCheckedAt { get; set; }        // last_checked_at
    public string? LastMessage { get; set; }            // last_message
    public DateTime CreatedAt { get; set; }             // created_at

    public List<string> KeywordList() =>
        Keywords.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public List<string> ProviderList() => CveProviders.Parse(Providers);
}
