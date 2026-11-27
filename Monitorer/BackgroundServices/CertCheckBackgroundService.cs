using Microsoft.Extensions.Options;
using Monitorer.Services;

namespace Monitorer.BackgroundServices;

/// <summary>Ticks every Monitoring:LiveCheckTickSeconds and runs all due certificate checks.</summary>
public class CertCheckBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonitoringSettings _settings;
    private readonly ILogger<CertCheckBackgroundService> _logger;

    public CertCheckBackgroundService(IServiceScopeFactory scopeFactory, IOptions<MonitoringSettings> settings,
        ILogger<CertCheckBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _settings.LiveCheckTickSeconds)));
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
            await scope.ServiceProvider.GetRequiredService<CertCheckRunnerService>().RunDue(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Certificate checks failed");
        }
    }
}
