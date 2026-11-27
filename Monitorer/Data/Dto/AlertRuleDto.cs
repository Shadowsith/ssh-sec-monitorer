using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>Alert rule plus display information for the UI / API.</summary>
public class AlertRuleDto : AlertRuleDb
{
    /// <summary>Display name of the scenario in the request language (set by AlertRuleService).</summary>
    public string ScenarioName { get; set; } = "";
    public string ScopeText { get; set; } = "";
    public int ActiveCount { get; set; }

    public AlertRuleDto() { }

    public AlertRuleDto(AlertRuleDb row)
    {
        Id = row.Id;
        Name = row.Name;
        Scenario = row.Scenario;
        AgentId = row.AgentId;
        LiveCheckId = row.LiveCheckId;
        CertCheckId = row.CertCheckId;
        CveCheckId = row.CveCheckId;
        ContainerPattern = row.ContainerPattern;
        ThresholdMinutes = row.ThresholdMinutes;
        ThresholdDays = row.ThresholdDays;
        MinStatus = row.MinStatus;
        IgnoreCleanExit = row.IgnoreCleanExit;
        DelayMinutes = row.DelayMinutes;
        RepeatMinutes = row.RepeatMinutes;
        NotifyRecovery = row.NotifyRecovery;
        Recipients = row.Recipients;
        Enabled = row.Enabled;
        CreatedAt = row.CreatedAt;
    }
}
