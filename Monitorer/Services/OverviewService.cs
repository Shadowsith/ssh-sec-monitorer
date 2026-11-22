using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>Data for the main overview: all agents with their latest snapshot plus all live checks.</summary>
public class OverviewService
{
    private readonly AgentService _agents;
    private readonly AgentSnapshotRepository _snapshots;
    private readonly LiveCheckService _liveChecks;

    public OverviewService(AgentService agents, AgentSnapshotRepository snapshots, LiveCheckService liveChecks)
    {
        _agents = agents;
        _snapshots = snapshots;
        _liveChecks = liveChecks;
    }

    public async Task<ServiceResult<OverviewDto>> Get()
    {
        var snapshots = (await _snapshots.Select()).ToDictionary(s => s.AgentId);
        var agents = (await _agents.GetAll()).Value ?? [];
        var liveChecks = (await _liveChecks.GetAll()).Value ?? [];

        return ServiceResult<OverviewDto>.Ok(new OverviewDto
        {
            Agents = agents
                .Select(a => new AgentOverviewDto { Agent = a, Snapshot = snapshots.GetValueOrDefault(a.Id) })
                .ToList(),
            LiveChecks = liveChecks
        });
    }
}
