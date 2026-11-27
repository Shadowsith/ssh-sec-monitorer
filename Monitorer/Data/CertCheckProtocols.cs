namespace Monitorer.Data;

/// <summary>How the TLS session of a certificate check is started.</summary>
public static class CertCheckProtocols
{
    /// <summary>TLS right after connecting (HTTPS, LDAPS, SMTPS, IMAPS, ...).</summary>
    public const string Tls = "Tls";
    /// <summary>SMTP STARTTLS (EHLO → STARTTLS).</summary>
    public const string Smtp = "Smtp";
    /// <summary>IMAP STARTTLS.</summary>
    public const string Imap = "Imap";
    /// <summary>LDAP StartTLS extended operation (OID 1.3.6.1.4.1.1466.20037).</summary>
    public const string Ldap = "Ldap";

    public static readonly string[] All = [Tls, Smtp, Imap, Ldap];

    public static bool IsValid(string value) => All.Contains(value);

    /// <summary>Port used when none is entered.</summary>
    public static int DefaultPort(string protocol) => protocol switch
    {
        Smtp => 587,
        Imap => 143,
        Ldap => 389,
        _ => 443
    };

    /// <summary>Display name for select box and table.</summary>
    public static string DisplayName(string protocol) => protocol switch
    {
        Smtp => "SMTP STARTTLS",
        Imap => "IMAP STARTTLS",
        Ldap => "LDAP StartTLS",
        _ => "TLS"
    };
}
