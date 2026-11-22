using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>Agents (target VMs): CRUD and the data for the detail view.</summary>
public partial class AgentService
{
    private readonly AgentRepository _repo;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly SshCredentialRepository _credentials;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly KnownContainerService _known;
    private readonly IStringLocalizer _l;

    public AgentService(AgentRepository repo, AgentSnapshotRepository snapshots, SshCredentialRepository credentials,
        MonitoringEvaluationService evaluation, KnownContainerService known, IStringLocalizer l)
    {
        _l = l;
        _known = known;
        _repo = repo;
        _snapshots = snapshots;
        _credentials = credentials;
        _evaluation = evaluation;
    }

    public async Task<ServiceResult<List<AgentDto>>> GetAll()
    {
        var credentials = (await _credentials.Select()).ToDictionary(c => c.Id, c => c.Name);
        var agents = (await _repo.Select())
            .Select(a => new AgentDto(a) { SshCredentialName = a.SshCredentialId is { } c ? credentials.GetValueOrDefault(c, "") : "" })
            .ToList();
        return ServiceResult<List<AgentDto>>.Ok(agents);
    }

    public async Task<ServiceResult<AgentDto>> Get(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AgentDto>.NotFound(_l["agents.notFound", id].Value);
        var credential = row.SshCredentialId is { } credentialId ? await _credentials.SelectById(credentialId) : null;
        return ServiceResult<AgentDto>.Ok(new AgentDto(row) { SshCredentialName = credential?.Name ?? "" });
    }

    public async Task<ServiceResult<AgentDetailDto>> GetDetail(int id)
    {
        var agent = await Get(id);
        if (!agent.IsSuccess)
            return ServiceResult<AgentDetailDto>.NotFound(agent.Message!);

        var detail = new AgentDetailDto { Agent = agent.Value!, Snapshot = await _snapshots.SelectByAgentId(id) };
        if (!string.IsNullOrEmpty(detail.Snapshot?.Payload))
        {
            try
            {
                detail.Monitoring = _evaluation.Parse(detail.Snapshot.Payload);
                var evaluation = _evaluation.Evaluate(detail.Monitoring, DateTime.UtcNow);
                detail.MissingContainers = await _known.GetMissing(id, detail.Monitoring.Docker);
                _known.AddFindings(evaluation, detail.MissingContainers);
                detail.Findings = evaluation.Findings;
            }
            catch (JsonException ex)
            {
                detail.Findings.Add(_l["agents.unreadable", ex.Message].Value);
            }
            detail.Snapshot.Payload = null;   // the parsed form is enough for the client
        }
        return ServiceResult<AgentDetailDto>.Ok(detail);
    }

    public async Task<ServiceResult<AgentDto>> Create(AgentDto dto)
    {
        var row = new AgentDb();
        var error = await Apply(dto, row);
        if (error is not null)
            return ServiceResult<AgentDto>.InvalidValue(error);
        if (await _repo.SelectByName(row.Name) is not null)
            return ServiceResult<AgentDto>.Conflict(_l["agents.nameExists"].Value);

        row.Id = await _repo.Insert(row);
        row.CreatedAt = DateTime.UtcNow;
        return ServiceResult<AgentDto>.Created(new AgentDto(row));
    }

    public async Task<ServiceResult<AgentDto>> Update(int id, AgentDto dto)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AgentDto>.NotFound(_l["agents.notFound", id].Value);
        var error = await Apply(dto, row);
        if (error is not null)
            return ServiceResult<AgentDto>.InvalidValue(error);
        var sameName = await _repo.SelectByName(row.Name);
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<AgentDto>.Conflict(_l["agents.nameExists"].Value);

        await _repo.Update(row);
        return ServiceResult<AgentDto>.Changed(new AgentDto(row));
    }

    public async Task<ServiceResult<AgentDto>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AgentDto>.NotFound(_l["agents.notFound", id].Value);
        await _snapshots.Delete(id);
        await _repo.Delete(id);
        return ServiceResult<AgentDto>.Ok(new AgentDto(row));
    }

    /// <summary>PATCH …/host-key: forget the pinned host key (e.g. after reinstalling the VM).</summary>
    public async Task<ServiceResult<AgentDto>> ResetHostKey(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AgentDto>.NotFound(_l["agents.notFound", id].Value);
        if (row.IsLocal())
            return ServiceResult<AgentDto>.NotAllowed(_l["agents.localNoHostKey"].Value);
        row.HostKeyFingerprint = null;
        await _repo.UpdateHostKeyFingerprint(id, null);
        return ServiceResult<AgentDto>.Changed(new AgentDto(row));
    }

    private async Task<string?> Apply(AgentDto dto, AgentDb row)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return _l["validation.nameLength", 100].Value;
        if (dto.PollIntervalSeconds is < 10 or > 86400)
            return _l["agents.invalidInterval"].Value;
        if (!AgentConnectionTypes.IsValid(dto.ConnectionType))
            return _l["agents.invalidConnectionType"].Value;

        var error = dto.ConnectionType == AgentConnectionTypes.Local
            ? await ApplyLocal(dto, row)
            : await ApplySsh(dto, row);
        if (error is not null)
            return error;

        row.Name = dto.Name.Trim();
        row.Description = dto.Description?.Trim() ?? "";
        row.ConnectionType = dto.ConnectionType;
        row.PollIntervalSeconds = dto.PollIntervalSeconds;
        row.Enabled = dto.Enabled;
        return null;
    }

    private async Task<string?> ApplyLocal(AgentDto dto, AgentDb row)
    {
        var dir = dto.LocalDir?.Trim() ?? "";
        if (!LocalDirPattern().IsMatch(dir))
            return _l["agents.invalidLocalDir"].Value;
        var existing = await _repo.SelectByLocalDir(dir);
        if (existing is not null && existing.Id != row.Id)
            return _l["agents.localDirInUse", dir, existing.Name].Value;

        row.LocalDir = dir;
        row.Host = null;
        row.Port = null;
        row.SshCredentialId = null;
        row.RemotePath = null;
        row.HostKeyFingerprint = null;
        return null;
    }

    private async Task<string?> ApplySsh(AgentDto dto, AgentDb row)
    {
        var host = dto.Host?.Trim() ?? "";
        if (host.Length is 0 or > 255 || host.Any(char.IsWhiteSpace) || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            return _l["agents.invalidHost"].Value;
        if (dto.Port is null or < 1 or > 65535)
            return _l["validation.port"].Value;
        var remotePath = dto.RemotePath?.Trim() ?? "";
        if (!remotePath.StartsWith('/') || remotePath.Any(c => char.IsControl(c) || c is '"' or '`' or '$'))
            return _l["agents.invalidRemotePath"].Value;
        if (dto.SshCredentialId is not { } credentialId || await _credentials.SelectById(credentialId) is null)
            return _l["agents.invalidCredential"].Value;

        row.Host = host;
        row.Port = dto.Port;
        row.SshCredentialId = credentialId;
        row.RemotePath = remotePath;
        row.LocalDir = null;
        row.HostKeyFingerprint = string.IsNullOrWhiteSpace(dto.HostKeyFingerprint) ? null : dto.HostKeyFingerprint.Trim();
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,62}$")]
    private static partial Regex LocalDirPattern();
}
