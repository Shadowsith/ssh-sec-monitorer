namespace Monitorer.Data.Db;

/// <summary>Table "alert_rule": when to send which alert mail.</summary>
public class AlertRuleDb
{
    public int Id { get; set; }                                 // id
    public string Name { get; set; } = "";                      // name
    public string Scenario { get; set; } = AlertScenarios.AgentUnreachable; // scenario
    public int? AgentId { get; set; }                           // agent_id (null = all agents)
    public int? LiveCheckId { get; set; }                       // live_check_id (null = all live checks)
    public int? CertCheckId { get; set; }                       // cert_check_id (null = all certificate checks)
    public int? CveCheckId { get; set; }                        // cve_check_id (null = all CVE checks)
    public string? ContainerPattern { get; set; }               // container_pattern (wildcards, null = all)
    public int? ThresholdMinutes { get; set; }                  // threshold_minutes (AgentStale)
    public int? ThresholdDays { get; set; }                     // threshold_days (CertExpiring: alert at <= N days left)
    public string? MinStatus { get; set; }                      // min_status (AgentStatus: Warning | Critical)
    public bool IgnoreCleanExit { get; set; } = true;           // ignore_clean_exit (ContainerDown)
    public int DelayMinutes { get; set; }                       // delay_minutes (condition must last this long)
    public int RepeatMinutes { get; set; } = 60;                // repeat_minutes (0 = send once)
    public bool NotifyRecovery { get; set; } = true;            // notify_recovery
    public string Recipients { get; set; } = "";                // recipients (";"-separated, empty = Mail:Recipients)
    public bool Enabled { get; set; } = true;                   // enabled
    public DateTime CreatedAt { get; set; }                     // created_at
}
