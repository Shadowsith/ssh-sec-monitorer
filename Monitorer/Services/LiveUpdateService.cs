using Microsoft.AspNetCore.SignalR;
using Monitorer.Hubs;

namespace Monitorer.Services;

/// <summary>
/// Tells the open UI pages that data changed (SignalR, singleton). Only topic names are sent, never data:
/// a page that shows one of the topics (data-live="...") re-fetches itself with the user's own session.
/// </summary>
public class LiveUpdateService
{
    public const string Agents = "agents";
    public const string LiveChecks = "livechecks";
    public const string CertChecks = "certchecks";
    public const string Alerts = "alerts";

    public static string Agent(int agentId) => $"agent:{agentId}";

    private readonly IHubContext<MonitorHub> _hub;
    private readonly ILogger<LiveUpdateService> _logger;

    public LiveUpdateService(IHubContext<MonitorHub> hub, ILogger<LiveUpdateService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <summary>Never throws: a failed push must not break polling, checks or alerting.</summary>
    public async Task Publish(params string[] topics)
    {
        try
        {
            await _hub.Clients.All.SendAsync("changed", topics);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Live update {Topics} not sent", string.Join(",", topics));
        }
    }
}
