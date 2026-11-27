using Dapper;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;

namespace Monitorer.Repository;

/// <summary>SQL for table "cve_finding".</summary>
public class CveFindingRepository
{
    private const string SelectWithCheck =
        "SELECT f.*, c.name AS check_name FROM cve_finding f JOIN cve_check c ON c.id = f.check_id";

    private readonly DbConnectionFactory _factory;

    public CveFindingRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>For the page: open findings first, then the newest. Baseline rows only if asked for.</summary>
    public async Task<IEnumerable<CveFindingDto>> SelectForPage(bool includeBaseline, int limit)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CveFindingDto>(
            $"""
            {SelectWithCheck}
            WHERE @includeBaseline OR NOT f.is_baseline
            ORDER BY (f.acknowledged_at IS NULL) DESC, f.first_seen_at DESC, f.cve_id DESC
            LIMIT @limit
            """,
            new { includeBaseline, limit });
    }

    /// <summary>Open findings of enabled checks (for the alert rules).</summary>
    public async Task<IEnumerable<CveFindingDto>> SelectOpen()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CveFindingDto>(
            $"{SelectWithCheck} WHERE f.acknowledged_at IS NULL AND c.enabled ORDER BY f.cve_id");
    }

    /// <summary>All rows (of every check) for these CVE IDs.</summary>
    public async Task<IEnumerable<CveFindingDb>> SelectByCveIds(IReadOnlyCollection<string> cveIds)
    {
        if (cveIds.Count == 0)
            return [];
        using var conn = _factory.Create();
        // Postgres array parameter (Dapper does not expand "IN @list" for Npgsql)
        return await conn.QueryAsync<CveFindingDb>(
            "SELECT * FROM cve_finding WHERE cve_id = ANY(@cveIds)", new { cveIds = cveIds.ToArray() });
    }

    /// <summary>Rows of a check that still lack details from the opencve-kb.</summary>
    public async Task<IEnumerable<CveFindingDb>> SelectNotEnriched(int checkId, int limit)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CveFindingDb>(
            "SELECT * FROM cve_finding WHERE check_id = @checkId AND enriched_at IS NULL ORDER BY first_seen_at DESC LIMIT @limit",
            new { checkId, limit });
    }

    /// <summary>check id → number of open findings.</summary>
    public async Task<Dictionary<int, int>> CountOpenByCheck()
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<(int CheckId, int Count)>(
            "SELECT check_id, count(*)::int FROM cve_finding WHERE acknowledged_at IS NULL GROUP BY check_id");
        return rows.ToDictionary(r => r.CheckId, r => r.Count);
    }

    public async Task<int> Insert(CveFindingDb row)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO cve_finding (check_id, cve_id, providers, keyword, title, severity, url, first_seen_at,
                                     last_seen_at, acknowledged_at, acknowledged_by, is_baseline)
            VALUES (@CheckId, @CveId, @Providers, @Keyword, @Title, @Severity, @Url, @FirstSeenAt,
                    @LastSeenAt, @AcknowledgedAt, @AcknowledgedBy, @IsBaseline)
            ON CONFLICT (check_id, cve_id) DO NOTHING
            RETURNING id
            """,
            row);
    }

    /// <summary>UPDATE — seen again (possibly by more sources).</summary>
    public async Task UpdateSeen(CveFindingDb row)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            """
            UPDATE cve_finding
            SET providers = @Providers,
                last_seen_at = @LastSeenAt,
                title = COALESCE(title, @Title),
                severity = COALESCE(severity, @Severity),
                url = COALESCE(url, @Url)
            WHERE id = @Id
            """,
            row);
    }

    /// <summary>UPDATE — details from the opencve-kb (EnrichedAt is set even if nothing was found).</summary>
    public async Task UpdateEnrichment(CveFindingDb row)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            """
            UPDATE cve_finding
            SET title = @Title,
                description = @Description,
                severity = @Severity,
                cvss = @Cvss,
                published_at = @PublishedAt,
                enriched_at = @EnrichedAt
            WHERE id = @Id
            """,
            row);
    }

    /// <summary>Acknowledges the CVE in every check that found it. Returns the number of rows changed.</summary>
    public async Task<int> Acknowledge(string cveId, string by, DateTime at)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync(
            "UPDATE cve_finding SET acknowledged_at = @at, acknowledged_by = @by WHERE cve_id = @cveId AND acknowledged_at IS NULL",
            new { cveId, by, at });
    }
}
