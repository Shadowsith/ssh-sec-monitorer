using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>Live check for UI / API. MqttPassword is write-only (empty on update = keep).</summary>
public class LiveCheckDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CheckType { get; set; } = LiveCheckTypes.Http;
    public string Target { get; set; } = "";
    public int IntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public int? ExpectedStatusCode { get; set; }
    public bool IgnoreTlsErrors { get; set; }
    public string? MqttUserName { get; set; }
    public string? MqttPassword { get; set; }
    public bool HasMqttPassword { get; set; }
    public bool Enabled { get; set; } = true;
    public string LastStatus { get; set; } = MonitorStatus.Unknown;
    public DateTime? LastCheckedAt { get; set; }
    public int? LastResponseMs { get; set; }
    public string? LastMessage { get; set; }
    public DateTime CreatedAt { get; set; }

    public LiveCheckDto() { }

    public LiveCheckDto(LiveCheckDb row)
    {
        Id = row.Id;
        Name = row.Name;
        CheckType = row.CheckType;
        Target = row.Target;
        IntervalSeconds = row.IntervalSeconds;
        TimeoutSeconds = row.TimeoutSeconds;
        ExpectedStatusCode = row.ExpectedStatusCode;
        IgnoreTlsErrors = row.IgnoreTlsErrors;
        MqttUserName = row.MqttUserName;
        HasMqttPassword = row.MqttPasswordEnc is not null;
        Enabled = row.Enabled;
        LastStatus = row.LastStatus;
        LastCheckedAt = row.LastCheckedAt;
        LastResponseMs = row.LastResponseMs;
        LastMessage = row.LastMessage;
        CreatedAt = row.CreatedAt;
    }
}
