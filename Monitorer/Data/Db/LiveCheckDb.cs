namespace Monitorer.Data.Db;

/// <summary>Table "live_check": a reachable web service checked directly by the Monitorer.</summary>
public class LiveCheckDb
{
    public int Id { get; set; }                         // id
    public string Name { get; set; } = "";              // name
    public string CheckType { get; set; } = LiveCheckTypes.Http; // check_type
    public string Target { get; set; } = "";            // target (URL)
    public int IntervalSeconds { get; set; } = 60;      // interval_seconds
    public int TimeoutSeconds { get; set; } = 10;       // timeout_seconds
    public int? ExpectedStatusCode { get; set; }        // expected_status_code (HTTP only)
    public bool IgnoreTlsErrors { get; set; }           // ignore_tls_errors
    public string? MqttUserName { get; set; }           // mqtt_user_name
    public string? MqttPasswordEnc { get; set; }        // mqtt_password_enc
    public bool Enabled { get; set; } = true;           // enabled
    public string LastStatus { get; set; } = MonitorStatus.Unknown; // last_status
    public DateTime? LastCheckedAt { get; set; }        // last_checked_at
    public int? LastResponseMs { get; set; }            // last_response_ms
    public string? LastMessage { get; set; }            // last_message
    public DateTime CreatedAt { get; set; }             // created_at
}
