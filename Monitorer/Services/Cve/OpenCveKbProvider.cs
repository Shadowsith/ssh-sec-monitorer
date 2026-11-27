using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Services.Cve;

/// <summary>
/// GitHub opencve/opencve-kb (one JSON file per CVE, a commit about every 15 min). The repository is not cloned:
/// the GitHub API lists the files changed since the last commit seen; only files of the current year whose diff
/// mentions a keyword are downloaded and checked (title, description, vendors / products).
/// </summary>
public partial class OpenCveKbProvider : ICveProvider
{
    private const string Api = "https://api.github.com/repos/opencve/opencve-kb";
    /// <summary>GitHub's compare endpoint returns at most 300 files.</summary>
    private const int CompareFileLimit = 300;
    private const int MaxFileDownloads = 100;

    private readonly CveHttpService _http;
    private readonly ILogger<OpenCveKbProvider> _logger;

    public OpenCveKbProvider(CveHttpService http, ILogger<OpenCveKbProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string Key => CveProviders.OpenCveKb;

    [GeneratedRegex(@"^(\d{4})/(CVE-\d{4}-\d{4,})\.json$")]
    private static partial Regex CveFile();

    public async Task<IReadOnlyList<CveHit>> Search(CveCheckDb check, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        string head;
        using (var commits = await _http.GetJson($"{Api}/commits?per_page=1", ct))
            head = commits.RootElement[0].GetProperty("sha").GetString()!;

        // baseline / first run: only remember where we are
        if (check.BaselinePending || check.KbLastCommit is null)
        {
            check.KbLastCommit = head;
            return [];
        }
        if (check.KbLastCommit == head)
            return [];

        JsonDocument compare;
        try
        {
            compare = await _http.GetJson($"{Api}/compare/{check.KbLastCommit}...{head}", ct);
        }
        catch (CveHttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // the old commit is gone (history rewritten) - start again from now
            _logger.LogWarning("opencve-kb: commit {Commit} not found, continuing from {Head}", check.KbLastCommit, head);
            check.KbLastCommit = head;
            return [];
        }

        var hits = new List<CveHit>();
        using (compare)
        {
            var files = compare.RootElement.TryGetProperty("files", out var f) && f.ValueKind == JsonValueKind.Array
                ? f.EnumerateArray().ToList()
                : [];
            if (files.Count >= CompareFileLimit)
                _logger.LogWarning("opencve-kb: {Count} changed files since {Commit} - GitHub lists only the first {Limit}",
                    files.Count, check.KbLastCommit, CompareFileLimit);

            var year = DateTime.UtcNow.Year.ToString();
            var downloads = 0;
            foreach (var file in files)
            {
                var name = CveFile().Match(file.GetProperty("filename").GetString() ?? "");
                if (!name.Success || name.Groups[1].Value != year)
                    continue;
                // cheap pre-filter on the diff; files without a diff (too large) are always checked
                var patch = file.TryGetProperty("patch", out var p) ? p.GetString() : null;
                if (patch is not null && CveText.FirstMatch(patch, keywords) is null)
                    continue;
                if (++downloads > MaxFileDownloads)
                {
                    _logger.LogWarning("opencve-kb: more than {Max} candidate files, the rest is skipped", MaxFileDownloads);
                    break;
                }
                var cveId = name.Groups[2].Value;
                var json = await _http.TryGetString(OpenCveKbDocument.RawUrl(cveId), ct);
                if (json is null)
                    continue;
                var kb = OpenCveKbDocument.Parse(json);
                var keyword = CveText.FirstMatch(kb.MatchText, keywords);
                if (keyword is not null)
                    hits.Add(new CveHit(cveId, Key, keyword, kb.Title, CveSeverity.FromScore(kb.Cvss), $"https://app.opencve.io/cve/{cveId}"));
            }
        }
        check.KbLastCommit = head;
        return hits;
    }
}
