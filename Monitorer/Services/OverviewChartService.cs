using System.Text.Json;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Monitoring;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>Aggregates agents, monitoring.json files and live checks into the overview diagrams.</summary>
public class OverviewChartService
{
    private readonly AgentRepository _agents;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly LiveCheckRepository _liveChecks;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly KnownContainerService _known;
    private readonly MonitoringSettings _settings;

    public OverviewChartService(AgentRepository agents, AgentSnapshotRepository snapshots, LiveCheckRepository liveChecks,
        MonitoringEvaluationService evaluation, KnownContainerService known, IOptions<MonitoringSettings> settings)
    {
        _agents = agents;
        _snapshots = snapshots;
        _liveChecks = liveChecks;
        _evaluation = evaluation;
        _known = known;
        _settings = settings.Value;
    }

    public async Task<ServiceResult<OverviewChartsDto>> Get()
    {
        var charts = new OverviewChartsDto
        {
            CpuWarnPercent = _settings.CpuWarnPercent,
            CpuCriticalPercent = _settings.CpuCriticalPercent
        };
        var agents = (await _agents.Select()).ToList();
        var snapshots = (await _snapshots.SelectWithPayload()).ToDictionary(s => s.AgentId);
        var files = new Dictionary<int, MonitoringFileDto>();

        foreach (var agent in agents)
        {
            var snapshot = snapshots.GetValueOrDefault(agent.Id);
            var status = agent.Enabled ? snapshot?.Status ?? MonitorStatus.Unknown : "Disabled";
            charts.AgentStatus[status] = charts.AgentStatus.GetValueOrDefault(status) + 1;
            if (!agent.Enabled)
                continue;

            if (TryParse(snapshot?.Payload) is { } file)
                files[agent.Id] = file;
            charts.Usage.Add(new ChartUsageDto
            {
                AgentId = agent.Id,
                Name = agent.Name,
                Status = status,
                Cpu = snapshot?.CpuPercent,
                Memory = snapshot?.MemoryPercent,
                Disk = snapshot?.DiskPercentMax
            });

            var history = files.GetValueOrDefault(agent.Id)?.Host?.History;
            if (history?.GetValueOrDefault("cpu_percent") is { Count: > 0 } cpu)
                charts.CpuHistory[agent.Name] = cpu;
            if (history?.GetValueOrDefault("memory_percent") is { Count: > 0 } memory)
                charts.MemoryHistory[agent.Name] = memory;
        }

        var enabled = agents.Where(a => a.Enabled).ToList();
        var missing = await _known.GetMissing(enabled.ToDictionary(a => a.Id, a => files.GetValueOrDefault(a.Id)?.Docker));
        foreach (var agent in enabled)
        {
            var containers = files.GetValueOrDefault(agent.Id)?.Docker?.Containers;
            var missingCount = missing.GetValueOrDefault(agent.Id)?.Count ?? 0;
            if (containers is null && missingCount == 0)
                continue;
            containers ??= [];
            charts.Containers.Add(new ChartContainerCountDto
            {
                Name = agent.Name,
                Running = containers.Count(c => c.IsRunning && c.StateCssClass == "success"),
                Problem = containers.Count(c => c.StateCssClass is "danger" or "warning"),
                Stopped = containers.Count(c => c.StateCssClass == "secondary"),
                Missing = missingCount
            });
        }

        foreach (var check in await _liveChecks.Select())
        {
            var status = check.Enabled ? check.LastStatus : "Disabled";
            charts.LiveCheckStatus[status] = charts.LiveCheckStatus.GetValueOrDefault(status) + 1;
            if (check.Enabled)
                charts.LiveChecks.Add(new ChartLiveCheckDto { Name = check.Name, Status = status, ResponseMs = check.LastResponseMs });
        }

        charts.Usage = charts.Usage.OrderByDescending(u => new[] { u.Cpu, u.Memory, u.Disk }.Max() ?? -1).ToList();
        return ServiceResult<OverviewChartsDto>.Ok(charts);
    }

    private MonitoringFileDto? TryParse(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
            return null;
        try
        {
            return _evaluation.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
