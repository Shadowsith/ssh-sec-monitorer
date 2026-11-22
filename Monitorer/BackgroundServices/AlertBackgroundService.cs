using Microsoft.Extensions.Options;
using Monitorer.Services;

namespace Monitorer.BackgroundServices;

/// <summary>Evaluates the alert rules every Monitoring:AlertTickSeconds.</summary>
public class AlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonitoringSettings _settings;
    private readonly ILogger<AlertBackgroundService> _logger;

    public AlertBackgroundService(IServiceScopeFactory scopeFactory, IOptions<MonitoringSettings> settings,
        ILogger<AlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _settings.AlertTickSeconds)));
        while (await WaitForNextTick(timer, stoppingToken))
            await RunOnce(stoppingToken);
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
            await scope.ServiceProvider.GetRequiredService<AlertEngineService>().Run(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Alert evaluation failed");
        }
    }
}
