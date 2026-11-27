namespace Monitorer.Data.Db;

/// <summary>Table "cert_check": the TLS certificate behind host:port, checked directly by the Monitorer.</summary>
public class CertCheckDb
{
    public int Id { get; set; }                         // id
    public string Name { get; set; } = "";              // name
    public string Host { get; set; } = "";              // host (public or local DNS name / IP)
    public int Port { get; set; } = 443;                // port
    public string Protocol { get; set; } = CertCheckProtocols.Tls; // protocol (Tls | Smtp | Imap | Ldap)
    public string? ServerName { get; set; }             // server_name (SNI, null = host)
    public int IntervalSeconds { get; set; } = 3600;    // interval_seconds
    public int TimeoutSeconds { get; set; } = 10;       // timeout_seconds
    public bool Enabled { get; set; } = true;           // enabled
    public string LastStatus { get; set; } = MonitorStatus.Unknown; // last_status
    public DateTime? LastCheckedAt { get; set; }        // last_checked_at
    public string? LastMessage { get; set; }            // last_message
    public DateTime? NotBefore { get; set; }            // not_before
    public DateTime? NotAfter { get; set; }             // not_after (kept when the host is unreachable)
    public string? Subject { get; set; }                // subject
    public string? Issuer { get; set; }                 // issuer
    public string? Thumbprint { get; set; }             // thumbprint
    public bool? ChainValid { get; set; }               // chain_valid (null = never read)
    public DateTime CreatedAt { get; set; }             // created_at

    /// <summary>Whole days until NotAfter (negative = expired), null if never read.</summary>
    public int? DaysLeft(DateTime nowUtc) =>
        NotAfter is null ? null : (int)Math.Floor((NotAfter.Value - nowUtc).TotalDays);

    public string Target => $"{Host}:{Port}";
}
