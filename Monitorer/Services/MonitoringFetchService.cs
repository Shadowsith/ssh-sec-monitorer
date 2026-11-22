using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Extensions;

namespace Monitorer.Services;

/// <summary>
/// Gets an agent's monitoring.json by its connection type: SFTP for remote VMs, the mounted directory for
/// local agents. Polling, snapshot, status and alerts work the same for both.
/// </summary>
public class MonitoringFetchService
{
    private readonly SshFetchService _ssh;
    private readonly SshCredentialService _credentials;
    private readonly LocalMonitoringSource _local;
    private readonly Microsoft.Extensions.Localization.IStringLocalizer _l;

    public MonitoringFetchService(SshFetchService ssh, SshCredentialService credentials, LocalMonitoringSource local,
        Microsoft.Extensions.Localization.IStringLocalizer l)
    {
        _l = l;
        _ssh = ssh;
        _credentials = credentials;
        _local = local;
    }

    public async Task<MonitoringFetchResultDto> Fetch(AgentDb agent, CancellationToken ct)
    {
        if (agent.IsLocal())
            return await _local.Read(agent, ct);

        var secrets = (agent.SshCredentialId is { } credentialId ? await _credentials.GetSecrets(credentialId) : null)
                      ?? throw new InvalidOperationException(_l["credentials.notFoundShort"].Value);
        return await _ssh.Download(agent, secrets, ct);
    }
}
