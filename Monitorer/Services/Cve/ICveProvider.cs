using Monitorer.Data.Db;

namespace Monitorer.Services.Cve;

/// <summary>A CVE reported by a source for one of the check's keywords.</summary>
public record CveHit(string CveId, string Provider, string Keyword, string? Title = null, string? Severity = null, string? Url = null);

/// <summary>A CVE source (CveProviders key). Throws if the source cannot be read.</summary>
public interface ICveProvider
{
    string Key { get; }

    /// <summary>
    /// CVEs matching the keywords. May update provider state on the check (opencve-kb: KbLastCommit);
    /// during a baseline run (check.BaselinePending) sources with a cursor only set it to "now".
    /// </summary>
    Task<IReadOnlyList<CveHit>> Search(CveCheckDb check, IReadOnlyList<string> keywords, CancellationToken ct);
}
