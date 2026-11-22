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

    public static string DisplayName(string scenario) => scenario switch
    {
        AgentUnreachable => "VM nicht erreichbar",
        AgentStale => "Agent meldet sich nicht mehr",
        AgentStatus => "Grenzwerte / Agent-Status",
        LiveCheckDown => "Live-Dienst nicht erreichbar",
        ContainerDown => "Container gestoppt / unhealthy",
        ContainerMissing => "Container verschwunden",
        _ => scenario
    };

    public static string Description(string scenario) => scenario switch
    {
        AgentUnreachable => "Der Monitorer erreicht die VM nicht per SSH (VM aus, Netzwerk, SSH-Dienst).",
        AgentStale => "Die monitoring.json wurde länger als die eingestellte Zeit nicht aktualisiert – der Agent läuft nicht oder hängt. Nur bei erreichbarer VM.",
        AgentStatus => "Der Agent-Status ist Warnung oder Kritisch, z. B. CPU, RAM oder Festplatte über dem Grenzwert. Ruht, solange die VM nicht erreichbar oder die Daten veraltet sind.",
        LiveCheckDown => "Ein HTTP(S)-, WS(S)- oder MQTT(S)-Dienst antwortet nicht oder mit falschem Statuscode.",
        ContainerDown => "Ein Container läuft nicht mehr, startet ständig neu, ist dead oder unhealthy. Ruht, solange die VM nicht erreichbar oder die Daten veraltet sind.",
        ContainerMissing => "Ein Container, der früher da war, ist auf der VM nicht mehr vorhanden. Ruht, solange die VM nicht erreichbar oder die Daten veraltet sind.",
        _ => ""
    };
}
