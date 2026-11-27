using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;
using Monitorer.Services.Cve;

namespace Monitorer.Services;

/// <summary>
/// Runs CVE checks: asks every selected source, merges the hits by CVE ID and stores new findings. A CVE that is
/// already known (from another source or another check, and acknowledged there) never becomes open again; on a
/// baseline run (first run / changed keywords or sources) everything found is stored as acknowledged baseline.
/// </summary>
public class CveCheckRunnerService
{
    private readonly CveCheckRepository _checks;
    private readonly CveFindingRepository _findings;
    private readonly IEnumerable<ICveProvider> _providers;
    private readonly CveEnrichmentService _enrichment;
    private readonly PollScheduler _scheduler;
    private readonly LiveUpdateService _live;
    private readonly IStringLocalizer _l;
    private readonly ILogger<CveCheckRunnerService> _logger;

    public CveCheckRunnerService(CveCheckRepository checks, CveFindingRepository findings, IEnumerable<ICveProvider> providers,
        CveEnrichmentService enrichment, PollScheduler scheduler, LiveUpdateService live, IStringLocalizer l,
        ILogger<CveCheckRunnerService> logger)
    {
        _checks = checks;
        _findings = findings;
        _providers = providers;
        _enrichment = enrichment;
        _scheduler = scheduler;
        _live = live;
        _l = l;
        _logger = logger;
    }

    /// <summary>Due checks run one after the other - the sources should never see a burst of requests.</summary>
    public async Task RunDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = (await _checks.SelectEnabled())
            .Where(c => !_scheduler.IsRunning(PollScheduler.CveCheckKey(c.Id))
                        && _scheduler.IsDue(PollScheduler.CveCheckKey(c.Id), c.IntervalMinutes * 60, c.LastCheckedAt, now))
            .OrderBy(c => c.LastCheckedAt ?? DateTime.MinValue)
            .ToList();
        foreach (var check in due)
            await RunGuarded(check, ct);
    }

    /// <summary>POST api/cve-checks/{id}/run.</summary>
    public async Task<ServiceResult<CveCheckDto>> RunNow(int id, CancellationToken ct)
    {
        var check = await _checks.SelectById(id);
        if (check is null)
            return ServiceResult<CveCheckDto>.NotFound(_l["cveChecks.notFound", id].Value);
        if (!await RunGuarded(check, ct))
            return ServiceResult<CveCheckDto>.Locked(_l["cveChecks.running"].Value);
        return ServiceResult<CveCheckDto>.Ok(new CveCheckDto(check));
    }

    private async Task<bool> RunGuarded(CveCheckDb check, CancellationToken ct)
    {
        var key = PollScheduler.CveCheckKey(check.Id);
        if (!_scheduler.TryStart(key))
            return false;
        try
        {
            await RunAndStore(check, ct);
            await _live.Publish(LiveUpdateService.Cves);
            return true;
        }
        finally
        {
            _scheduler.Finish(key);
        }
    }

    private async Task RunAndStore(CveCheckDb check, CancellationToken ct)
    {
        var keywords = check.KeywordList();
        var selected = check.ProviderList();
        var hits = new List<CveHit>();
        var failed = new List<string>();
        foreach (var provider in _providers.Where(p => selected.Contains(p.Key)))
        {
            try
            {
                hits.AddRange(await provider.Search(check, keywords, ct));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "CVE check {Name}: source {Provider} failed", check.Name, provider.Key);
                failed.Add($"{CveProviders.DisplayName(provider.Key)}: {ex.GetBaseException().Message}");
            }
        }

        var baseline = check.BaselinePending;
        var newOpen = await Store(check, hits, baseline);
        await _enrichment.Enrich(check.Id, ct);

        // the baseline is used up as soon as one source delivered
        if (failed.Count < selected.Count)
            check.BaselinePending = false;
        var open = (await _findings.CountOpenByCheck()).GetValueOrDefault(check.Id);
        var found = hits.Select(h => h.CveId).Distinct().Count();
        var previous = check.LastStatus;
        check.LastStatus = failed.Count == selected.Count ? MonitorStatus.Down
            : failed.Count > 0 ? MonitorStatus.Degraded
            : open > 0 ? MonitorStatus.Warning
            : MonitorStatus.Ok;
        check.LastMessage = string.Join(" · ", new[]
            {
                baseline ? _l["cveChecks.resultBaseline", found].Value : _l["cveChecks.result", found, newOpen, open].Value
            }
            .Concat(failed));
        check.LastCheckedAt = DateTime.UtcNow;
        await _checks.UpdateResult(check);

        if (newOpen > 0 || previous != check.LastStatus)
            _logger.LogInformation("CVE check {Name}: {Status}, {New} new CVE(s) ({Message})",
                check.Name, check.LastStatus, newOpen, check.LastMessage);
    }

    /// <summary>Inserts new findings / updates known ones. Returns the number of new open findings.</summary>
    private async Task<int> Store(CveCheckDb check, List<CveHit> hits, bool baseline)
    {
        var merged = hits.GroupBy(h => h.CveId).ToList();
        var existing = (await _findings.SelectByCveIds(merged.Select(g => g.Key).ToList())).ToList();
        var now = DateTime.UtcNow;
        var newOpen = 0;

        foreach (var group in merged)
        {
            var providers = group.Select(h => h.Provider).Distinct().ToList();
            var title = group.Select(h => h.Title).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            var severity = group.Select(h => h.Severity).MaxBy(CveSeverity.Rank);
            var url = group.Select(h => h.Url).FirstOrDefault(u => u is not null);

            var own = existing.FirstOrDefault(e => e.CheckId == check.Id && e.CveId == group.Key);
            if (own is not null)
            {
                own.Providers = string.Join(",", CveProviders.Parse(own.Providers).Union(providers).Distinct());
                own.LastSeenAt = now;
                own.Title = title;
                own.Severity = severity;
                own.Url = url;
                await _findings.UpdateSeen(own);
                continue;
            }

            var row = new CveFindingDb
            {
                CheckId = check.Id,
                CveId = group.Key,
                Providers = string.Join(",", CveProviders.Parse(string.Join(",", providers))),
                Keyword = group.First().Keyword,
                Title = title,
                Severity = severity,
                Url = url,
                FirstSeenAt = now,
                LastSeenAt = now
            };
            // one notification per CVE: acknowledged in another check = acknowledged here too
            var acknowledged = existing.FirstOrDefault(e => e.CveId == group.Key && e.AcknowledgedAt is not null);
            if (baseline)
            {
                row.IsBaseline = true;
                row.AcknowledgedAt = now;
            }
            else if (acknowledged is not null)
            {
                row.AcknowledgedAt = acknowledged.AcknowledgedAt;
                row.AcknowledgedBy = acknowledged.AcknowledgedBy;
            }
            if (await _findings.Insert(row) > 0 && row.AcknowledgedAt is null)
                newOpen++;
        }
        return newOpen;
    }
}
