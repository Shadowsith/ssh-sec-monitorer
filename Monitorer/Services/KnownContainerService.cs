using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Monitoring;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>
/// Remembers every container seen on an agent. Containers that were there once and are gone now
/// are shown greyed out ("missing") until a user removes them.
/// </summary>
public class KnownContainerService
{
    private readonly KnownContainerRepository _repo;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly MonitoringSettings _settings;
    private readonly List<Regex> _ignore;

    public KnownContainerService(KnownContainerRepository repo, AgentSnapshotRepository snapshots,
        MonitoringEvaluationService evaluation, IOptions<MonitoringSettings> settings)
    {
        _repo = repo;
        _snapshots = snapshots;
        _evaluation = evaluation;
        _settings = settings.Value;
        _ignore = _settings.KnownContainerIgnore
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(WildcardToRegex)
            .ToList();
    }

    /// <summary>After a successful fetch: mark all current containers as seen. Does nothing if Docker was not readable.</summary>
    public async Task Sync(int agentId, DockerSectionDto? docker, DateTime nowUtc)
    {
        if (docker is not { Available: true })
            return;
        foreach (var c in docker.Containers.Where(c => !IsIgnored(c.Name)))
        {
            await _repo.Upsert(new KnownContainerDb
            {
                AgentId = agentId,
                Name = c.Name,
                Image = c.Image,
                LastState = c.State,
                FirstSeenAt = nowUtc,
                LastSeenAt = nowUtc
            });
        }
    }

    /// <summary>Known containers of an agent that are not in its current container list.</summary>
    public async Task<List<KnownContainerDb>> GetMissing(int agentId, DockerSectionDto? docker) =>
        FilterMissing(await _repo.SelectByAgentId(agentId), docker);

    /// <summary>Missing containers for several agents at once (Docker view).</summary>
    public async Task<Dictionary<int, List<KnownContainerDb>>> GetMissing(IReadOnlyDictionary<int, DockerSectionDto?> dockerByAgent)
    {
        var known = (await _repo.Select()).GroupBy(k => k.AgentId).ToDictionary(g => g.Key, g => g.ToList());
        return dockerByAgent.ToDictionary(
            entry => entry.Key,
            entry => FilterMissing(known.GetValueOrDefault(entry.Key) ?? [], entry.Value));
    }

    /// <summary>Adds one finding per missing container, with the status from Monitoring:MissingContainerStatus.</summary>
    public void AddFindings(EvaluationDto evaluation, IEnumerable<KnownContainerDb> missing)
    {
        var status = _settings.MissingContainerStatus;
        if (status is not (MonitorStatus.Warning or MonitorStatus.Critical))
            return;
        foreach (var m in missing)
            evaluation.Raise(status, $"Container {m.Name} ist nicht mehr vorhanden (zuletzt gesehen {m.LastSeenAt:yyyy-MM-dd HH:mm} UTC).");
    }

    /// <summary>DELETE: forget one container (e.g. it was removed on purpose).</summary>
    public async Task<ServiceResult<KnownContainerDb>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<KnownContainerDb>.NotFound($"Container-Eintrag {id} nicht gefunden.");
        await _repo.Delete(id);
        return ServiceResult<KnownContainerDb>.Ok(row);
    }

    /// <summary>DELETE: forget all containers of an agent that are currently missing.</summary>
    public async Task<ServiceResult<List<KnownContainerDb>>> DeleteMissing(int agentId)
    {
        var missing = await GetMissing(agentId, await LoadDocker(agentId));
        foreach (var m in missing)
            await _repo.Delete(m.Id);
        return ServiceResult<List<KnownContainerDb>>.Ok(missing);
    }

    private async Task<DockerSectionDto?> LoadDocker(int agentId)
    {
        var payload = (await _snapshots.SelectByAgentId(agentId))?.Payload;
        if (string.IsNullOrEmpty(payload))
            return null;
        try
        {
            return _evaluation.Parse(payload).Docker;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Without readable Docker data nothing counts as missing - otherwise an outage would grey out everything.</summary>
    private List<KnownContainerDb> FilterMissing(IEnumerable<KnownContainerDb> known, DockerSectionDto? docker)
    {
        if (docker is not { Available: true })
            return [];
        var present = docker.Containers.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        return known
            .Where(k => !present.Contains(k.Name) && !IsIgnored(k.Name))
            .OrderByDescending(k => k.LastSeenAt)
            .ToList();
    }

    private bool IsIgnored(string name) => _ignore.Any(r => r.IsMatch(name));

    private static Regex WildcardToRegex(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
