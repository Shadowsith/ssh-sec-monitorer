using Monitorer.Data;
using Monitorer.Repository;

namespace Monitorer.Services.Cve;

/// <summary>
/// Fills title, description, CVSS, severity and publish date of new findings from the opencve-kb file of the CVE
/// (raw.githubusercontent.com, one request per CVE). Best effort: missing files are retried for a day.
/// </summary>
public class CveEnrichmentService
{
    private const int MaxPerRun = 50;
    private static readonly TimeSpan RetryMissingFor = TimeSpan.FromDays(1);

    private readonly CveFindingRepository _findings;
    private readonly CveHttpService _http;
    private readonly ILogger<CveEnrichmentService> _logger;

    public CveEnrichmentService(CveFindingRepository findings, CveHttpService http, ILogger<CveEnrichmentService> logger)
    {
        _findings = findings;
        _http = http;
        _logger = logger;
    }

    public async Task Enrich(int checkId, CancellationToken ct)
    {
        var rows = (await _findings.SelectNotEnriched(checkId, MaxPerRun)).ToList();
        if (rows.Count == 0)
            return;
        // the same CVE may already be enriched in another check
        var known = (await _findings.SelectByCveIds(rows.Select(r => r.CveId).ToList()))
            .Where(r => r.EnrichedAt is not null)
            .GroupBy(r => r.CveId)
            .ToDictionary(g => g.Key, g => g.First());
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            try
            {
                if (known.TryGetValue(row.CveId, out var other))
                {
                    row.Title = other.Title ?? row.Title;
                    row.Description = other.Description;
                    row.Cvss = other.Cvss;
                    row.Severity = other.Severity ?? row.Severity;
                    row.PublishedAt = other.PublishedAt;
                    row.EnrichedAt = now;
                }
                else if (await _http.TryGetString(OpenCveKbDocument.RawUrl(row.CveId), ct) is { } json)
                {
                    var kb = OpenCveKbDocument.Parse(json);
                    row.Title = kb.Title ?? row.Title;
                    row.Description = kb.Description;
                    row.Cvss = kb.Cvss;
                    row.Severity = CveSeverity.FromScore(kb.Cvss) ?? row.Severity;
                    row.PublishedAt = kb.PublishedAt;
                    row.EnrichedAt = now;
                }
                else if (now - row.FirstSeenAt > RetryMissingFor)
                {
                    row.EnrichedAt = now;   // give up, keep the source's own title / severity
                }
                else
                {
                    continue;
                }
                await _findings.UpdateEnrichment(row);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "CVE {CveId}: details not loaded", row.CveId);
            }
        }
    }
}
