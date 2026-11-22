using System.Text.Json;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>Container data of all agents for the Docker view.</summary>
public class DockerOverviewService
{
    private readonly AgentService _agents;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly KnownContainerService _known;
    private readonly ILogger<DockerOverviewService> _logger;

    public DockerOverviewService(AgentService agents, AgentSnapshotRepository snapshots,
        MonitoringEvaluationService evaluation, KnownContainerService known, ILogger<DockerOverviewService> logger)
    {
        _known = known;
        _agents = agents;
        _snapshots = snapshots;
        _evaluation = evaluation;
        _logger = logger;
    }

    public async Task<ServiceResult<List<DockerHostDto>>> Get()
    {
        var snapshots = (await _snapshots.SelectWithPayload()).ToDictionary(s => s.AgentId);
        var hosts = new List<DockerHostDto>();
        foreach (var agent in (await _agents.GetAll()).Value ?? [])
        {
            var host = new DockerHostDto { Agent = agent, Snapshot = snapshots.GetValueOrDefault(agent.Id) };
            if (!string.IsNullOrEmpty(host.Snapshot?.Payload))
            {
                try
                {
                    host.Docker = _evaluation.Parse(host.Snapshot.Payload).Docker;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("monitoring.json of agent {Agent} unreadable: {Message}", agent.Name, ex.Message);
                }
                host.Snapshot.Payload = null;
            }
            hosts.Add(host);
        }

        var missing = await _known.GetMissing(hosts.ToDictionary(h => h.Agent.Id, h => h.Docker));
        foreach (var host in hosts)
            host.MissingContainers = missing.GetValueOrDefault(host.Agent.Id) ?? [];
        return ServiceResult<List<DockerHostDto>>.Ok(hosts);
    }
}
