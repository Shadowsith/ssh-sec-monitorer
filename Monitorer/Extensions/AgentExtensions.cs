using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;

namespace Monitorer.Extensions;

/// <summary>Helpers for AgentDb (kept out of the Db class, which maps the table 1:1).</summary>
public static class AgentExtensions
{
    public static bool IsLocal(this AgentDb agent) => agent.ConnectionType == AgentConnectionTypes.Local;

    /// <summary>"vm-web-01.intern:22" or "local: local" for the UI and messages.</summary>
    public static string Target(this AgentDb agent, IStringLocalizer l) =>
        agent.IsLocal() ? l["agents.localTarget", agent.LocalDir ?? ""].Value : $"{agent.Host}:{agent.Port}";
}
