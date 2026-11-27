using System.Text.RegularExpressions;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Services.Cve;

/// <summary>
/// app.opencve.io search page (server-rendered HTML, /cve/ is allowed by robots.txt): first result page per keyword.
/// Only the CVE IDs are taken from the HTML - the details come from the opencve-kb (CveEnrichmentService).
/// </summary>
public partial class OpenCveProvider : ICveProvider
{
    private static readonly TimeSpan PauseBetweenRequests = TimeSpan.FromSeconds(1);

    private readonly CveHttpService _http;

    public OpenCveProvider(CveHttpService http) => _http = http;

    public string Key => CveProviders.OpenCve;

    [GeneratedRegex(@"href=""/cve/(CVE-\d{4}-\d{4,})""", RegexOptions.IgnoreCase)]
    private static partial Regex CveLink();

    public async Task<IReadOnlyList<CveHit>> Search(CveCheckDb check, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var hits = new List<CveHit>();
        for (var i = 0; i < keywords.Count; i++)
        {
            if (i > 0)
                await Task.Delay(PauseBetweenRequests, ct);   // one request after the other, never a burst
            var html = await _http.GetString($"https://app.opencve.io/cve/?search={Uri.EscapeDataString(keywords[i])}", ct);
            hits.AddRange(CveLink().Matches(html)
                .Select(m => CveText.Normalize(m.Groups[1].Value))
                .Distinct()
                .Select(id => new CveHit(id, Key, keywords[i], Url: $"https://app.opencve.io/cve/{id}")));
        }
        return hits;
    }
}
