using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>Certificate check for UI / API.</summary>
public class CertCheckDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    /// <summary>Empty = default port of the protocol.</summary>
    public int? Port { get; set; }
    public string Protocol { get; set; } = CertCheckProtocols.Tls;
    public string? ServerName { get; set; }
    public int IntervalSeconds { get; set; } = 3600;
    public int TimeoutSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = true;
    public string LastStatus { get; set; } = MonitorStatus.Unknown;
    public DateTime? LastCheckedAt { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? NotBefore { get; set; }
    public DateTime? NotAfter { get; set; }
    public int? DaysLeft { get; set; }
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
    public string? Thumbprint { get; set; }
    public bool? ChainValid { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Target => $"{Host}:{Port}";

    public CertCheckDto() { }

    public CertCheckDto(CertCheckDb row)
    {
        Id = row.Id;
        Name = row.Name;
        Host = row.Host;
        Port = row.Port;
        Protocol = row.Protocol;
        ServerName = row.ServerName;
        IntervalSeconds = row.IntervalSeconds;
        TimeoutSeconds = row.TimeoutSeconds;
        Enabled = row.Enabled;
        LastStatus = row.LastStatus;
        LastCheckedAt = row.LastCheckedAt;
        LastMessage = row.LastMessage;
        NotBefore = row.NotBefore;
        NotAfter = row.NotAfter;
        DaysLeft = row.DaysLeft(DateTime.UtcNow);
        Subject = row.Subject;
        Issuer = row.Issuer;
        Thumbprint = row.Thumbprint;
        ChainValid = row.ChainValid;
        CreatedAt = row.CreatedAt;
    }
}
