using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>An active alert with the name of its rule.</summary>
public class ActiveAlertDto
{
    public AlertStateDb State { get; set; } = new();
    public string RuleName { get; set; } = "";
    public string Scenario { get; set; } = "";
    public int RepeatMinutes { get; set; }
    public int DelayMinutes { get; set; }

    /// <summary>Still inside the delay: detected, but no mail sent yet.</summary>
    public bool IsPending => State.SendCount == 0;

    public DateTime? NextMailAt => IsPending
        ? (DateTime?)State.FirstDetectedAt.AddMinutes(DelayMinutes)
        : RepeatMinutes > 0 && State.LastSentAt is not null ? State.LastSentAt.Value.AddMinutes(RepeatMinutes) : null;
}
