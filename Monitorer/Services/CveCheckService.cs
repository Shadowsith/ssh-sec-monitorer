using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>CRUD for CVE checks (keywords + sources + interval in minutes).</summary>
public class CveCheckService
{
    public const int MinIntervalMinutes = 15;
    public const int MaxIntervalMinutes = 10080;
    private const int MaxKeywords = 50;

    private readonly CveCheckRepository _repo;
    private readonly CveFindingRepository _findings;
    private readonly IStringLocalizer _l;

    public CveCheckService(CveCheckRepository repo, CveFindingRepository findings, IStringLocalizer l)
    {
        _repo = repo;
        _findings = findings;
        _l = l;
    }

    public async Task<ServiceResult<List<CveCheckDto>>> GetAll()
    {
        var open = await _findings.CountOpenByCheck();
        return ServiceResult<List<CveCheckDto>>.Ok((await _repo.Select())
            .Select(r => new CveCheckDto(r) { OpenCount = open.GetValueOrDefault(r.Id) })
            .ToList());
    }

    public async Task<ServiceResult<CveCheckDto>> Get(int id)
    {
        var row = await _repo.SelectById(id);
        return row is null
            ? ServiceResult<CveCheckDto>.NotFound(_l["cveChecks.notFound", id].Value)
            : ServiceResult<CveCheckDto>.Ok(new CveCheckDto(row));
    }

    public async Task<ServiceResult<CveCheckDto>> Create(CveCheckDto dto)
    {
        var row = new CveCheckDb();
        var error = Apply(dto, row);
        if (error is not null)
            return ServiceResult<CveCheckDto>.InvalidValue(error);
        if (await _repo.SelectByName(row.Name) is not null)
            return ServiceResult<CveCheckDto>.Conflict(_l["cveChecks.nameExists"].Value);

        row.BaselinePending = true;
        row.Id = await _repo.Insert(row);
        row.CreatedAt = DateTime.UtcNow;
        return ServiceResult<CveCheckDto>.Created(new CveCheckDto(row));
    }

    public async Task<ServiceResult<CveCheckDto>> Update(int id, CveCheckDto dto)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<CveCheckDto>.NotFound(_l["cveChecks.notFound", id].Value);
        var searchBefore = (row.Keywords, row.Providers);
        var error = Apply(dto, row);
        if (error is not null)
            return ServiceResult<CveCheckDto>.InvalidValue(error);
        var sameName = await _repo.SelectByName(row.Name);
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<CveCheckDto>.Conflict(_l["cveChecks.nameExists"].Value);

        // other keywords / sources find other (old) CVEs - the next run stores them silently again
        if (searchBefore != (row.Keywords, row.Providers))
            row.BaselinePending = true;
        await _repo.Update(row);
        return ServiceResult<CveCheckDto>.Changed(new CveCheckDto(row));
    }

    public async Task<ServiceResult<CveCheckDto>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<CveCheckDto>.NotFound(_l["cveChecks.notFound", id].Value);
        await _repo.Delete(id);
        return ServiceResult<CveCheckDto>.Ok(new CveCheckDto(row));
    }

    private string? Apply(CveCheckDto dto, CveCheckDb row)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return _l["validation.nameLength", 100].Value;

        // one per line; commas / semicolons are accepted as separators too
        var keywords = (dto.Keywords ?? "")
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .DistinctBy(k => k.ToLowerInvariant())
            .ToList();
        if (keywords.Count is 0 or > MaxKeywords)
            return _l["cveChecks.invalidKeywordCount", MaxKeywords].Value;
        if (keywords.FirstOrDefault(k => k.Length is < 2 or > 100) is { } bad)
            return _l["cveChecks.invalidKeyword", bad].Value;

        var providers = dto.Providers ?? [];
        if (providers.Count == 0 || providers.Any(p => !CveProviders.IsValid(p)))
            return _l["cveChecks.invalidProviders"].Value;
        if (dto.IntervalMinutes is < MinIntervalMinutes or > MaxIntervalMinutes)
            return _l["cveChecks.invalidInterval", MinIntervalMinutes, MaxIntervalMinutes].Value;

        row.Name = dto.Name.Trim();
        row.Keywords = string.Join("\n", keywords);
        row.Providers = string.Join(",", CveProviders.Parse(string.Join(",", providers)));
        row.IntervalMinutes = dto.IntervalMinutes;
        row.Enabled = dto.Enabled;
        return null;
    }
}
