using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>Alert rule plus display information for the UI / API.</summary>
public class AlertRuleDto : AlertRuleDb
{
    public string ScenarioName => AlertScenarios.DisplayName(Scenario);
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
        ContainerPattern = row.ContainerPattern;
        ThresholdMinutes = row.ThresholdMinutes;
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
