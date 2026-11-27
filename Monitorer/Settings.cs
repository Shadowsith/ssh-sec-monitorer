namespace Monitorer;

/// <summary>Database connection (section "DbConfig").</summary>
public class DbConfig
{
    public string ConnectionString { get; set; } = "";
    public string MasterConnectionString { get; set; } = "";
}

/// <summary>JWT signing (section "Jwt"). The key must be at least 32 characters.</summary>
public class JwtSettings
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "SshSecMonitorer";
    public string Audience { get; set; } = "SshSecMonitorer";
    public int LifetimeMinutes { get; set; } = 480;
}

/// <summary>Browser session (section "Auth"): the JWT is kept in an HttpOnly cookie.</summary>
public class AuthSettings
{
    public string CookieName { get; set; } = "ssm_session";
    /// <summary>Allow plain HTTP: cookies are only marked Secure on HTTPS requests and HSTS is off.
    /// Keep false in production (HTTPS via reverse proxy); true only for tests without HTTPS.</summary>
    public bool AllowHttp { get; set; }
    public int MinPasswordLength { get; set; } = 12;
}

/// <summary>Initial admin user (section "Setup"). Used once if no user exists yet.</summary>
public class SetupSettings
{
    public string AdminUserName { get; set; } = "";
    public string AdminPassword { get; set; } = "";
}

/// <summary>Optional LDAP / Active Directory login (section "Ldap").</summary>
public class LdapSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 636;
    public bool UseSsl { get; set; } = true;
    public bool UseStartTls { get; set; }
    /// <summary>Directory with trusted CA certificates (Linux only), e.g. Assets/Certs.</summary>
    public string TrustedCertificatesDirectory { get; set; } = "";
    public string BindDn { get; set; } = "";
    public string BindPassword { get; set; } = "";
    public string SearchBase { get; set; } = "";
    /// <summary>{0} is replaced by the escaped user name.</summary>
    public string UserFilter { get; set; } = "(&(objectClass=user)(sAMAccountName={0}))";
    /// <summary>Members of this group may log in. Empty = every user found by the filter.</summary>
    public string UserGroupDn { get; set; } = "";
    /// <summary>Members of this group get the Admin role.</summary>
    public string AdminGroupDn { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>SMTP for warning mails (section "Mail").</summary>
public class MailSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    /// <summary>None | StartTls | SslOnConnect | Auto</summary>
    public string Security { get; set; } = "Auto";
    public bool UseAuth { get; set; }
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    /// <summary>Recipients, separated by ";".</summary>
    public string Recipients { get; set; } = "";
}

/// <summary>Polling, thresholds and live checks (section "Monitoring").</summary>
public class MonitoringSettings
{
    public int PollTickSeconds { get; set; } = 10;
    public int DefaultPollIntervalSeconds { get; set; } = 60;
    /// <summary>At most this many SFTP fetches at the same time.</summary>
    public int MaxParallelPolls { get; set; } = 8;
    /// <summary>At most this many fetches started per tick (0 = no limit); the rest follows in the next tick.</summary>
    public int MaxPollsPerTick { get; set; } = 0;
    public int SshTimeoutSeconds { get; set; } = 15;
    public int MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    /// <summary>Read-only mounted directory of local agents: &lt;root&gt;/&lt;local_dir&gt;/monitoring.json.</summary>
    public string LocalAgentRoot { get; set; } = "/app/agents";
    /// <summary>monitoring.json older than this is reported as stale.</summary>
    public int StaleAfterSeconds { get; set; } = 300;
    public double CpuWarnPercent { get; set; } = 85;
    public double CpuCriticalPercent { get; set; } = 95;
    public double MemoryWarnPercent { get; set; } = 85;
    public double MemoryCriticalPercent { get; set; } = 95;
    public double DiskWarnPercent { get; set; } = 85;
    public double DiskCriticalPercent { get; set; } = 95;
    public int LiveCheckTickSeconds { get; set; } = 5;
    /// <summary>Certificate checks: Warning at or below this many days left.</summary>
    public int CertWarnDays { get; set; } = 30;
    /// <summary>Certificate checks: Critical at or below this many days left.</summary>
    public int CertCriticalDays { get; set; } = 7;
    /// <summary>How often the alert rules are evaluated.</summary>
    public int AlertTickSeconds { get; set; } = 30;
    /// <summary>Status for containers that were seen once and are gone now: Warning | Critical | None.</summary>
    public string MissingContainerStatus { get; set; } = "Warning";
    /// <summary>Container names (wildcards, ";"-separated) that are never remembered, e.g. short-lived CI jobs: "build-job-*;*-tmp".</summary>
    public string KnownContainerIgnore { get; set; } = "";
}

/// <summary>CVE checks (section "Cve").</summary>
public class CveSettings
{
    /// <summary>Optional GitHub token for the opencve-kb source: 5000 instead of 60 API requests per hour.</summary>
    public string GitHubToken { get; set; } = "";
}

/// <summary>ASP.NET Data Protection key ring (section "DataProtection"). Encrypts stored SSH keys and passwords.</summary>
public class DataProtectionSettings
{
    public string KeyPath { get; set; } = "keys";
}

/// <summary>Swagger UI (section "Swagger").</summary>
public class SwaggerSettings
{
    public bool Enabled { get; set; }
}

/// <summary>UI language (section "Localization"). Texts live in i18n/&lt;language&gt;.json.</summary>
public class LocalizationSettings
{
    /// <summary>Used when the browser asks for no supported language, and for background work (alert mails, poll results).</summary>
    public string DefaultLanguage { get; set; } = "en";
}
