namespace Monitorer.Data;

/// <summary>General alert scenarios an alert rule can watch.</summary>
public static class AlertScenarios
{
    /// <summary>VM / agent host cannot be reached via SSH.</summary>
    public const string AgentUnreachable = "AgentUnreachable";
    /// <summary>monitoring.json was not updated for N minutes (agent stopped / hangs).</summary>
    public const string AgentStale = "AgentStale";
    /// <summary>Agent status is Warning / Critical (thresholds, findings).</summary>
    public const string AgentStatus = "AgentStatus";
    /// <summary>Live check (HTTP(S), WS(S), MQTT(S)) is down.</summary>
    public const string LiveCheckDown = "LiveCheckDown";
    /// <summary>Container is not running, restarting, dead or unhealthy.</summary>
    public const string ContainerDown = "ContainerDown";
    /// <summary>Container that was seen before is gone.</summary>
    public const string ContainerMissing = "ContainerMissing";

    public static readonly string[] All =
        [AgentUnreachable, AgentStale, AgentStatus, LiveCheckDown, ContainerDown, ContainerMissing];

    public static bool IsValid(string value) => All.Contains(value);

    public static bool UsesAgent(string scenario) => scenario != LiveCheckDown;
    public static bool UsesLiveCheck(string scenario) => scenario == LiveCheckDown;
    public static bool UsesContainerPattern(string scenario) => scenario is ContainerDown or ContainerMissing;

    /// <summary>Translation key of the display name, e.g. "scenario.agentUnreachable".</summary>
    public static string NameKey(string scenario) => "scenario." + char.ToLowerInvariant(scenario[0]) + scenario[1..];

    /// <summary>Translation key of the explanation shown in the rule editor.</summary>
    public static string DescriptionKey(string scenario) => NameKey(scenario) + ".hint";
}
