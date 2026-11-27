namespace Monitorer.Data.Db;

/// <summary>Table "cve_finding": a CVE found by a CVE check. The CVE ID identifies it across sources and checks.</summary>
public class CveFindingDb
{
    public int Id { get; set; }                         // id
    public int CheckId { get; set; }                    // check_id
    public string CveId { get; set; } = "";             // cve_id, e.g. CVE-2026-12345
    public string Providers { get; set; } = "";         // providers that reported it ("Bsi,OpenCve")
    public string Keyword { get; set; } = "";           // keyword that matched first
    public string? Title { get; set; }                  // title
    public string? Description { get; set; }            // description
    public string? Severity { get; set; }               // severity (Low | Medium | High | Critical)
    public decimal? Cvss { get; set; }                  // cvss
    public DateTime? PublishedAt { get; set; }          // published_at
    public string? Url { get; set; }                    // url
    public DateTime FirstSeenAt { get; set; }           // first_seen_at
    public DateTime LastSeenAt { get; set; }            // last_seen_at
    public DateTime? EnrichedAt { get; set; }           // enriched_at
    public DateTime? AcknowledgedAt { get; set; }       // acknowledged_at
    public string? AcknowledgedBy { get; set; }         // acknowledged_by
    public bool IsBaseline { get; set; }                // is_baseline (found on the first run, no alarm)

    public bool IsOpen => AcknowledgedAt is null;
}
