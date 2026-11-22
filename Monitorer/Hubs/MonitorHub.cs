using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Monitorer.Hubs;

/// <summary>
/// Push channel for the UI (wwwroot/js/live.js). The server only announces which topics changed
/// (see LiveUpdateService); clients have nothing to call and re-fetch their page themselves.
/// </summary>
[Authorize]
public class MonitorHub : Hub
{
    public const string Path = "/hubs/monitor";
}
