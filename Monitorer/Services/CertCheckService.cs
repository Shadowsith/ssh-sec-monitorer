using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>CRUD for TLS certificate checks (host:port behind a public or local DNS name).</summary>
public class CertCheckService
{
    private readonly CertCheckRepository _repo;
    private readonly IStringLocalizer _l;

    public CertCheckService(CertCheckRepository repo, IStringLocalizer l)
    {
        _repo = repo;
        _l = l;
    }

    /// <summary>Sorted by days left (soonest expiry first), never-read checks last.</summary>
    public async Task<ServiceResult<List<CertCheckDto>>> GetAll() =>
        ServiceResult<List<CertCheckDto>>.Ok((await _repo.Select())
            .Select(r => new CertCheckDto(r))
            .OrderBy(c => c.DaysLeft ?? int.MaxValue)
            .ThenBy(c => c.Name)
            .ToList());

    public async Task<ServiceResult<CertCheckDto>> Get(int id)
    {
        var row = await _repo.SelectById(id);
        return row is null
            ? ServiceResult<CertCheckDto>.NotFound(_l["certChecks.notFound", id].Value)
            : ServiceResult<CertCheckDto>.Ok(new CertCheckDto(row));
    }

    public async Task<ServiceResult<CertCheckDto>> Create(CertCheckDto dto)
    {
        var row = new CertCheckDb();
        var error = Apply(dto, row);
        if (error is not null)
            return ServiceResult<CertCheckDto>.InvalidValue(error);
        if (await _repo.SelectByName(row.Name) is not null)
            return ServiceResult<CertCheckDto>.Conflict(_l["certChecks.nameExists"].Value);

        row.Id = await _repo.Insert(row);
        row.CreatedAt = DateTime.UtcNow;
        return ServiceResult<CertCheckDto>.Created(new CertCheckDto(row));
    }

    public async Task<ServiceResult<CertCheckDto>> Update(int id, CertCheckDto dto)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<CertCheckDto>.NotFound(_l["certChecks.notFound", id].Value);
        var error = Apply(dto, row);
        if (error is not null)
            return ServiceResult<CertCheckDto>.InvalidValue(error);
        var sameName = await _repo.SelectByName(row.Name);
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<CertCheckDto>.Conflict(_l["certChecks.nameExists"].Value);

        await _repo.Update(row);
        return ServiceResult<CertCheckDto>.Changed(new CertCheckDto(row));
    }

    public async Task<ServiceResult<CertCheckDto>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<CertCheckDto>.NotFound(_l["certChecks.notFound", id].Value);
        await _repo.Delete(id);
        return ServiceResult<CertCheckDto>.Ok(new CertCheckDto(row));
    }

    private string? Apply(CertCheckDto dto, CertCheckDb row)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return _l["validation.nameLength", 100].Value;
        if (!CertCheckProtocols.IsValid(dto.Protocol))
            return _l["certChecks.invalidProtocol"].Value;

        // a pasted URL (https://host:8443/path) is accepted: host and port are taken from it
        var host = dto.Host?.Trim() ?? "";
        var port = dto.Port ?? CertCheckProtocols.DefaultPort(dto.Protocol);
        if (host.Contains("://") && Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
            if (!uri.IsDefaultPort || uri.Scheme == "https")
                port = uri.Port;
        }
        if (host.Length is 0 or > 255 || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            return _l["certChecks.invalidHost"].Value;
        if (port is < 1 or > 65535)
            return _l["certChecks.invalidPort"].Value;
        var serverName = string.IsNullOrWhiteSpace(dto.ServerName) ? null : dto.ServerName.Trim();
        if (serverName is not null && (serverName.Length > 255 || Uri.CheckHostName(serverName) != UriHostNameType.Dns))
            return _l["certChecks.invalidServerName"].Value;
        if (dto.IntervalSeconds is < 60 or > 86400)
            return _l["certChecks.invalidInterval"].Value;
        if (dto.TimeoutSeconds is < 1 or > 120)
            return _l["certChecks.invalidTimeout"].Value;

        row.Name = dto.Name.Trim();
        row.Host = host;
        row.Port = port;
        row.Protocol = dto.Protocol;
        row.ServerName = serverName;
        row.IntervalSeconds = dto.IntervalSeconds;
        row.TimeoutSeconds = dto.TimeoutSeconds;
        row.Enabled = dto.Enabled;
        return null;
    }
}
