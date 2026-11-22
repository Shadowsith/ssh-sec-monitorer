namespace Monitorer.Data;

/// <summary>How the Monitorer gets an agent's monitoring.json (the agent itself is always passive).</summary>
public static class AgentConnectionTypes
{
    /// <summary>Remote VM: fetched via SFTP.</summary>
    public const string Ssh = "Ssh";
    /// <summary>Agent on the Monitorer host: read from a mounted directory below Monitoring:LocalAgentRoot.</summary>
    public const string Local = "Local";

    public static bool IsValid(string value) => value is Ssh or Local;
}
