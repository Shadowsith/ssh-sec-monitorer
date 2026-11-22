namespace Monitorer.Data.Db;

/// <summary>Table "alert_state": one active alert (rule + affected VM / service / container).</summary>
public class AlertStateDb
{
    public int Id { get; set; }                         // id
    public int RuleId { get; set; }                     // rule_id
    public string SubjectKey { get; set; } = "";        // subject_key, e.g. "agent:3/container:web"
    public string SubjectName { get; set; } = "";       // subject_name, e.g. "vm-web-01 / web"
    public string? Message { get; set; }                // message
    public DateTime FirstDetectedAt { get; set; }       // first_detected_at
    public DateTime? LastSentAt { get; set; }           // last_sent_at
    public int SendCount { get; set; }                  // send_count
}
