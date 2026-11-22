using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Monitoring;
using Monitorer.Extensions;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>
/// Determines which alert conditions of a rule are currently fulfilled.
/// Alerts are layered: if a VM is unreachable only "VM unreachable" fires; if the VM is reachable but
/// its monitoring.json is stale only "agent stopped reporting" fires - status and container alerts
/// based on old data are suppressed (and neither resolved nor repeated) until the data is fresh again.
/// </summary>
public class AlertConditionService
{
    private const int DefaultStaleMinutes = 10;

    private readonly AgentRepository _agents;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly LiveCheckRepository _liveChecks;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly KnownContainerService _known;
    private readonly MonitoringSettings _settings;
    private readonly IStringLocalizer _l;

    public AlertConditionService(AgentRepository agents, AgentSnapshotRepository snapshots, LiveCheckRepository liveChecks,
        MonitoringEvaluationService evaluation, KnownContainerService known, IOptions<MonitoringSettings> settings,
        IStringLocalizer l)
    {
        _l = l;
        _settings = settings.Value;
        _agents = agents;
        _snapshots = snapshots;
        _liveChecks = liveChecks;
        _evaluation = evaluation;
        _known = known;
    }

    /// <summary>Loads agents, snapshots, parsed monitoring.json files and live checks once.</summary>
    public async Task<AlertContextDto> LoadContext()
    {
        var context = new AlertContextDto
        {
            Agents = (await _agents.SelectEnabled()).ToList(),
            Snapshots = (await _snapshots.SelectWithPayload()).ToDictionary(s => s.AgentId),
            LiveChecks = (await _liveChecks.SelectEnabled()).ToList()
        };
        foreach (var (agentId, snapshot) in context.Snapshots)
        {
            if (string.IsNullOrEmpty(snapshot.Payload))
                continue;
            try
            {
                context.Monitoring[agentId] = _evaluation.Parse(snapshot.Payload);
            }
            catch (JsonException)
            {
                // unreadable file: the AgentStatus / AgentStale rules still see the snapshot
            }
            snapshot.Payload = null;
        }
        context.MissingContainers = await _known.GetMissing(
            context.Agents.ToDictionary(a => a.Id, a => context.Monitoring.GetValueOrDefault(a.Id)?.Docker));
        return context;
    }

    public IEnumerable<AlertConditionDto> GetConditions(AlertRuleDb rule, AlertContextDto context, DateTime nowUtc)
    {
        var suppressed = SuppressedAgents(rule, context, nowUtc);
        var agents = AgentsInScope(rule, context).Where(a => !suppressed.Contains(a.Id)).ToList();
        return rule.Scenario switch
        {
            AlertScenarios.AgentUnreachable => agents.SelectMany(a => Unreachable(a, context)),
            AlertScenarios.AgentStale => agents.SelectMany(a => Stale(rule, a, context, nowUtc)),
            AlertScenarios.AgentStatus => agents.SelectMany(a => BadStatus(rule, a, context)),
            AlertScenarios.LiveCheckDown => LiveChecksDown(rule, context),
            AlertScenarios.ContainerDown => agents.SelectMany(a => ContainersDown(rule, a, context)),
            AlertScenarios.ContainerMissing => agents.SelectMany(a => ContainersMissing(rule, a, context)),
            _ => Enumerable.Empty<AlertConditionDto>()
        };
    }

    /// <summary>
    /// Agents whose alerts of this rule are on hold because a more basic problem exists:
    /// VM unreachable → everything except "VM unreachable";
    /// data stale (agent not writing) → status and container alerts.
    /// Active alerts of these agents are kept as they are (no reminder, no recovery mail).
    /// </summary>
    public HashSet<int> SuppressedAgents(AlertRuleDb rule, AlertContextDto context, DateTime nowUtc)
    {
        if (rule.Scenario is AlertScenarios.AgentUnreachable or AlertScenarios.LiveCheckDown)
            return new HashSet<int>();
        var result = new HashSet<int>();
        foreach (var agent in context.Agents)
        {
            var snapshot = context.Snapshots.GetValueOrDefault(agent.Id);
            if (snapshot?.Status == MonitorStatus.Unreachable)
            {
                result.Add(agent.Id);
                continue;
            }
            if (rule.Scenario == AlertScenarios.AgentStale)
                continue;
            var generatedAt = context.Monitoring.GetValueOrDefault(agent.Id)?.GeneratedAt;
            if (generatedAt is null || (nowUtc - generatedAt.Value).TotalSeconds > _settings.StaleAfterSeconds)
                result.Add(agent.Id);
        }
        return result;
    }

    /// <summary>"agent:3" / "agent:3/container:web" → 3; null for live checks.</summary>
    public static int? AgentIdFromKey(string subjectKey)
    {
        if (!subjectKey.StartsWith("agent:", StringComparison.Ordinal))
            return null;
        var end = subjectKey.IndexOf('/');
        var id = end < 0 ? subjectKey[6..] : subjectKey[6..end];
        return int.TryParse(id, out var value) ? value : null;
    }

    private static IEnumerable<AgentDb> AgentsInScope(AlertRuleDb rule, AlertContextDto context) =>
        context.Agents.Where(a => rule.AgentId is null || a.Id == rule.AgentId);

    private IEnumerable<AlertConditionDto> Unreachable(AgentDb agent, AlertContextDto context)
    {
        if (context.Snapshots.GetValueOrDefault(agent.Id) is { Status: MonitorStatus.Unreachable } snapshot)
            yield return Condition($"agent:{agent.Id}", agent.Name,
                agent.IsLocal()
                    ? _l["condition.localUnreadable", agent.Target(_l), snapshot.Message ?? ""].Value
                    : _l["condition.unreachable", agent.Target(_l), snapshot.Message ?? ""].Value);
    }

    private IEnumerable<AlertConditionDto> Stale(AlertRuleDb rule, AgentDb agent, AlertContextDto context, DateTime nowUtc)
    {
        var minutes = rule.ThresholdMinutes ?? DefaultStaleMinutes;
        var snapshot = context.Snapshots.GetValueOrDefault(agent.Id);
        // the agent's own timestamp; without a readable file the time of the last successful fetch
        var lastUpdate = context.Monitoring.GetValueOrDefault(agent.Id)?.GeneratedAt ?? snapshot?.FetchedAt;
        var reference = lastUpdate ?? agent.CreatedAt;
        if ((nowUtc - reference).TotalMinutes < minutes)
            yield break;
        yield return Condition($"agent:{agent.Id}", agent.Name, lastUpdate is null
            ? _l["condition.neverReceived"].Value
            : _l["condition.stale", (int)(nowUtc - lastUpdate.Value).TotalMinutes, lastUpdate.Value.ToBerlinString(_l), minutes].Value);
    }

    private IEnumerable<AlertConditionDto> BadStatus(AlertRuleDb rule, AgentDb agent, AlertContextDto context)
    {
        var minimum = rule.MinStatus == MonitorStatus.Critical ? MonitorStatus.Critical : MonitorStatus.Warning;
        if (context.Snapshots.GetValueOrDefault(agent.Id) is { } snapshot
            && snapshot.Status is MonitorStatus.Warning or MonitorStatus.Critical
            && MonitorStatus.Severity(snapshot.Status) >= MonitorStatus.Severity(minimum))
            yield return Condition($"agent:{agent.Id}", agent.Name, _l["condition.status", _l[MonitorStatus.TextKey(snapshot.Status)].Value, snapshot.Message ?? ""].Value);
    }

    private static IEnumerable<AlertConditionDto> LiveChecksDown(AlertRuleDb rule, AlertContextDto context) =>
        context.LiveChecks
            .Where(c => (rule.LiveCheckId is null || c.Id == rule.LiveCheckId) && c.LastStatus == MonitorStatus.Down)
            .Select(c => Condition($"livecheck:{c.Id}", c.Name, $"{c.Target}: {c.LastMessage}"));

    private IEnumerable<AlertConditionDto> ContainersDown(AlertRuleDb rule, AgentDb agent, AlertContextDto context)
    {
        if (context.Monitoring.GetValueOrDefault(agent.Id)?.Docker is not { Available: true } docker)
            yield break;
        foreach (var c in docker.Containers.Where(c => c.Name.MatchesWildcard(rule.ContainerPattern)))
        {
            var problem = DownReason(c, rule.IgnoreCleanExit);
            if (problem is not null)
                yield return Condition($"agent:{agent.Id}/container:{c.Name}", $"{agent.Name} / {c.Name}", problem);
        }
    }

    private string? DownReason(ContainerDto c, bool ignoreCleanExit)
    {
        if (c.IsUnhealthy)
            return _l["condition.containerUnhealthy"].Value;
        if (c.State is "restarting" or "dead")
            return _l["condition.containerRestarting", c.State ?? "", c.RestartCount ?? 0].Value;
        if (c.IsRunning || c.State is "created" or "paused" && ignoreCleanExit)
            return null;
        if (c.State == "exited" && c.ExitCode is null or 0 && ignoreCleanExit)
            return null;
        return _l["condition.containerNotRunning", c.State ?? "", c.ExitCode?.ToString() ?? "–"].Value;
    }

    private IEnumerable<AlertConditionDto> ContainersMissing(AlertRuleDb rule, AgentDb agent, AlertContextDto context) =>
        (context.MissingContainers.GetValueOrDefault(agent.Id) ?? [])
            .Where(k => k.Name.MatchesWildcard(rule.ContainerPattern))
            .Select(k => Condition($"agent:{agent.Id}/container:{k.Name}", $"{agent.Name} / {k.Name}",
                _l["condition.containerMissing", k.LastSeenAt.ToBerlinString(_l)].Value));

    private static AlertConditionDto Condition(string key, string name, string message) =>
        new() { SubjectKey = key, SubjectName = name, Message = message };
}
