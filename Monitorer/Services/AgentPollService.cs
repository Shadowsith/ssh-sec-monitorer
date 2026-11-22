using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>Fetches monitoring.json of the agents (cyclically and on demand) and stores the snapshot.</summary>
public class AgentPollService
{
    private readonly AgentRepository _agents;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly MonitoringFetchService _fetch;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly KnownContainerService _known;
    private readonly PollScheduler _scheduler;
    private readonly MonitoringSettings _settings;
    private readonly ILogger<AgentPollService> _logger;

    public AgentPollService(AgentRepository agents, AgentSnapshotRepository snapshots, MonitoringFetchService fetch,
        MonitoringEvaluationService evaluation, KnownContainerService known, PollScheduler scheduler,
        IOptions<MonitoringSettings> settings, ILogger<AgentPollService> logger)
    {
        _agents = agents;
        _snapshots = snapshots;
        _fetch = fetch;
        _evaluation = evaluation;
        _known = known;
        _scheduler = scheduler;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Polls the enabled agents that are due in their time slot (see PollScheduler): spread over the interval,
    /// longest waiting first, at most MaxPollsPerTick per tick and MaxParallelPolls at the same time.
    /// </summary>
    public async Task PollDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var snapshots = (await _snapshots.Select()).ToDictionary(s => s.AgentId);
        var due = (await _agents.SelectEnabled())
            .Select(a => (Agent: a, LastAttempt: snapshots.GetValueOrDefault(a.Id)?.LastAttemptAt))
            .Where(x => !_scheduler.IsRunning(PollScheduler.AgentKey(x.Agent.Id))
                        && _scheduler.IsDue(PollScheduler.AgentKey(x.Agent.Id), x.Agent.PollIntervalSeconds, x.LastAttempt, now))
            .OrderBy(x => x.LastAttempt ?? DateTime.MinValue)
            .Select(x => x.Agent)
            .ToList();
        if (_settings.MaxPollsPerTick > 0 && due.Count > _settings.MaxPollsPerTick)
        {
            _logger.LogInformation("{Due} agents due, {Max} started this tick, the rest follows", due.Count, _settings.MaxPollsPerTick);
            due = due.Take(_settings.MaxPollsPerTick).ToList();
        }
        if (due.Count == 0)
            return;

        await Parallel.ForEachAsync(due,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _settings.MaxParallelPolls), CancellationToken = ct },
            async (agent, token) => await PollGuarded(agent, snapshots.GetValueOrDefault(agent.Id), token));
    }

    /// <summary>When the agent will be fetched next (for the UI).</summary>
    public DateTime? NextPollAt(AgentDb agent, DateTime? lastAttemptUtc) =>
        agent.Enabled
            ? _scheduler.NextRun(PollScheduler.AgentKey(agent.Id), agent.PollIntervalSeconds, lastAttemptUtc, DateTime.UtcNow)
            : null;

    /// <summary>POST api/agents/{id}/poll - fetch now.</summary>
    public async Task<ServiceResult<AgentSnapshotDb>> PollNow(int id, CancellationToken ct)
    {
        var agent = await _agents.SelectById(id);
        if (agent is null)
            return ServiceResult<AgentSnapshotDb>.NotFound($"Agent {id} nicht gefunden.");
        var snapshot = await PollGuarded(agent, await _snapshots.SelectByAgentId(id), ct);
        if (snapshot is null)
            return ServiceResult<AgentSnapshotDb>.Locked("Für diesen Agent läuft gerade schon ein Abruf.");
        snapshot.Payload = null;
        return ServiceResult<AgentSnapshotDb>.Ok(snapshot);
    }

    /// <summary>Runs PollAgent unless a fetch of the same agent is already running (then null).</summary>
    private async Task<AgentSnapshotDb?> PollGuarded(AgentDb agent, AgentSnapshotDb? previous, CancellationToken ct)
    {
        var key = PollScheduler.AgentKey(agent.Id);
        if (!_scheduler.TryStart(key))
            return null;
        try
        {
            return await PollAgent(agent, previous, ct);
        }
        finally
        {
            _scheduler.Finish(key);
        }
    }

    private async Task<AgentSnapshotDb> PollAgent(AgentDb agent, AgentSnapshotDb? previous, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var previousStatus = previous?.Status ?? MonitorStatus.Unknown;
        AgentSnapshotDb snapshot;
        List<string> findings;

        try
        {
            var fetched = await _fetch.Fetch(agent, ct);

            if (!agent.IsLocal() && agent.HostKeyFingerprint is null && fetched.HostKeyFingerprint is not null)
            {
                await _agents.UpdateHostKeyFingerprint(agent.Id, fetched.HostKeyFingerprint);
                _logger.LogInformation("Agent {Agent}: host key {Fingerprint} pinned (first use)",
                    agent.Name, fetched.HostKeyFingerprint);
            }

            var file = _evaluation.Parse(fetched.Content);
            var evaluation = _evaluation.Evaluate(file, now);
            await _known.Sync(agent.Id, file.Docker, now);
            _known.AddFindings(evaluation, await _known.GetMissing(agent.Id, file.Docker));
            var host = file.Host?.Current;
            findings = evaluation.Findings;

            snapshot = new AgentSnapshotDb
            {
                AgentId = agent.Id,
                LastAttemptAt = now,
                FetchedAt = now,
                GeneratedAt = file.GeneratedAt,
                Status = evaluation.Status,
                Message = findings.Count > 0 ? string.Join("\n", findings) : null,
                Payload = fetched.Content,
                CpuPercent = ToDecimal(host?.CpuPercent),
                MemoryPercent = ToDecimal(host?.Memory?.Percent),
                DiskPercentMax = ToDecimal(host?.Disks?.Max(d => d.Percent)),
                ContainersRunning = file.Docker?.Containers.Count(c => c.IsRunning),
                ContainersTotal = file.Docker?.Containers.Count
            };
            await _snapshots.Upsert(snapshot);
        }
        catch (Renci.SshNet.Common.SftpPathNotFoundException) when (!ct.IsCancellationRequested)
        {
            // SSH works, the VM is up - the agent did not write its file
            return await StoreFailure(agent, previous, now, MonitorStatus.Critical,
                $"monitoring.json nicht gefunden: {agent.RemotePath} (läuft der Agent? Bei chroot den Pfad relativ zum Chroot angeben).");
        }
        catch (Renci.SshNet.Common.SftpPermissionDeniedException) when (!ct.IsCancellationRequested)
        {
            return await StoreFailure(agent, previous, now, MonitorStatus.Critical,
                $"Keine Leserechte auf {agent.RemotePath} für den SSH-Benutzer.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Agent {Agent} ({Target}) not reachable: {Message}", agent.Name, agent.Target(), ex.Message);
            return await StoreFailure(agent, previous, now, MonitorStatus.Unreachable, ex.Message);
        }

        LogStatusChange(agent, previousStatus, snapshot.Status, findings);
        return snapshot;
    }

    /// <summary>
    /// Stores a failed fetch: new status and message, the last good payload is kept.
    /// Unreachable = SSH failed (VM down / network); Critical = VM reachable, but the file is missing / unreadable.
    /// </summary>
    private async Task<AgentSnapshotDb> StoreFailure(AgentDb agent, AgentSnapshotDb? previous, DateTime now,
        string status, string message)
    {
        var previousStatus = previous?.Status ?? MonitorStatus.Unknown;
        var snapshot = previous ?? new AgentSnapshotDb { AgentId = agent.Id };
        snapshot.LastAttemptAt = now;
        snapshot.Status = status;
        snapshot.Message = message;
        if (previous is null)
            await _snapshots.Upsert(snapshot);
        else
            await _snapshots.UpdateFailure(agent.Id, now, status, message);
        LogStatusChange(agent, previousStatus, status, [message]);
        return snapshot;
    }

    private void LogStatusChange(AgentDb agent, string previous, string current, IEnumerable<string> findings)
    {
        if (current != previous)
            _logger.LogInformation("Agent {Agent}: {Previous} -> {Current} ({Findings})",
                agent.Name, previous, current, string.Join("; ", findings));
    }

    private static decimal? ToDecimal(double? value) => value is null ? null : Math.Round((decimal)value.Value, 2);
}
