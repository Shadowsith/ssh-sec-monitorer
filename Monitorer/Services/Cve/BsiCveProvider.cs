using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Services.Cve;

/// <summary>
/// BSI CERT-Bund WID: the RSS feed of the advisories (title = product + summary) is matched against the keywords;
/// the CVE IDs of a matching advisory come from its CSAF document. The portal itself is a JavaScript app.
/// </summary>
public partial class BsiCveProvider : ICveProvider
{
    private const string RssUrl = "https://wid.cert-bund.de/content/public/securityAdvisory/rss";

    private readonly CveHttpService _http;
    private readonly ILogger<BsiCveProvider> _logger;

    public BsiCveProvider(CveHttpService http, ILogger<BsiCveProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string Key => CveProviders.Bsi;

    [GeneratedRegex(@"WID-SEC-(\d{4})-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AdvisoryId();

    [GeneratedRegex(@"^(\s*\[[^\]]*\])+\s*")]
    private static partial Regex LeadingTags();

    public async Task<IReadOnlyList<CveHit>> Search(CveCheckDb check, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var feed = XDocument.Parse(await _http.GetString(RssUrl, ct));
        var hits = new List<CveHit>();
        foreach (var item in feed.Descendants("item"))
        {
            var rawTitle = item.Element("title")?.Value ?? "";
            var title = LeadingTags().Replace(rawTitle, "");
            var keyword = CveText.FirstMatch($"{title}\n{item.Element("description")?.Value}", keywords);
            var link = item.Element("link")?.Value ?? "";
            var id = AdvisoryId().Match(link);
            if (keyword is null || !id.Success)
                continue;

            // the CSAF document of an advisory only changes with a new version (= new pubDate in the feed).
            // Not every advisory has one: some old ones never got one, brand-new ones get it a bit later than
            // the RSS entry - skip them (404 is not cached, so new ones are picked up on a later run).
            var year = id.Groups[1].Value;
            var csafUrl = $"https://wid.cert-bund.de/.well-known/csaf/white/{year}/wid-sec-w-{year}-{id.Groups[2].Value}.json";
            var json = await _http.TryGetString(csafUrl, ct, $"{csafUrl}|{item.Element("pubDate")?.Value}", TimeSpan.FromDays(1));
            if (json is null)
            {
                _logger.LogDebug("BSI: no CSAF document for {Advisory} ({Url})", id.Value, csafUrl);
                continue;
            }
            using var csaf = JsonDocument.Parse(json);
            var severity = CveSeverity.FromText(item.Element("category")?.Value);
            if (!csaf.RootElement.TryGetProperty("vulnerabilities", out var vulnerabilities)
                || vulnerabilities.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var vulnerability in vulnerabilities.EnumerateArray())
            {
                if (vulnerability.TryGetProperty("cve", out var cve) && CveText.IsCveId(cve.GetString()?.ToUpperInvariant()))
                    hits.Add(new CveHit(CveText.Normalize(cve.GetString()!), Key, keyword, title, severity, link));
            }
        }
        return hits;
    }
}
