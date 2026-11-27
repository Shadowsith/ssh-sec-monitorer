using System.Xml.Linq;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Services.Cve;

/// <summary>tldrcve.com RSS feed: written-up CVEs, each item lists its CVE IDs as &lt;category domain="cve"&gt;.</summary>
public class TldrCveProvider : ICveProvider
{
    private const string FeedUrl = "https://tldrcve.com/feed.xml";

    private readonly CveHttpService _http;

    public TldrCveProvider(CveHttpService http) => _http = http;

    public string Key => CveProviders.TldrCve;

    public async Task<IReadOnlyList<CveHit>> Search(CveCheckDb check, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var feed = XDocument.Parse(await _http.GetString(FeedUrl, ct));
        var hits = new List<CveHit>();
        foreach (var item in feed.Descendants("item"))
        {
            var title = item.Element("title")?.Value ?? "";
            var keyword = CveText.FirstMatch($"{title}\n{CveText.StripHtml(item.Element("description")?.Value)}", keywords);
            if (keyword is null)
                continue;
            var ids = item.Elements("category")
                .Where(c => (string?)c.Attribute("domain") == "cve")
                .Select(c => c.Value)
                .Concat(CveText.CveIdPattern().Matches(title).Select(m => m.Value))
                .Select(CveText.Normalize)
                .Where(CveText.IsCveId)
                .Distinct();
            var link = item.Element("link")?.Value;
            hits.AddRange(ids.Select(id => new CveHit(id, Key, keyword, title, null, link)));
        }
        return hits;
    }
}
