using Microsoft.Extensions.Localization;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;
using Monitorer.Services.Cve;

namespace Monitorer.Services;

/// <summary>Found CVEs: list for the page, acknowledge (ends the alarm in every check that found the CVE).</summary>
public class CveFindingService
{
    private const int PageLimit = 200;

    private readonly CveFindingRepository _repo;
    private readonly LiveUpdateService _live;
    private readonly IStringLocalizer _l;

    public CveFindingService(CveFindingRepository repo, LiveUpdateService live, IStringLocalizer l)
    {
        _repo = repo;
        _live = live;
        _l = l;
    }

    public async Task<ServiceResult<List<CveFindingDto>>> GetForPage(bool includeBaseline) =>
        ServiceResult<List<CveFindingDto>>.Ok((await _repo.SelectForPage(includeBaseline, PageLimit)).ToList());

    /// <summary>POST api/cve-checks/findings/{cveId}/acknowledge.</summary>
    public async Task<ServiceResult<int>> Acknowledge(string cveId, string userName)
    {
        cveId = CveText.Normalize(cveId ?? "");
        if (!CveText.IsCveId(cveId))
            return ServiceResult<int>.InvalidValue(_l["cveChecks.invalidCveId"].Value);
        var rows = await _repo.Acknowledge(cveId, string.IsNullOrWhiteSpace(userName) ? "?" : userName, DateTime.UtcNow);
        if (rows == 0)
            return ServiceResult<int>.NotFound(_l["cveChecks.findingNotOpen", cveId].Value);
        await _live.Publish(LiveUpdateService.Cves, LiveUpdateService.Alerts);
        return ServiceResult<int>.Ok(rows);
    }
}
