using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Monitorer.Data.Dto;

namespace Monitorer.Services;

/// <summary>
/// Optional LDAP / Active Directory login. Searches the user with a service account,
/// then verifies the password with a bind as the user.
/// On Linux this needs libldap (installed in the Docker image).
/// </summary>
public class LdapService
{
    private readonly LdapSettings _settings;
    private readonly ILogger<LdapService> _logger;

    public LdapService(IOptions<LdapSettings> settings, ILogger<LdapService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool Enabled => _settings.Enabled;

    /// <summary>Returns the user if user name / password are valid and the user may log in, otherwise null.</summary>
    public Task<LdapUserDto?> Authenticate(string userName, string password) =>
        Task.Run(() => AuthenticateSync(userName, password));

    private LdapUserDto? AuthenticateSync(string userName, string password)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return null;

        try
        {
            SearchResultEntry entry;
            using (var serviceConn = Connect(new NetworkCredential(_settings.BindDn, _settings.BindPassword)))
            {
                var filter = string.Format(_settings.UserFilter, EscapeFilterValue(userName.Trim()));
                var request = new SearchRequest(_settings.SearchBase, filter, SearchScope.Subtree,
                    "sAMAccountName", "uid", "displayName", "cn", "mail", "memberOf");
                var response = (SearchResponse)serviceConn.SendRequest(request);
                if (response.Entries.Count != 1)
                {
                    _logger.LogInformation("LDAP login {UserName}: {Count} entries found", userName, response.Entries.Count);
                    return null;
                }
                entry = response.Entries[0];
            }

            // verify the password: bind as the user (throws LdapException on wrong password)
            using (Connect(new NetworkCredential(entry.DistinguishedName, password))) { }

            var groups = GetValues(entry, "memberOf");
            if (!string.IsNullOrWhiteSpace(_settings.UserGroupDn) && !IsMember(groups, _settings.UserGroupDn)
                && !IsMember(groups, _settings.AdminGroupDn))
            {
                _logger.LogInformation("LDAP login {UserName}: not member of the user group", userName);
                return null;
            }

            return new LdapUserDto
            {
                Dn = entry.DistinguishedName,
                UserName = GetValues(entry, "sAMAccountName").FirstOrDefault()
                           ?? GetValues(entry, "uid").FirstOrDefault() ?? userName.Trim(),
                DisplayName = GetValues(entry, "displayName").FirstOrDefault()
                              ?? GetValues(entry, "cn").FirstOrDefault() ?? userName.Trim(),
                Email = GetValues(entry, "mail").FirstOrDefault(),
                IsAdmin = IsMember(groups, _settings.AdminGroupDn)
            };
        }
        catch (LdapException ex)
        {
            _logger.LogInformation("LDAP login {UserName} failed: {Message}", userName, ex.Message);
            return null;
        }
    }

    private LdapConnection Connect(NetworkCredential credential)
    {
        var conn = new LdapConnection(new LdapDirectoryIdentifier(_settings.Host, _settings.Port))
        {
            AuthType = AuthType.Basic,
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        };
        conn.SessionOptions.ProtocolVersion = 3;
        conn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;

        if (_settings.UseSsl)
            conn.SessionOptions.SecureSocketLayer = true;

        if (OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(_settings.TrustedCertificatesDirectory))
        {
            var dir = Path.IsPathRooted(_settings.TrustedCertificatesDirectory)
                ? _settings.TrustedCertificatesDirectory
                : Path.Combine(AppContext.BaseDirectory, _settings.TrustedCertificatesDirectory);
            conn.SessionOptions.TrustedCertificatesDirectory = dir;
            conn.SessionOptions.StartNewTlsSessionContext();
        }

        if (!_settings.UseSsl && _settings.UseStartTls)
            conn.SessionOptions.StartTransportLayerSecurity(null);

        conn.Bind(credential);
        return conn;
    }

    private static List<string> GetValues(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute))
            return [];
        return entry.Attributes[attribute].GetValues(typeof(string)).Cast<string>().ToList();
    }

    private static bool IsMember(IEnumerable<string> groups, string groupDn) =>
        !string.IsNullOrWhiteSpace(groupDn) && groups.Any(g => string.Equals(g, groupDn, StringComparison.OrdinalIgnoreCase));

    /// <summary>RFC 4515 escaping so user input cannot change the LDAP filter.</summary>
    private static string EscapeFilterValue(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '\\' => @"\5c",
                '*' => @"\2a",
                '(' => @"\28",
                ')' => @"\29",
                '\0' => @"\00",
                _ => c.ToString()
            });
        }
        return sb.ToString();
    }
}
