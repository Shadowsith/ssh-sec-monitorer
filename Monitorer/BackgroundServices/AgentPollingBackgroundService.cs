using Microsoft.Extensions.Options;
using Monitorer.Services;

namespace Monitorer.BackgroundServices;

/// <summary>
/// Ticks every Monitoring:PollTickSeconds and lets AgentPollService fetch all agents whose
/// individual poll interval has elapsed. The work itself lives in the service.
/// </summary>
public class AgentPollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonitoringSettings _settings;
    private readonly ILogger<AgentPollingBackgroundService> _logger;

    public AgentPollingBackgroundService(IServiceScopeFactory scopeFactory, IOptions<MonitoringSettings> settings,
        ILogger<AgentPollingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _settings.PollTickSeconds)));
        do
        {
            await RunOnce(stoppingToken);
        }
        while (await WaitForNextTick(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try { return await timer.WaitForNextTickAsync(stoppingToken); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task RunOnce(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AgentPollService>().PollDue(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Agent polling failed");
        }
    }
}
